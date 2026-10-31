namespace Sentier.Definitions;

/// <summary>Séquence de nœuds consécutifs (processus principal ou bloc imbriqué).</summary>
internal sealed class SequenceEnConstruction
{
    internal List<ElementSequence> Elements { get; } = [];

    internal string? LienDirect { get; set; }
}
