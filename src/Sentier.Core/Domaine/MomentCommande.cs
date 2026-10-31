namespace Sentier;

/// <summary>Moment où une commande est exécutée.</summary>
public enum MomentCommande
{
    /// <summary>Exécution d'un nœud systémique.</summary>
    Execution,

    /// <summary>Entrée d'un nœud (attente ou interactif).</summary>
    Entree,

    /// <summary>Sortie d'un nœud interactif.</summary>
    Sortie,

    /// <summary>Calcul de variables pendant une migration par plan.</summary>
    Migration,
}
