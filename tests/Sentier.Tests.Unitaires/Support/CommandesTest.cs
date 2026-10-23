using Sentier.Commandes;
using Sentier.Definitions;
using Sentier.Migration;
using Sentier.Variables;

namespace Sentier.Tests.Unitaires.Support;

// Commandes et requêtes génériques, découvertes par balayage de l'assembly de tests.

/// <summary>Trace son passage dans le journal : « nœud:moment[:message] ».</summary>
[CommandeSentier("tracer")]
public sealed class Tracer(Journal journal) : ICommandeSentier
{
    public Task ExecuterAsync(ContexteCommande contexte, CancellationToken ct)
    {
        var message = contexte.Parametres.LireOuDefaut<string?>("message");
        journal.Ecrire($"{contexte.CodeNoeud}:{contexte.Moment}{(message is null ? "" : ":" + message)}");
        return Task.CompletedTask;
    }
}

/// <summary>Écrit la valeur du paramètre « valeur » dans la variable nommée par le paramètre « variable ».</summary>
[CommandeSentier("ecrire")]
public sealed class EcrireValeur : ICommandeSentier
{
    public Task ExecuterAsync(ContexteCommande contexte, CancellationToken ct)
    {
        contexte.Variables.Ecrire(contexte.Parametres.Lire<string>("variable"), contexte.Parametres.Valeurs.GetValueOrDefault("valeur"));
        return Task.CompletedTask;
    }
}

/// <summary>Incrémente la variable entière « compteur ».</summary>
[CommandeSentier("incrementer")]
public sealed class Incrementer : ICommandeSentier
{
    public Task ExecuterAsync(ContexteCommande contexte, CancellationToken ct)
    {
        contexte.Variables.Ecrire("compteur", (contexte.Variables.Lire("compteur") as long? ?? 0) + 1);
        return Task.CompletedTask;
    }
}

/// <summary>Échoue toujours.</summary>
[CommandeSentier("echouer")]
public sealed class Echouer : ICommandeSentier
{
    public Task ExecuterAsync(ContexteCommande contexte, CancellationToken ct)
        => throw new InvalidOperationException("Échec volontaire.");
}

/// <summary>Échoue tant que l'interrupteur est levé.</summary>
public sealed class CommandeInstable(Interrupteurs interrupteurs, Journal journal) : ICommandeSentier
{
    public Task ExecuterAsync(ContexteCommande contexte, CancellationToken ct)
    {
        if (interrupteurs.Echouer)
        {
            throw new InvalidOperationException("Service indisponible.");
        }

        journal.Ecrire($"{contexte.CodeNoeud}:instable");
        return Task.CompletedTask;
    }
}

public sealed class ParametresFacture
{
    public long Numero { get; set; }

    public decimal Montant { get; set; }

    public string? Client { get; set; }

    public bool Urgente { get; set; }
}

/// <summary>Commande à paramètres liés à une classe.</summary>
public sealed class EmettreFacture(Journal journal) : CommandeSentier<ParametresFacture>
{
    protected override Task ExecuterAsync(ContexteCommande contexte, ParametresFacture p, CancellationToken ct)
    {
        journal.Ecrire($"facture {p.Numero} {p.Client} {p.Montant:0.00} {(p.Urgente ? "urgente" : "normale")}");
        return Task.CompletedTask;
    }
}

/// <summary>Vrai si le paramètre booléen « valeur » est vrai.</summary>
[RequeteSentier("vrai")]
public sealed class ParametreVrai(Journal journal) : IRequeteSentier
{
    public Task<bool> EvaluerAsync(ContexteRequete contexte, CancellationToken ct)
    {
        var valeur = contexte.Parametres.Lire<bool>("valeur");
        journal.Ecrire($"{contexte.CodeNoeud}:requete:{valeur}");
        return Task.FromResult(valeur);
    }
}

public sealed class SeuilCompteur
{
    public long Compteur { get; set; }

    public long Seuil { get; set; }
}

/// <summary>Requête à paramètres liés à une classe.</summary>
public sealed class CompteurAtteint : RequeteSentier<SeuilCompteur>
{
    protected override Task<bool> EvaluerAsync(ContexteRequete contexte, SeuilCompteur p, CancellationToken ct)
        => Task.FromResult(p.Compteur >= p.Seuil);
}

