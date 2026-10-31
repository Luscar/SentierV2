using Sentier.Variables;

namespace Sentier.Definitions;

/// <summary>Mappage de sortie d'un sous-processus : variable de l'enfant recopiée dans une variable du parent.</summary>
public sealed class MappageSortie
{
    /// <summary>Variable de l'enfant.</summary>
    public required string VariableEnfant { get; init; }

    /// <summary>Variable du parent.</summary>
    public required string VariableParent { get; init; }
}
