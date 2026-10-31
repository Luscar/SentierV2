using Sentier.Variables;

namespace Sentier.Commandes;

/// <summary>Contexte reçu par une requête : tout est en lecture seule.</summary>
public sealed class ContexteRequete
{
    internal ContexteRequete(InstanceProcessus instance, string codeNoeud, ParametresResolus parametres, IVariablesLecture variables)
    {
        Instance = instance;
        CodeNoeud = codeNoeud;
        Parametres = parametres;
        Variables = variables;
    }

    /// <summary>Instance en cours (lecture seule).</summary>
    public InstanceProcessus Instance { get; }

    /// <summary>Identifiant de l'instance.</summary>
    public IdInstance IdInstance => Instance.Id;

    /// <summary>Clé du processus de l'instance.</summary>
    public string CleProcessus => Instance.CleProcessus;

    /// <summary>Clé d'affaire de l'instance.</summary>
    public long CleAffaire => Instance.CleAffaire;

    /// <summary>Code du nœud de décision.</summary>
    public string CodeNoeud { get; }

    /// <summary>Paramètres déjà résolus.</summary>
    public ParametresResolus Parametres { get; }

    /// <summary>Variables de l'instance, en lecture seule.</summary>
    public IVariablesLecture Variables { get; }
}
