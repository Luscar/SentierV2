using Sentier.Commandes;
using Sentier.Definitions;
using Sentier.Variables;

namespace Sentier.Core.Tests;

// Commandes, requêtes et définitions minimales pour construire des processus : elles ne sont jamais exécutées.

public sealed class CommandeFictive : ICommandeSentier
{
    public Task ExecuterAsync(ContexteCommandeSentier contexte, CancellationToken ct) => Task.CompletedTask;
}

public sealed class AutreCommandeFictive : ICommandeSentier
{
    public Task ExecuterAsync(ContexteCommandeSentier contexte, CancellationToken ct) => Task.CompletedTask;
}

public sealed class RequeteFictive : IRequeteSentier
{
    public Task<bool> EvaluerAsync(ContexteRequeteSentier contexte, CancellationToken ct) => Task.FromResult(true);
}

[ProcessusSentier("enfant-fictif", Nom = "Enfant fictif")]
public sealed class EnfantFictif : DefinitionSentier
{
    public static readonly Variable<decimal> Montant = Variable.Decimal("montant");

    protected override void Definir(ConstructeurProcessus processus) => processus
        .Systemique<CommandeFictive>("traiter")
        .Fin("termine");
}

[ProcessusSentier("demande-fictive", Nom = "Demande fictive")]
public sealed class DemandeFictive : DefinitionSentier
{
    public static readonly Variable<long> NbJours = Variable.Entier("nbJours").Obligatoire();
    public static readonly Variable<long> Solde = Variable.Entier("solde").ParDefaut(0);
    public static readonly Variable<decimal> Montant = Variable.Decimal("montant");

    protected override void Definir(ConstructeurProcessus processus) => processus
        .Systemique<CommandeFictive>("calculer-solde", p => p.CleAffaire("employe"))
        .Decision("solde-suffisant", d => d
            .Si(NbJours).SuperieurA(Solde).Nommee("insuffisant").Alors(b => b.Fin("refusee"))
            .Sinon())
        .Interactif("approbation", "Approuver la demande", i => i.Role("gestionnaire").Donnees(NbJours))
        .SousProcessus<EnfantFictif>("enfant", s => s.Entree(EnfantFictif.Montant, Montant))
        .AttendreDelai("pause", TimeSpan.FromHours(1))
        .Fin("approuvee");
}

/// <summary>Processus sans attribut : sa clé est le nom de la classe.</summary>
public sealed class ProcessusSansAttribut : DefinitionSentier
{
    protected override void Definir(ConstructeurProcessus processus) => processus.Fin("fin");
}
