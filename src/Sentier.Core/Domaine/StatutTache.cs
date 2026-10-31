namespace Sentier;

/// <summary>Statut d'une tâche utilisateur, tenu par le gestionnaire de tâches du client.</summary>
public enum StatutTache
{
    /// <summary>La tâche attend d'être complétée.</summary>
    Ouverte,

    /// <summary>La tâche a été complétée.</summary>
    Completee,

    /// <summary>La tâche a été annulée.</summary>
    Annulee,
}
