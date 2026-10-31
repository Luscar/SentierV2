namespace Sentier.Execution;

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
