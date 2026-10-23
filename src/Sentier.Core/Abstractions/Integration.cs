namespace Sentier;

/// <summary>Heure courante, injectable. Toutes les dates de Sentier sont en UTC.</summary>
public interface IHorloge
{
    /// <summary>Date et heure courantes, en UTC.</summary>
    DateTime MaintenantUtc { get; }
}

/// <summary>Horloge système par défaut.</summary>
public sealed class HorlogeSysteme : IHorloge
{
    /// <inheritdoc />
    public DateTime MaintenantUtc => DateTime.UtcNow;
}

/// <summary>Optionnel : fournit l'utilisateur courant, pour l'audit.</summary>
public interface IContexteUtilisateur
{
    /// <summary>Identifiant de l'utilisateur courant, ou <c>null</c>.</summary>
    string? Utilisateur { get; }
}

/// <summary>
/// Bassin de tâches existant chez le client, auquel Sentier délègue les tâches utilisateurs.
/// S'il est en base, il doit utiliser la même session (donc la même transaction) que Sentier.
/// </summary>
public interface IBassinTaches
{
    /// <summary>Crée une tâche et retourne son identifiant externe.</summary>
    Task<long> CreerTacheAsync(DemandeTacheUtilisateur demande, CancellationToken ct = default);

    /// <summary>Annule une tâche à partir de son identifiant externe.</summary>
    Task AnnulerTacheAsync(long idTacheExterne, string motif, CancellationToken ct = default);
}

/// <summary>Demande de création d'une tâche dans le bassin du client.</summary>
public sealed class DemandeTacheUtilisateur
{
    /// <summary>Instance à l'origine de la tâche.</summary>
    public required IdInstance IdInstance { get; init; }

    /// <summary>Clé du processus.</summary>
    public required string CleProcessus { get; init; }

    /// <summary>Clé d'affaire de l'instance.</summary>
    public required long CleAffaire { get; init; }

    /// <summary>Code du nœud interactif.</summary>
    public required string CodeNoeud { get; init; }

    /// <summary>Nom de la tâche.</summary>
    public required string Nom { get; init; }

    /// <summary>Rôle assigné, optionnel.</summary>
    public string? Role { get; init; }

    /// <summary>Utilisateur assigné, optionnel.</summary>
    public string? Utilisateur { get; init; }

    /// <summary>Date (UTC) d'échéance indicative.</summary>
    public DateTime? DateEcheance { get; init; }

    /// <summary>Données transmises à la tâche (paramètres résolus du nœud).</summary>
    public IReadOnlyDictionary<string, object?> Donnees { get; init; } = new Dictionary<string, object?>();
}
