namespace Sentier;

/// <summary>État consultable d'une instance.</summary>
public sealed record EtatInstance
{
    /// <summary>Identifiant.</summary>
    public required IdInstance Id { get; init; }

    /// <summary>Clé du processus.</summary>
    public required string CleProcessus { get; init; }

    /// <summary>Version de définition suivie.</summary>
    public required IdDefinition IdDefinition { get; init; }

    /// <summary>Numéro de version de la définition.</summary>
    public required int VersionDefinition { get; init; }

    /// <summary>Clé d'affaire.</summary>
    public required long CleAffaire { get; init; }

    /// <summary>Statut.</summary>
    public required StatutInstance Statut { get; init; }

    /// <summary>Nœud courant.</summary>
    public required string CodeNoeudCourant { get; init; }

    /// <summary>Tâche attendue sur le nœud interactif courant (identifiant chez le client), ou <c>null</c>.</summary>
    public long? IdTacheCourante { get; init; }

    /// <summary>Code de fin.</summary>
    public string? CodeFin { get; init; }

    /// <summary>Instance parente.</summary>
    public IdInstance? IdInstanceParent { get; init; }

    /// <summary>Variables, relues avec leur type déclaré.</summary>
    public IReadOnlyDictionary<string, object?> Variables { get; init; } = new Dictionary<string, object?>();

    /// <summary>Date de création (UTC).</summary>
    public DateTime DateCreation { get; init; }

    /// <summary>Date de dernière mise à jour (UTC).</summary>
    public DateTime DateMaj { get; init; }

    /// <summary>Date de fin (UTC).</summary>
    public DateTime? DateFin { get; init; }

    /// <summary>Message d'erreur.</summary>
    public string? MessageErreur { get; init; }

    /// <summary>Date de l'erreur (UTC).</summary>
    public DateTime? DateErreur { get; init; }
}
