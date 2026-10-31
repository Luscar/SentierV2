using Sentier.Variables;

namespace Sentier.Commandes;

/// <summary>Contexte reçu par une commande.</summary>
public sealed class ContexteCommande
{
    internal ContexteCommande(
        InstanceProcessus instance,
        string codeNoeud,
        MomentCommande moment,
        ParametresResolus parametres,
        VariablesProcessus variables,
        IReadOnlyDictionary<string, object?>? donneesTache,
        string? utilisateur)
    {
        Instance = instance;
        CodeNoeud = codeNoeud;
        Moment = moment;
        Parametres = parametres;
        Variables = variables;
        DonneesTache = donneesTache;
        Utilisateur = utilisateur;
    }

    /// <summary>Instance en cours (lecture seule).</summary>
    public InstanceProcessus Instance { get; }

    /// <summary>Identifiant de l'instance.</summary>
    public IdInstance IdInstance => Instance.Id;

    /// <summary>Clé du processus de l'instance.</summary>
    public string CleProcessus => Instance.CleProcessus;

    /// <summary>Clé d'affaire de l'instance.</summary>
    public long CleAffaire => Instance.CleAffaire;

    /// <summary>Code du nœud qui exécute la commande.</summary>
    public string CodeNoeud { get; }

    /// <summary>Moment d'exécution.</summary>
    public MomentCommande Moment { get; }

    /// <summary>Paramètres déjà résolus.</summary>
    public ParametresResolus Parametres { get; }

    /// <summary>Variables de l'instance, en lecture et en écriture.</summary>
    public VariablesProcessus Variables { get; }

    /// <summary>Données de la tâche complétée (sortie d'un nœud interactif), sinon <c>null</c>.</summary>
    public IReadOnlyDictionary<string, object?>? DonneesTache { get; }

    /// <summary>Utilisateur courant, si disponible.</summary>
    public string? Utilisateur { get; }
}
