namespace Sentier;

/// <summary>Recherche d'instances d'un processus par la valeur de leurs variables.</summary>
public sealed record RechercheParVariables
{
    /// <summary>Clé du processus (toutes versions confondues).</summary>
    public required string CleProcessus { get; init; }

    /// <summary>Filtres, tous obligatoires (ET logique). Au moins un.</summary>
    public required IReadOnlyList<FiltreVariable> Filtres { get; init; }

    /// <summary>Statuts retenus ; tous si <c>null</c> ou vide.</summary>
    public IReadOnlyCollection<StatutInstance>? Statuts { get; init; }

    /// <summary>Nombre maximal d'instances retournées, des plus récentes aux plus anciennes.</summary>
    public int TailleMax { get; init; } = 100;
}
