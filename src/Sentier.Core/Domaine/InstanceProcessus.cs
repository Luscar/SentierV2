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
