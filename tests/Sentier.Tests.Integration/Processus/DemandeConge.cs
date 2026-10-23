using Dapper;
using Sentier.Commandes;
using Sentier.Definitions;
using Sentier.Tests.Integration.Support;
using Sentier.Variables;

namespace Sentier.Tests.Integration.Processus;

// Processus métier de bout en bout, inspiré du README : toutes les commandes et requêtes travaillent
// sur les tables du client, dans la même transaction que Sentier.

/// <summary>Demande de congé : solde, approbation, validation RH avec relances, débit du solde, archivage.</summary>
[ProcessusSentier("demande-conge", Nom = "Demande de congé")]
public sealed class DemandeConge : DefinitionSentier
{
    public const string SignalRh = "VALIDATION_RH";

    public static readonly Variable<long> NbJours = Variable.Entier("nbJours").Obligatoire();
    public static readonly Variable<long> Solde = Variable.Entier("solde").ParDefaut(0);
    public static readonly Variable<string> Decision = Variable.Texte("decision");
    public static readonly Variable<long> Relances = Variable.Entier("relances").ParDefaut(0);
    public static readonly Variable<string> NumeroArchive = Variable.Texte("numeroArchive");
    public static readonly Variable<string> ValidePar = Variable.Texte("validePar");

    protected override void Definir(ConstructeurProcessus processus) => processus
        .Systemique<CalculerSolde>("calculer-solde", p => p.CleAffaire("employe"))
        .Decision("solde-suffisant", d => d
            .Si<SoldeSuffisant>(p => p.Variable("jours", NbJours).Variable(Solde))
            .Sinon(b => b.Fin("refusee")))
        .Interactif("approbation", "Approuver la demande", i => i
            .Role("gestionnaire")
            .Donnees(NbJours, Solde)
            .Echeance(TimeSpan.FromDays(5))
            .CommandeSortie<Journaliser>(p => p.Valeur("evenement", "decision-gestionnaire").VariableOptionnelle("detail", Decision)))
        .Decision("est-approuvee", d => d
            .Si<EstApprouvee>(p => p.VariableOptionnelle(Decision))
            .Sinon("refusee-gestionnaire"))
        .Attente("attendre-rh", a => a
            .Signal(SignalRh)
            .Echeance(TimeSpan.FromDays(2)).Alors(b => b
                .Systemique<Relancer>("relancer-rh")
                .Vers("attendre-rh")))
        .Systemique<DebiterSolde>("debiter-solde", p => p.CleAffaire("employe").Variable("jours", NbJours))
        .SousProcessus<ArchivageConge>("archivage", s => s
            .Entree(ArchivageConge.Jours, NbJours)
            .Sortie(ArchivageConge.Numero, NumeroArchive))
        .Fin("approuvee")
        .Fin("refusee-gestionnaire");
}

/// <summary>Sous-processus d'archivage, démarré sur la même clé d'affaire (l'employé).</summary>
[ProcessusSentier("archivage-conge", Nom = "Archivage d'un congé")]
public sealed class ArchivageConge : DefinitionSentier
{
    public static readonly Variable<long> Jours = Variable.Entier("nbJours").Obligatoire();
    public static readonly Variable<string> Numero = Variable.Texte("numero");

    protected override void Definir(ConstructeurProcessus processus) => processus
        .Systemique<Archiver>("archiver", p => p.CleAffaire("employe").Variable("jours", Jours))
        .Fin("archivee");
}

public sealed class CalculerSolde(ISessionSentier session) : ICommandeSentier
{
    public async Task ExecuterAsync(ContexteCommande contexte, CancellationToken ct)
    {
        var solde = await session.Connexion.ExecuteScalarAsync<long?>(new CommandDefinition(
            "SELECT SOLDE FROM CLIENT_EMPLOYE WHERE ID = @id", new { id = contexte.Parametres.Lire<long>("employe") }, session.Transaction, cancellationToken: ct));
        contexte.Variables.Ecrire(DemandeConge.Solde, solde ?? 0);
    }
}

public sealed class ParametresSolde
{
    public long Jours { get; set; }

    public long Solde { get; set; }
}

