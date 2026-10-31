namespace Sentier.Persistance;

/// <summary>Dépôt des attentes (une ligne par déclencheur).</summary>
public interface IDepotAttentes
{
    /// <summary>Insère une attente.</summary>
    Task InsererAsync(Attente attente, CancellationToken ct);

    /// <summary>Attentes d'une instance.</summary>
    Task<IReadOnlyList<Attente>> ObtenirParInstanceAsync(IdInstance idInstance, CancellationToken ct);

    /// <summary>
    /// Lit et verrouille une attente sans attendre (<c>FOR UPDATE SKIP LOCKED</c> sous Oracle).
    /// Retourne <c>null</c> si elle n'existe plus ou si un autre traitement la verrouille.
    /// </summary>
    Task<Attente?> VerrouillerAsync(IdAttente id, CancellationToken ct);

    /// <summary>Supprime toutes les attentes d'un nœud d'une instance ; retourne le nombre de lignes supprimées.</summary>
    Task<int> SupprimerParNoeudAsync(IdInstance idInstance, string codeNoeud, CancellationToken ct);

    /// <summary>Supprime toutes les attentes d'une instance.</summary>
    Task<int> SupprimerParInstanceAsync(IdInstance idInstance, CancellationToken ct);

    /// <summary>Échéances atteintes des instances suspendues, sans verrou.</summary>
    Task<IReadOnlyList<EcheanceDue>> ObtenirEcheancesDuesAsync(DateTime maintenant, int tailleMax, CancellationToken ct);

    /// <summary>Attentes de signal correspondant au signal envoyé, pour des instances au repos.</summary>
    Task<IReadOnlyList<Attente>> RechercherSignalAsync(Signal signal, CancellationToken ct);

    /// <summary>Renomme le nœud des attentes d'une instance (migration).</summary>
    Task RenommerNoeudAsync(IdInstance idInstance, string ancienCode, string nouveauCode, CancellationToken ct);
}
