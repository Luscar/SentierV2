using Sentier.Definitions;
using Sentier.Variables;

namespace Sentier.Execution;

/// <summary>Résultat de l'exécution (ou de la reprise) d'un nœud.</summary>
public abstract record ResultatNoeud
{
    private protected ResultatNoeud()
    {
    }

    /// <summary>Le nœud est terminé : suivre la sortie nommée (ou la sortie par défaut).</summary>
    public static ResultatNoeud Continuer(string? nomSortie = null, string? resultat = null) => new ResultatContinuer(nomSortie, resultat);

    /// <summary>Le nœud suspend l'instance.</summary>
    public static ResultatNoeud Suspendre() => new ResultatSuspendre();

    /// <summary>Le nœud termine l'instance avec le code de fin donné.</summary>
    public static ResultatNoeud Terminer(string codeFin) => new ResultatTerminer(codeFin);

    /// <summary>Le nœud annule l'instance.</summary>
    public static ResultatNoeud Annuler(string motif) => new ResultatAnnuler(motif);
}

/// <summary>Le nœud est terminé et l'exécution continue.</summary>
public sealed record ResultatContinuer(string? NomSortie, string? Resultat) : ResultatNoeud;

/// <summary>Le nœud suspend l'instance.</summary>
public sealed record ResultatSuspendre : ResultatNoeud;

/// <summary>Le nœud termine l'instance.</summary>
public sealed record ResultatTerminer(string CodeFin) : ResultatNoeud;

/// <summary>Le nœud annule l'instance.</summary>
public sealed record ResultatAnnuler(string Motif) : ResultatNoeud;

/// <summary>Événement qui reprend une instance suspendue sur un nœud.</summary>
public abstract record EvenementReprise;

/// <summary>Un déclencheur d'attente (échéance ou signal) a gagné.</summary>
public sealed record RepriseDeclencheur(Attente Attente, IReadOnlyDictionary<string, object?>? ChargeUtile) : EvenementReprise;

/// <summary>Une tâche utilisateur a été complétée.</summary>
public sealed record RepriseTache(TacheUtilisateur Tache, IReadOnlyDictionary<string, object?>? Donnees, string? Utilisateur) : EvenementReprise;

/// <summary>Une instance enfant s'est terminée ou a été annulée.</summary>
public sealed record RepriseEnfant(InstanceProcessus Enfant, IVariablesLecture? VariablesEnfant) : EvenementReprise;

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

/// <summary>
/// Exécuteur d'un type de nœud. Le moteur choisit l'exécuteur selon <see cref="TypeNoeud"/> ;
/// ajouter un type de nœud revient à ajouter un exécuteur.
/// </summary>
public interface IExecuteurNoeud
{
    /// <summary>Type de nœud pris en charge.</summary>
    TypeNoeud Type { get; }

    /// <summary>Exécute le nœud à l'entrée de l'instance.</summary>
    Task<ResultatNoeud> EntrerAsync(ContexteNoeud contexte, CancellationToken ct);

    /// <summary>Reprend un nœud suspensif à la suite d'un événement.</summary>
    Task<ResultatNoeud> ReprendreAsync(ContexteNoeud contexte, EvenementReprise evenement, CancellationToken ct);
}

/// <summary>Vue en lecture seule des variables, transmise aux requêtes.</summary>
internal sealed class VariablesLectureSeule(IVariablesLecture source) : IVariablesLecture
{
    public IReadOnlyDictionary<string, object?> Valeurs => source.Valeurs;

    public bool Contient(string nom) => source.Contient(nom);

    public T Lire<T>(Variable<T> variable) => source.Lire(variable);

    public bool TryLire<T>(Variable<T> variable, out T valeur) => source.TryLire(variable, out valeur);

    public T LireOuDefaut<T>(Variable<T> variable, T defaut = default!) => source.LireOuDefaut(variable, defaut);

    public object? Lire(string nom) => source.Lire(nom);
}
