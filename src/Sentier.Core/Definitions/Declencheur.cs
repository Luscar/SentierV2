using System.Text.Json.Serialization;

namespace Sentier.Definitions;

/// <summary>Déclencheur d'un nœud d'attente.</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(DeclencheurEcheance), "ECHEANCE")]
[JsonDerivedType(typeof(DeclencheurSignal), "SIGNAL")]
public abstract class Declencheur
{
    /// <summary>Nom de la sortie suivie si ce déclencheur gagne.</summary>
    public required string NomSortie { get; init; }

    /// <summary>Type du déclencheur.</summary>
    [JsonIgnore]
    public abstract TypeDeclencheur Type { get; }
}
