namespace Sentier;

/// <summary>Résultat d'une action de <see cref="IServiceSentier"/>.</summary>
public sealed record ResultatExecution
{
    /// <summary>Instance concernée, si elle existe.</summary>
    public IdInstance? IdInstance { get; init; }

    /// <summary>Statut de l'instance après l'action.</summary>
    public StatutInstance? Statut { get; init; }

    /// <summary>Code du nœud courant après l'action.</summary>
    public string? CodeNoeudCourant { get; init; }

    /// <summary>Code de fin, si l'instance est terminée.</summary>
    public string? CodeFin { get; init; }

    /// <summary>
    /// Tâche attendue sur le nœud interactif courant (identifiant chez le client), ou <c>null</c>.
    /// Une tâche attendue par un sous-processus est portée par l'instance enfant.
    /// </summary>
    public long? IdTacheCourante { get; init; }

    /// <summary>Vrai s'il ne s'est rien passé (action idempotente).</summary>
    public bool SansEffet { get; init; }

    /// <summary>Raison de l'absence d'effet.</summary>
    public string? Raison { get; init; }

    /// <summary>Résultat d'une action effectuée.</summary>
    public static ResultatExecution Depuis(InstanceProcessus instance) => new()
    {
        IdInstance = instance.Id,
        Statut = instance.Statut,
        CodeNoeudCourant = instance.CodeNoeudCourant,
        CodeFin = instance.CodeFin,
        IdTacheCourante = instance.IdTacheCourante,
    };

    /// <summary>Résultat d'une action sans effet.</summary>
    public static ResultatExecution Aucun(string raison, InstanceProcessus? instance = null) => new()
    {
        IdInstance = instance?.Id,
        Statut = instance?.Statut,
        CodeNoeudCourant = instance?.CodeNoeudCourant,
        CodeFin = instance?.CodeFin,
        IdTacheCourante = instance?.IdTacheCourante,
        SansEffet = true,
        Raison = raison,
    };
}
