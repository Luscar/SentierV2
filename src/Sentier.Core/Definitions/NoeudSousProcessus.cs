using System.Text.Json.Serialization;

namespace Sentier.Definitions;

/// <summary>Démarre une instance enfant dans la même transaction.</summary>
public sealed class NoeudSousProcessus : Noeud
{
    /// <summary>Nom de la sortie suivie lorsque l'enfant est annulé.</summary>
    public const string SortieAnnule = "annule";

    /// <inheritdoc />
    [JsonIgnore]
    public override TypeNoeud Type => TypeNoeud.SousProcessus;

    /// <inheritdoc />
    [JsonIgnore]
    public override bool EstSuspensif => true;

    /// <summary>Clé du processus enfant (sa dernière version active est utilisée).</summary>
    public required string CleProcessus { get; init; }

    /// <summary>Variable entière du parent donnant la clé d'affaire de l'enfant ; par défaut, celle du parent.</summary>
    public string? VariableCleAffaire { get; init; }

    /// <summary>Mappage d'entrée : chaque paramètre remplit la variable de l'enfant du même nom.</summary>
    public IReadOnlyList<Parametre> Entrees { get; init; } = [];

    /// <summary>Mappage de sortie, appliqué à la fin normale de l'enfant.</summary>
    public IReadOnlyList<MappageSortie> Sorties { get; init; } = [];
}
