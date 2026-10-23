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

/// <summary>Type d'une variable de processus.</summary>
public enum TypeVariable
{
    /// <summary>Chaîne de caractères.</summary>
    Texte,

    /// <summary>Entier 64 bits (<see cref="long"/>).</summary>
    Entier,

    /// <summary>Nombre décimal (<see cref="decimal"/>).</summary>
    Decimal,

    /// <summary>Booléen.</summary>
    Booleen,

    /// <summary>Date et heure UTC (<see cref="DateTime"/>).</summary>
    Date,

    /// <summary>Objet JSON libre, sans garantie de type.</summary>
    Json,
}

/// <summary>Source de la valeur d'un paramètre de commande, de requête ou de mappage.</summary>
public enum SourceParametre
{
    /// <summary>Valeur fixe stockée dans la définition.</summary>
    Statique,

    /// <summary>Valeur d'une variable de l'instance, lue au moment de l'appel.</summary>
    Variable,

    /// <summary>Clé d'affaire de l'instance.</summary>
    CleAffaire,
}

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

/// <summary>Type d'un déclencheur d'attente.</summary>
public enum TypeDeclencheur
{
    /// <summary>Échéance (date).</summary>
    Echeance,

    /// <summary>Signal nommé.</summary>
    Signal,
}

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

/// <summary>Mode d'une migration d'instance.</summary>
public enum ModeMigration
{
    /// <summary>Migration automatique d'un changement compatible, au déploiement.</summary>
    Auto,

    /// <summary>Migration selon un plan écrit par le client.</summary>
    Plan,
}

/// <summary>Dialecte SQL de la base utilisée par Sentier.</summary>
public enum DialecteBase
{
    /// <summary>Oracle (production).</summary>
    Oracle,

    /// <summary>SQLite (tests).</summary>
    Sqlite,
}
