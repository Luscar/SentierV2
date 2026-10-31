namespace Sentier.Migration;

/// <summary>Traitement des tâches ouvertes d'une instance migrée.</summary>
public enum ActionTaches
{
    /// <summary>Conserver les tâches ouvertes (le nœud interactif existe toujours).</summary>
    Conserver,

    /// <summary>Annuler les tâches dans le bassin du client ; l'instance reprend au nœud de suite.</summary>
    Annuler,
}
