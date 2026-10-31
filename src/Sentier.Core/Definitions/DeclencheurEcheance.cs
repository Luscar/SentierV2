using System.Text.Json.Serialization;

namespace Sentier.Definitions;

/// <summary>Échéance : date absolue, durée relative à l'entrée dans le nœud, ou date lue dans une variable.</summary>
public sealed class DeclencheurEcheance : Declencheur
{
    /// <summary>Nom de sortie par défaut d'une échéance.</summary>
    public const string SortieParDefaut = "echeance";

    /// <inheritdoc />
    [JsonIgnore]
    public override TypeDeclencheur Type => TypeDeclencheur.Echeance;

    /// <summary>Date absolue (UTC).</summary>
    public DateTime? DateAbsolue { get; init; }

    /// <summary>Durée relative à l'entrée dans le nœud.</summary>
    public TimeSpan? Duree { get; init; }

    /// <summary>Variable (date) donnant l'échéance.</summary>
    public string? Variable { get; init; }
}
