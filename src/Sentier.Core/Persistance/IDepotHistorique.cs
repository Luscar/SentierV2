namespace Sentier.Persistance;

/// <summary>Dépôt de l'historique des passages.</summary>
public interface IDepotHistorique
{
    /// <summary>Réserve l'identifiant du prochain passage, avant l'exécution du nœud.</summary>
    Task<IdPassageNoeud> ReserverIdAsync(CancellationToken ct);

    /// <summary>Insère un passage ; son identifiant est attribué s'il n'a pas été réservé.</summary>
    Task InsererAsync(PassageNoeud passage, CancellationToken ct);

    /// <summary>Ferme les passages encore ouverts d'une instance.</summary>
    Task FermerPassagesOuvertsAsync(IdInstance idInstance, DateTime dateSortie, string? resultat, CancellationToken ct);

    /// <summary>Passages d'une instance, dans l'ordre.</summary>
    Task<IReadOnlyList<PassageNoeud>> ObtenirAsync(IdInstance idInstance, CancellationToken ct);
}
