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