public sealed class SoldeSuffisant : RequeteSentier<ParametresSolde>
{
    protected override Task<bool> EvaluerAsync(ContexteRequete contexte, ParametresSolde p, CancellationToken ct)
        => Task.FromResult(p.Jours <= p.Solde);
}

public sealed class EstApprouvee : IRequeteSentier
{
    public Task<bool> EvaluerAsync(ContexteRequete contexte, CancellationToken ct)
        => Task.FromResult(contexte.Parametres.LireOuDefaut<string?>("decision") == "approuvee");
}

/// <summary>Écrit dans le journal du client ; reçoit les données de la tâche complétée en sortie d'un nœud interactif.</summary>
public sealed class Journaliser(ISessionSentier session) : ICommandeSentier
{
    public Task ExecuterAsync(ContexteCommande contexte, CancellationToken ct)
        => JournalClient.EcrireAsync(session, contexte.CleAffaire, contexte.Parametres.Lire<string>("evenement"), contexte.Parametres.LireOuDefaut<string?>("detail"), contexte.Utilisateur, ct);
}

public sealed class Relancer(ISessionSentier session, Pannes pannes) : ICommandeSentier
{
    public async Task ExecuterAsync(ContexteCommande contexte, CancellationToken ct)
    {
        if (pannes.Relance)
        {
            throw new InvalidOperationException("Messagerie indisponible.");
        }

        var relances = contexte.Variables.Lire(DemandeConge.Relances) + 1;
        contexte.Variables.Ecrire(DemandeConge.Relances, relances);
        await JournalClient.EcrireAsync(session, contexte.CleAffaire, "relance-rh", relances.ToString(), contexte.Utilisateur, ct);
    }
}

public sealed class DebiterSolde(ISessionSentier session) : ICommandeSentier
{
    public Task ExecuterAsync(ContexteCommande contexte, CancellationToken ct)
        => session.Connexion.ExecuteAsync(new CommandDefinition(
            "UPDATE CLIENT_EMPLOYE SET SOLDE = SOLDE - @jours WHERE ID = @id",
            new { id = contexte.Parametres.Lire<long>("employe"), jours = contexte.Parametres.Lire<long>("jours") },
            session.ExigerTransaction(),
            cancellationToken: ct));
}

public sealed class Archiver(ISessionSentier session, Pannes pannes) : ICommandeSentier
{
    public async Task ExecuterAsync(ContexteCommande contexte, CancellationToken ct)
    {
        if (pannes.Archivage)
        {
            throw new InvalidOperationException("Service d'archivage indisponible.");
        }

        var employe = contexte.Parametres.Lire<long>("employe");
        var numero = $"ARC-{employe}-{contexte.IdInstance.Valeur}";
        await session.Connexion.ExecuteAsync(new CommandDefinition(
            "INSERT INTO CLIENT_ARCHIVE (NUMERO, ID_EMPLOYE, JOURS) VALUES (@numero, @employe, @jours)",
            new { numero, employe, jours = contexte.Parametres.Lire<long>("jours") },
            session.ExigerTransaction(),
            cancellationToken: ct));
        contexte.Variables.Ecrire(ArchivageConge.Numero, numero);
    }
}

public static class JournalClient
{
    public static Task EcrireAsync(ISessionSentier session, long employe, string evenement, string? detail, string? utilisateur, CancellationToken ct)
        => session.Connexion.ExecuteAsync(new CommandDefinition(
            "INSERT INTO CLIENT_JOURNAL (ID_EMPLOYE, EVENEMENT, DETAIL, UTILISATEUR) VALUES (@employe, @evenement, @detail, @utilisateur)",
            new { employe, evenement, detail, utilisateur },
            session.ExigerTransaction(),
            cancellationToken: ct));

    public static IReadOnlyList<(string Evenement, string? Detail, string? Utilisateur)> Lire(System.Data.IDbConnection connexion, long employe)
        => connexion.Query<(string, string?, string?)>("SELECT EVENEMENT, DETAIL, UTILISATEUR FROM CLIENT_JOURNAL WHERE ID_EMPLOYE = @employe ORDER BY ID", new { employe }).ToList();
}
