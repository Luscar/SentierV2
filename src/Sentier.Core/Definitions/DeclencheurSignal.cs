using System.Text.Json.Serialization;

namespace Sentier.Definitions;

/// <summary>Signal nommé, avec clé de corrélation optionnelle (fixe ou tirée d'une variable).</summary>
public sealed class DeclencheurSignal : Declencheur
{
    /// <inheritdoc />
    [JsonIgnore]
    public override TypeDeclencheur Type => TypeDeclencheur.Signal;

    /// <summary>Nom du signal attendu.</summary>
    public required string NomSignal { get; init; }

    /// <summary>Clé de corrélation fixe.</summary>
    public string? CleCorrelation { get; init; }

    /// <summary>Variable donnant la clé de corrélation.</summary>
    public string? VariableCorrelation { get; init; }

    /// <summary>Fusionne la charge utile du signal dans les variables (vrai par défaut).</summary>
    public bool FusionnerChargeUtile { get; init; } = true;
}
