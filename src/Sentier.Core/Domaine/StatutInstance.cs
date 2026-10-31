namespace Sentier;

/// <summary>Statut d'une instance de processus.</summary>
public enum StatutInstance
{
    /// <summary>L'instance est en cours d'exécution (état transitoire dans une transaction).</summary>
    EnCours,

    /// <summary>L'instance attend une échéance, un signal, une tâche ou un sous-processus.</summary>
    Suspendue,

    /// <summary>L'instance a atteint un nœud de fin.</summary>
    Terminee,

    /// <summary>L'instance a été annulée.</summary>
    Annulee,

    /// <summary>L'instance a été marquée en erreur par <c>MarquerEnErreurAsync</c>.</summary>
    EnErreur,
}
