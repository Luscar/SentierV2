using Sentier.Commandes;
using Sentier.Definitions;
using Sentier.Variables;

namespace Sentier.Integration.Tests.Processus;

/// <summary>Processus parent : démarre une vérification enfant, en mappant les variables à l'aller et au retour.</summary>
[ProcessusSentier(Cle, Nom = "Traitement d'une commande")]
public sealed class Commande : DefinitionSentier
{
    public const string Cle = "commande";

    public static readonly Variable<decimal> Montant = Variable.Decimal("montant").Obligatoire();
    public static readonly Variable<string> Resultat = Variable.Texte("resultat");

    protected override void Definir(ConstructeurProcessus processus) => processus
        .SousProcessus<VerificationCommande>("verifier", s => s
            .Entree(VerificationCommande.Montant, Montant)
            .Sortie(VerificationCommande.Resultat, Resultat)
            .SiFin("refusee", "refusee")
            .SiAnnule(b => b.Fin("annulee")))
        .Fin("acceptee")
        .Fin("refusee");
}

/// <summary>Processus enfant : validation automatique sous un seuil, sinon vérification manuelle.</summary>
[ProcessusSentier(Cle, Nom = "Vérification d'une commande")]
public sealed class VerificationCommande : DefinitionSentier
{
    public const string Cle = "verification-commande";
    public const decimal Seuil = 1000m;

    public static readonly Variable<decimal> Montant = Variable.Decimal("montant").Obligatoire();
    public static readonly Variable<string> Resultat = Variable.Texte("resultat");

    protected override void Definir(ConstructeurProcessus processus) => processus
        .Systemique<EvaluerMontant>("evaluer")
        .Decision("validation-automatique", d => d
            .Si(Resultat).EgalA("ok").Vers("validee")
            .Sinon())
        .Interactif("verification-manuelle", "Vérifier la commande", i => i.Role("controleur").Donnees(Montant))
        .Decision("verdict", d => d
            .Si(Resultat).EgalA("ok").Vers("validee")
            .Sinon("refusee"))
        .Fin("validee")
        .Fin("refusee");
}

public sealed class EvaluerMontant : ICommandeSentier
{
    public Task ExecuterAsync(ContexteCommandeSentier contexte, CancellationToken ct)
    {
        var montant = contexte.Variables.Lire(VerificationCommande.Montant);
        contexte.Variables.Ecrire(VerificationCommande.Resultat, montant <= VerificationCommande.Seuil ? "ok" : "a-verifier");
        return Task.CompletedTask;
    }
}
