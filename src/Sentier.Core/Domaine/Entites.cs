namespace Sentier;

/// <summary>
/// Exécution d'une définition de processus, couplée à une clé d'affaire.
/// Seul le moteur modifie une instance ; le client la consulte.
/// </summary>
public sealed class InstanceProcessus
{
    /// <summary>Identifiant de l'instance.</summary>
    public IdInstance Id { get; internal set; }

    /// <summary>Version de définition suivie par l'instance.</summary>
    public IdDefinition IdDefinition { get; internal set; }

    /// <summary>Clé du processus (recopiée de la définition).</summary>
    public string CleProcessus { get; internal set; } = "";

    /// <summary>Instance parente, pour un sous-processus.</summary>
    public IdInstance? IdInstanceParent { get; internal set; }

    /// <summary>Code du nœud sous-processus du parent qui a démarré cette instance.</summary>
    public string? CodeNoeudParent { get; internal set; }

    /// <summary>Clé de l'objet métier sur lequel agit l'instance. Immuable.</summary>
    public long CleAffaire { get; internal set; }

    /// <summary>Indique si la règle « une seule instance active par clé d'affaire » s'applique.</summary>
    public bool InstanceUnique { get; internal set; }

    /// <summary>Statut courant.</summary>
    public StatutInstance Statut { get; internal set; }

    /// <summary>Code du nœud de fin atteint, une fois l'instance terminée.</summary>
    public string? CodeFin { get; internal set; }

    /// <summary>Code du nœud courant.</summary>
    public string CodeNoeudCourant { get; internal set; } = "";

    /// <summary>
    /// Identifiant (chez le client) de la tâche attendue sur le nœud interactif courant, ou <c>null</c>.
    /// C'est la référence de Sentier : seule cette tâche peut reprendre l'instance.
    /// </summary>
    public long? IdTacheCourante { get; internal set; }

    /// <summary>Variables sérialisées en JSON.</summary>
    internal string? VariablesJson { get; set; }

    /// <summary>Message de la dernière erreur, si l'instance est en erreur.</summary>
    public string? MessageErreur { get; internal set; }

    /// <summary>Date (UTC) de la dernière erreur.</summary>
    public DateTime? DateErreur { get; internal set; }

    /// <summary>Version pour le verrou optimiste.</summary>
    public int Version { get; internal set; }

    /// <summary>Date (UTC) de création.</summary>
    public DateTime DateCreation { get; internal set; }

    /// <summary>Date (UTC) de dernière mise à jour.</summary>
    public DateTime DateMaj { get; internal set; }

    /// <summary>Date (UTC) de fin (terminée ou annulée).</summary>
    public DateTime? DateFin { get; internal set; }

    /// <summary>Vrai si l'instance est active : en cours, suspendue ou en erreur.</summary>
    public bool EstActive => Statut is StatutInstance.EnCours or StatutInstance.Suspendue or StatutInstance.EnErreur;

    /// <summary>Vrai si l'instance est au repos (suspendue ou en erreur) et peut être reprise ou migrée.</summary>
    public bool EstAuRepos => Statut is StatutInstance.Suspendue or StatutInstance.EnErreur;
}

/// <summary>Entrée de l'historique : un nœud traversé par une instance.</summary>
public sealed class PassageNoeud
{
    /// <summary>Identifiant du passage.</summary>
    public IdPassageNoeud Id { get; internal set; }

    /// <summary>Instance concernée.</summary>
    public IdInstance IdInstance { get; internal set; }

    /// <summary>Code du nœud traversé.</summary>
    public string CodeNoeud { get; internal set; } = "";

    /// <summary>Type du nœud traversé.</summary>
    public TypeNoeud TypeNoeud { get; internal set; }

    /// <summary>Date (UTC) d'entrée dans le nœud.</summary>
    public DateTime DateEntree { get; internal set; }

    /// <summary>Date (UTC) de sortie, nulle tant que le nœud est suspendu.</summary>
    public DateTime? DateSortie { get; internal set; }

    /// <summary>Résultat du passage (sortie choisie, déclencheur gagnant, code de fin…).</summary>
    public string? Resultat { get; internal set; }
}

/// <summary>Déclencheur en cours (échéance ou signal) d'une instance suspendue sur un nœud d'attente.</summary>
public sealed class Attente
{
    /// <summary>Identifiant de l'attente.</summary>
    public IdAttente Id { get; internal set; }

    /// <summary>Instance en attente.</summary>
    public IdInstance IdInstance { get; internal set; }

    /// <summary>Code du nœud d'attente.</summary>
    public string CodeNoeud { get; internal set; } = "";

    /// <summary>Type du déclencheur.</summary>
    public TypeDeclencheur TypeDeclencheur { get; internal set; }

    /// <summary>Nom de la sortie suivie si ce déclencheur gagne.</summary>
    public string NomSortie { get; internal set; } = "";

    /// <summary>Date (UTC) d'échéance, pour un déclencheur d'échéance.</summary>
    public DateTime? DateEcheance { get; internal set; }

    /// <summary>Nom du signal attendu, pour un déclencheur de signal.</summary>
    public string? NomSignal { get; internal set; }

    /// <summary>Clé de corrélation du signal, optionnelle.</summary>
    public string? CleCorrelation { get; internal set; }

    /// <summary>Date (UTC) de création.</summary>
    public DateTime DateCreation { get; internal set; }
}

/// <summary>Trace d'une migration d'instance vers une autre version.</summary>
public sealed class TraceMigration
{
    /// <summary>Identifiant de la trace.</summary>
    public IdMigration Id { get; internal set; }

    /// <summary>Instance migrée.</summary>
    public IdInstance IdInstance { get; internal set; }

    /// <summary>Version de définition d'origine.</summary>
    public IdDefinition IdDefinitionSource { get; internal set; }

    /// <summary>Version de définition cible.</summary>
    public IdDefinition IdDefinitionCible { get; internal set; }

    /// <summary>Mode de migration.</summary>
    public ModeMigration Mode { get; internal set; }

    /// <summary>Détail JSON des actions effectuées.</summary>
    public string? Detail { get; internal set; }

    /// <summary>Utilisateur à l'origine de la migration.</summary>
    public string? Utilisateur { get; internal set; }

    /// <summary>Date (UTC) de la migration.</summary>
    public DateTime DateMigration { get; internal set; }
}

/// <summary>Version de définition telle qu'enregistrée en base.</summary>
public sealed class DefinitionStockee
{
    /// <summary>Identifiant de la version.</summary>
    public IdDefinition Id { get; internal set; }

    /// <summary>Clé du processus.</summary>
    public string CleProcessus { get; internal set; } = "";

    /// <summary>Numéro de version (1, 2, …).</summary>
    public int Version { get; internal set; }

    /// <summary>Nom lisible du processus.</summary>
    public string Nom { get; internal set; } = "";

    /// <summary>Une seule instance active par clé d'affaire.</summary>
    public bool InstanceUnique { get; internal set; }

    /// <summary>Migration automatique des changements compatibles.</summary>
    public bool MigrationAuto { get; internal set; }

    /// <summary>Contenu JSON canonique de la définition.</summary>
    public string Contenu { get; internal set; } = "";

    /// <summary>Empreinte SHA-256 du contenu.</summary>
    public string Empreinte { get; internal set; } = "";

    /// <summary>Vrai pour la dernière version active.</summary>
    public bool EstActive { get; internal set; }

    /// <summary>Date (UTC) de déploiement.</summary>
    public DateTime DateCreation { get; internal set; }
}
