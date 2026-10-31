namespace Sentier.Persistance;

/// <summary>Dépôt de l'historique des passages.</summary>
public interface IDepotHistorique
{
    /// <summary>Insère un passage.</summary>
    Task InsererAsync(PassageNoeud passage, CancellationToken ct);

    /// <summary>Ferme les passages encore ouverts d'une instance.</summary>
    Task FermerPassagesOuvertsAsync(IdInstance idInstance, DateTime dateSortie, string? resultat, CancellationToken ct);

    /// <summary>Passages d'une instance, dans l'ordre.</summary>
    Task<IReadOnlyList<PassageNoeud>> ObtenirAsync(IdInstance idInstance, CancellationToken ct);
}
