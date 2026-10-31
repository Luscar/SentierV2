using Sentier.Definitions;

namespace Exemples.DeclarationSousProcessus;

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
