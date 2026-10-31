namespace Sentier;

/// <summary>Demande de création d'une tâche, transmise au <see cref="IGestionnaireTaches"/> du client.</summary>
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
