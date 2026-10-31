namespace Sentier.Persistance;

/// <summary>Dépôt des versions de définitions.</summary>
public interface IDepotDefinitions
{
    /// <summary>Dernière version déployée d'un processus (la plus haute), avec son contenu.</summary>
    Task<DefinitionStockee?> ObtenirDerniereAsync(string cleProcessus, CancellationToken ct);

    /// <summary>Identifiant de la version active d'un processus (requête légère indexée).</summary>
    Task<IdDefinition?> ObtenirIdActifAsync(string cleProcessus, CancellationToken ct);

    /// <summary>Version de définition par identifiant, avec son contenu.</summary>
    Task<DefinitionStockee?> ObtenirAsync(IdDefinition id, CancellationToken ct);

    /// <summary>Version de définition par numéro de version, avec son contenu.</summary>
    Task<DefinitionStockee?> ObtenirParVersionAsync(string cleProcessus, int version, CancellationToken ct);

    /// <summary>Insère une nouvelle version, qui devient la seule version active du processus.</summary>
    Task InsererAsync(DefinitionStockee definition, CancellationToken ct);
}
