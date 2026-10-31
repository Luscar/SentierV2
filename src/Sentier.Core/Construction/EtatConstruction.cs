namespace Sentier.Definitions;

/// <summary>État partagé pendant la construction d'une définition.</summary>
internal sealed class EtatConstruction
{
    internal List<string> Erreurs { get; } = [];

    internal HashSet<string> Codes { get; } = new(StringComparer.Ordinal);

    internal List<ElementSequence> TousLesElements { get; } = [];

    internal SequenceEnConstruction Principale { get; } = new();
}