/// <summary>Note si le contexte de la requête donne accès en écriture aux variables.</summary>
public sealed class SondeLectureSeule(Journal journal) : IRequeteSentier
{
    public Task<bool> EvaluerAsync(ContexteRequete contexte, CancellationToken ct)
    {
        journal.Ecrire(contexte.Variables is VariablesProcessus ? "variables:ecriture" : "variables:lecture");
        return Task.FromResult(true);
    }
}

/// <summary>Intercepteur qui encadre chaque commande et chaque requête.</summary>
public sealed class IntercepteurTrace(Journal journal) : IIntercepteurCommande, IIntercepteurRequete
{
    public async Task ExecuterAsync(string nomCommande, ContexteCommande contexte, Func<Task> suivant, CancellationToken ct)
    {
        journal.Ecrire($"avant {nomCommande}");
        await suivant();
        journal.Ecrire($"après {nomCommande}");
    }

    public async Task<bool> EvaluerAsync(string nomRequete, ContexteRequete contexte, Func<Task<bool>> suivant, CancellationToken ct)
    {
        var resultat = await suivant();
        journal.Ecrire($"requête {nomRequete} = {resultat}");
        return resultat;
    }
}

/// <summary>
/// Configuration de la définition et du plan « dynamique » : les tests de déploiement et de migration
/// changent la définition d'une version à l'autre sans changer de classe.
/// </summary>
public sealed class ConfigurationDynamique
{
    public Func<DefinitionProcessus>? Definition { get; set; }

    public Action<ConstructeurPlanMigration>? Plan { get; set; }

    public int PlanVersionMin { get; set; } = 1;

    public int PlanVersionMax { get; set; } = int.MaxValue;
}

/// <summary>Définition découverte par balayage dont le contenu vient de <see cref="ConfigurationDynamique"/>.</summary>
public sealed class DefinitionDynamique(ConfigurationDynamique configuration) : IDefinitionSentier
{
    public const string Cle = "dynamique";

    public DefinitionProcessus Definir() => configuration.Definition?.Invoke()
        ?? ConstructeurProcessus.Nouveau(Cle).Systemique("tracer", "tracer").Fin("fin").Construire();
}

/// <summary>Plan de migration découvert par balayage dont les règles viennent de <see cref="ConfigurationDynamique"/>.</summary>
public sealed class PlanDynamique(ConfigurationDynamique configuration) : IPlanMigration
{
    public string CleProcessus => DefinitionDynamique.Cle;

    public int VersionSourceMin => configuration.Plan is null ? 1 : configuration.PlanVersionMin;

    public int VersionSourceMax => configuration.Plan is null ? 0 : configuration.PlanVersionMax;

    public void Definir(ConstructeurPlanMigration plan) => configuration.Plan!(plan);
}

/// <summary>Définition déclarée par classe, avec ses variables en champs statiques.</summary>
[ProcessusSentier("demande-conge", Nom = "Demande de congé")]
public sealed class DemandeCongeTest : DefinitionSentier
{
    public static readonly Variable<long> NbJours = Variable.Entier("nbJours").Obligatoire();
    public static readonly Variable<long> Solde = Variable.Entier("solde").ParDefaut(10);
    public static readonly Variable<string> Decision = Variable.Texte("decision");

    protected override void Definir(ConstructeurProcessus processus) => processus
        .Decision("solde-suffisant", d => d
            .Si<CompteurAtteint>(p => p.Variable("compteur", Solde).Variable("seuil", NbJours))
            .Sinon(b => b.Fin("refusee")))
        .Interactif("approbation", "Approuver la demande", i => i.Role("gestionnaire").Donnees(NbJours))
        .Fin("approuvee");
}

/// <summary>Sous-processus déclaré par classe, sans attribut : la clé est le nom de la classe.</summary>
public sealed class ArchivageTest : DefinitionSentier
{
    protected override void Definir(ConstructeurProcessus processus) => processus
        .Systemique("archiver", "tracer")
        .Fin("archive");
}
