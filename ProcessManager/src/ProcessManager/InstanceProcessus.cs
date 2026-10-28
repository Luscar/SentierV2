namespace ProcessManager;

public enum StatutProcessus
{
    EnCours,
    EnAttente,
    AttenteEnfant,
    Termine,
}

/// <summary>Seule donnée persistée d'un processus.</summary>
public sealed record InstanceProcessus
{
    public required Guid Id { get; init; }

    /// <summary>Nom du type du processus, au format « Namespace.Type, Assembly ».</summary>
    public required string TypeProcessus { get; init; }

    public string? Etape { get; set; }
    public StatutProcessus Statut { get; set; }
    public DateTimeOffset? Echeance { get; set; }
    public Guid? ParentId { get; init; }
    public string? CleCorrelation { get; set; }

    /// <summary>État du processus sérialisé en JSON.</summary>
    public string? Etat { get; set; }

    /// <summary>Version pour la concurrence optimiste (0 = jamais sauvegardée).</summary>
    public int Version { get; set; }
}
