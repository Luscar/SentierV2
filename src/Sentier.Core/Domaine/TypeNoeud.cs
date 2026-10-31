namespace Sentier;

/// <summary>Type d'un nœud de définition.</summary>
public enum TypeNoeud
{
    /// <summary>Point d'entrée unique du processus.</summary>
    Debut,

    /// <summary>Exécute une commande.</summary>
    Systemique,

    /// <summary>Attend une échéance et/ou un ou plusieurs signaux.</summary>
    Attente,

    /// <summary>Crée une tâche par le gestionnaire de tâches du client.</summary>
    Interactif,

    /// <summary>Passerelle exclusive évaluant des requêtes.</summary>
    Decision,

    /// <summary>Démarre une instance enfant.</summary>
    SousProcessus,

    /// <summary>Termine l'instance.</summary>
    Fin,
}
