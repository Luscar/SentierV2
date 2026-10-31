namespace Sentier.Definitions;

/// <summary>Constructeur d'un bloc imbriqué (branche d'un nœud à plusieurs sorties).</summary>
public sealed class ConstructeurBloc : ConstructeurSequence<ConstructeurBloc>
{
    internal ConstructeurBloc(EtatConstruction etat, SequenceEnConstruction sequence) : base(etat, sequence)
    {
    }
}
