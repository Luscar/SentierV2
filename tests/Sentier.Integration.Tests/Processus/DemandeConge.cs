using Sentier.Commandes;
using Sentier.Definitions;
using Sentier.Integration.Tests.Infrastructure;
using Sentier.Variables;

namespace Sentier.Integration.Tests.Processus;

/// <summary>
/// Systémique → décision sur variables → tâche (données, commande de sortie) → décision → fin.
/// </summary>
[ProcessusSentier(Cle, Nom = "Demande de congé")]
public sealed class DemandeConge : DefinitionSentier
{
    public const string Cle = "demande-conge";
    public const string NoeudApprobation = "approbation";

    public static readonly Variable<long> NbJours = Variable.Entier("nbJours").Obligatoire();
    public static readonly Variable<long> Solde = Variable.Entier("solde").ParDefaut(0);
    public static readonly Variable<string> Decision = Variable.Texte("decision");
    public static readonly Variable<string> Motif = Variable.Texte("motif");

    protected override void Definir(ConstructeurProcessus processus) => processus
        .Systemique<CalculerSolde>("calculer-solde", p => p.CleAffaire("employe"))
        .Decision("solde-suffisant", d => d
            .Si(NbJours).SuperieurA(Solde).Nommee("solde-insuffisant").Alors(b => b.Fin("refusee"))
            .Sinon())
        .Interactif(NoeudApprobation, "Approuver la demande", i => i
            .Role("gestionnaire")
            .Echeance(TimeSpan.FromDays(3))
            .Donnees(NbJours, Solde)
            .CommandeSortie<JournaliserDecision>(p => p.Variable(Decision)))
        .Decision("est-approuvee", d => d
            .Si(Decision).EgalA("approuvee").Vers("approuvee")
            .Sinon("rejetee"))
        .Fin("approuvee")
        .Fin("rejetee");
}

public sealed class CalculerSolde(ServicesMetier metier) : ICommandeSentier
{
    public Task ExecuterAsync(ContexteCommandeSentier contexte, CancellationToken ct)
    {
        var employe = contexte.Parametres.Lire<long>("employe");
        contexte.Variables.Ecrire(DemandeConge.Solde, metier.Soldes.GetValueOrDefault(employe));
        metier.Noter($"calculer-solde:{employe}");
        return Task.CompletedTask;
    }
}

public sealed class JournaliserDecision(ServicesMetier metier) : CommandeSentier<JournaliserDecision.Parametres>
{
    protected override Task ExecuterAsync(ContexteCommandeSentier contexte, Parametres parametres, CancellationToken ct)
    {
        metier.Noter($"decision:{contexte.CleAffaire}:{parametres.Decision}:{contexte.Utilisateur}");
        return Task.CompletedTask;
    }

    public sealed class Parametres
    {
        public string? Decision { get; set; }
    }
}
