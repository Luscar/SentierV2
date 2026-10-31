using Sentier.Definitions;

namespace Exemples.DeclarationSousProcessus;

/// <summary>
/// Conformité : comme l'identification, mais la correction doit être autorisée avant la revalidation.
/// Une correction refusée retourne en correction.
/// </summary>
[ProcessusSentier("conformite-declaration", Nom = "Conformité d'une déclaration")]
public sealed class ConformiteDeclaration : DefinitionSentier
{
    private const string Valider = "valider-conformite";
    private const string Corriger = "corriger-rejet-conformite";

    protected override void Definir(ConstructeurProcessus processus) => processus
        .Systemique<ValiderConformite>(Valider)
        .Decision("est-conforme", d => d
            .Si<EstConforme>().Nommee("conforme")
            .Sinon(Corriger))
        .Fin("conforme")

        .Interactif(Corriger, "Corriger le rejet de conformité", i => i
            .Role("agent-conformite")
            .Echeance(TimeSpan.FromDays(5)))
        .Interactif("autoriser-correction-conformite", "Autoriser la correction du rejet de conformité", i => i
            .Role("chef-equipe-conformite")
            .Echeance(TimeSpan.FromDays(2)))
        .Decision("correction-autorisee", d => d
            .Si<CorrectionConformiteAutorisee>().Nommee("autorisee").Vers(Valider)
            .Sinon(Corriger));
}
