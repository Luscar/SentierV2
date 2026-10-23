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

/// <summary>Identification du déclarant ; un rejet part en correction, puis l'identification est reprise.</summary>
[ProcessusSentier("identification-declaration", Nom = "Identification d'une déclaration")]
public sealed class IdentificationDeclaration : DefinitionSentier
{
    private const string Identifier = "identifier-declaration";

    protected override void Definir(ConstructeurProcessus processus) => processus
        .Systemique<IdentifierDeclaration>(Identifier)
        .Decision("est-identifiee", d => d
            .Si<EstIdentifiee>().Nommee("identifiee")
            .Sinon(b => b
                .Interactif("corriger-rejet-identification", "Corriger le rejet d'identification", i => i
                    .Role("agent-identification")
                    .Echeance(TimeSpan.FromDays(5)))
                .Vers(Identifier)))
        .Fin("identifiee");
}

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

/// <summary>Vérification : établit les critères de sélection, puis attend le retour du système de vérification.</summary>
[ProcessusSentier("verification-declaration", Nom = "Vérification d'une déclaration")]
public sealed class VerificationDeclaration : DefinitionSentier
{
    /// <summary>Signal envoyé pour la clé d'affaire quand le système de vérification a répondu.</summary>
    public const string SignalRetourVerification = "RETOUR_VERIFICATION";

    protected override void Definir(ConstructeurProcessus processus) => processus
        .Systemique<EtablirCriteresSelection>("etablir-criteres-selection")
        .Attente("attendre-retour-verification", a => a.Signal(SignalRetourVerification, s => s.SansFusion()))
        .Fin("verifiee");
}

/// <summary>
/// Comptabilisation : délai avant le traitement comptable, comptabilisation, attente du retour comptable ;
/// un rejet part en correction, puis le sous-processus se termine.
/// </summary>
[ProcessusSentier("comptabilisation-declaration", Nom = "Comptabilisation d'une déclaration")]
public sealed class ComptabilisationDeclaration : DefinitionSentier
{
    /// <summary>Signal envoyé pour la clé d'affaire quand le système comptable a répondu.</summary>
    public const string SignalRetourComptable = "RETOUR_COMPTABLE";

    /// <summary>Délai entre l'entrée dans la comptabilisation et le traitement comptable.</summary>
    public static readonly TimeSpan DelaiComptabilisation = TimeSpan.FromDays(1);

    protected override void Definir(ConstructeurProcessus processus) => processus
        .Attente("attendre-comptabilisation", a => a.Echeance(DelaiComptabilisation))
        .Systemique<Comptabiliser>("comptabiliser")
        .Attente("attendre-retour-comptable", a => a.Signal(SignalRetourComptable, s => s.SansFusion()))
        .Decision("est-rejetee", d => d
            .Si<EstRejeteeComptabilite>().Nommee("rejetee").Alors(b => b
                .Interactif("corriger-rejet-comptable", "Corriger le rejet comptable", i => i
                    .Role("agent-comptable")
                    .Echeance(TimeSpan.FromDays(3))))
            .Sinon())
        .Fin("comptabilisee");
}
