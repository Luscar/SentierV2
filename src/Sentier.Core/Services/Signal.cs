namespace Sentier;

/// <summary>
/// Signal nommé envoyé aux instances qui l'attendent. Il cible un processus et une clé d'affaire,
/// une clé de corrélation, ou les deux. Une clé d'affaire n'est jamais utilisée seule.
/// </summary>
public sealed class Signal
{
    /// <summary>Nom du signal.</summary>
    public required string Nom { get; init; }

    /// <summary>Processus visé (obligatoire avec une clé d'affaire).</summary>
    public string? CleProcessus { get; init; }

    /// <summary>Clé d'affaire visée.</summary>
    public long? CleAffaire { get; init; }

    /// <summary>Clé de corrélation visée.</summary>
    public string? CleCorrelation { get; init; }

    /// <summary>Charge utile, fusionnée dans les variables déclarées.</summary>
    public IReadOnlyDictionary<string, object?>? ChargeUtile { get; init; }
}
