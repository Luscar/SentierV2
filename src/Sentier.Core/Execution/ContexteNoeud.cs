using Sentier.Definitions;
using Sentier.Variables;

namespace Sentier.Execution;

/// <summary>Contexte d'exécution d'un nœud.</summary>
public sealed class ContexteNoeud
{
    internal ContexteNoeud(InstanceProcessus instance, DefinitionProcessus definition, Noeud noeud, VariablesProcessus variables, string? utilisateur, MoteurSentier moteur)
    {
        Instance = instance;
        Definition = definition;
        Noeud = noeud;
        Variables = variables;
        Utilisateur = utilisateur;
        Moteur = moteur;
    }

    /// <summary>Instance en cours.</summary>
    public InstanceProcessus Instance { get; }

    /// <summary>Définition suivie par l'instance.</summary>
    public DefinitionProcessus Definition { get; }

    /// <summary>Nœud exécuté.</summary>
    public Noeud Noeud { get; }

    /// <summary>Variables de l'instance.</summary>
    public VariablesProcessus Variables { get; }

    /// <summary>Utilisateur courant, si disponible.</summary>
    public string? Utilisateur { get; }

    internal MoteurSentier Moteur { get; }
}
