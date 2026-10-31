using Sentier.Definitions;

namespace Exemples.DeclarationSousProcessus;

// Aucun processus ne déclare de variable : la clé d'affaire (numéro de la déclaration) suffit.
// Les commandes agissent sur la déclaration et les requêtes lisent son état chez l'application.

/// <summary>
/// Traitement d'une déclaration déjà reçue et enregistrée par l'application.
/// Le parent ne contient que des sous-processus, démarrés sur la même clé d'affaire.
/// </summary>
[ProcessusSentier("traitement-declaration", Nom = "Traitement d'une déclaration")]
public sealed class TraitementDeclaration : DefinitionSentier
{
    protected override void Definir(ConstructeurProcessus processus) => processus
        .SousProcessus<IdentificationDeclaration>("identification")
        .SousProcessus<ConformiteDeclaration>("conformite")
        .SousProcessus<VerificationDeclaration>("verification")
        .SousProcessus<ComptabilisationDeclaration>("comptabilisation")
        .Fin("traitee");
}
