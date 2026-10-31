namespace Sentier.Persistance;

/// <summary>Dépôt des instances de processus.</summary>
public interface IDepotInstances
{
    /// <summary>Instance par identifiant.</summary>
    Task<InstanceProcessus?> ObtenirAsync(IdInstance id, CancellationToken ct);

    /// <summary>Insère une instance ; l'identifiant est obtenu avant l'insertion.</summary>
    Task InsererAsync(InstanceProcessus instance, CancellationToken ct);

    /// <summary>
    /// Met à jour une instance avec verrou optimiste sur <see cref="InstanceProcessus.Version"/>,
    /// puis incrémente la version. Lève <see cref="ConflitConcurrenceException"/> en cas de conflit.
    /// </summary>
    Task MettreAJourAsync(InstanceProcessus instance, CancellationToken ct);

    /// <summary>Instances actives d'un processus pour une clé d'affaire.</summary>
    Task<IReadOnlyList<InstanceProcessus>> ObtenirActivesAsync(string cleProcessus, long cleAffaire, CancellationToken ct);

    /// <summary>Instances d'une clé d'affaire pour une ou plusieurs clés de processus.</summary>
    Task<IReadOnlyList<InstanceProcessus>> RechercherAsync(long cleAffaire, IReadOnlyCollection<string> clesProcessus, IReadOnlyCollection<StatutInstance>? statuts, CancellationToken ct);

    /// <summary>
    /// Lot d'instances d'un processus, des plus récentes aux plus anciennes, pour une recherche par variables.
    /// <paramref name="motifsVariables"/> sont des motifs <c>LIKE</c> (échappement <c>\</c>) que le contenu
    /// des variables doit tous respecter : un préfiltre sans faux négatif, le filtre exact est appliqué ensuite.
    /// </summary>
    Task<IReadOnlyList<InstanceProcessus>> ObtenirLotParProcessusAsync(
        string cleProcessus,
        IReadOnlyCollection<StatutInstance>? statuts,
        IReadOnlyList<string> motifsVariables,
        IdInstance? avant,
        int taille,
        CancellationToken ct);

    /// <summary>Enfants actifs d'une instance.</summary>
    Task<IReadOnlyList<InstanceProcessus>> ObtenirEnfantsActifsAsync(IdInstance idParent, CancellationToken ct);

    /// <summary>Instances en erreur, paginées.</summary>
    Task<IReadOnlyList<InstanceProcessus>> ObtenirEnErreurAsync(int decalage, int taille, CancellationToken ct);

    /// <summary>Nombre d'instances actives d'un processus, par version et par nœud.</summary>
    Task<IReadOnlyList<CompteInstancesActives>> CompterActivesAsync(string cleProcessus, CancellationToken ct);

    /// <summary>Instances au repos (suspendues ou en erreur) d'une version, éventuellement filtrées par nœud.</summary>
    Task<IReadOnlyList<InstanceProcessus>> ObtenirAuReposAsync(IdDefinition idDefinition, string? codeNoeud, int taille, CancellationToken ct);

    /// <summary>Renomme le nœud parent des enfants actifs d'une instance (migration).</summary>
    Task RenommerNoeudParentAsync(IdInstance idParent, string ancienCode, string nouveauCode, CancellationToken ct);
}
