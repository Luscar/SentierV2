using Sentier.Definitions;

namespace Exemples.DeclarationSousProcessus;

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
