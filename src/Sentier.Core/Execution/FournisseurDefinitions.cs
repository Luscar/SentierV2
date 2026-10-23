using System.Collections.Concurrent;
using Sentier.Definitions;
using Sentier.Persistance;

namespace Sentier.Execution;

/// <summary>
/// Cache mémoire des versions de définitions, par identifiant. Les versions étant immuables,
/// le cache n'est jamais invalidé. Enregistré en Singleton.
/// </summary>
public sealed class CacheDefinitions
{
    private readonly ConcurrentDictionary<IdDefinition, DefinitionProcessus> _versions = new();

    /// <summary>Tente de lire une version en cache.</summary>
    public bool TryObtenir(IdDefinition id, out DefinitionProcessus definition) => _versions.TryGetValue(id, out definition!);

    /// <summary>Ajoute une version au cache.</summary>
    public DefinitionProcessus Ajouter(IdDefinition id, DefinitionProcessus definition) => _versions.GetOrAdd(id, definition);
}

/// <summary>Accès aux définitions : version active par requête légère, contenu par le cache.</summary>
internal sealed class FournisseurDefinitions(IDepotDefinitions depot, CacheDefinitions cache)
{
    public async Task<DefinitionProcessus> ObtenirAsync(IdDefinition id, CancellationToken ct)
    {
        if (cache.TryObtenir(id, out var definition))
        {
            return definition;
        }

        var stockee = await depot.ObtenirAsync(id, ct)
            ?? throw new SentierException($"La version de définition {id} est introuvable.");
        return Charger(stockee);
    }

    public async Task<DefinitionProcessus?> ObtenirActiveAsync(string cleProcessus, CancellationToken ct)
    {
        var id = await depot.ObtenirIdActifAsync(cleProcessus, ct);
        return id is null ? null : await ObtenirAsync(id.Value, ct);
    }

    public async Task<DefinitionProcessus?> ObtenirParVersionAsync(string cleProcessus, int version, CancellationToken ct)
    {
        var stockee = await depot.ObtenirParVersionAsync(cleProcessus, version, ct);
        if (stockee is null)
        {
            return null;
        }

        return cache.TryObtenir(stockee.Id, out var definition) ? definition : Charger(stockee);
    }

    public DefinitionProcessus Charger(DefinitionStockee stockee)
    {
        var definition = SerialisationDefinition.Deserialiser(stockee.Contenu);
        definition.Id = stockee.Id;
        definition.Version = stockee.Version;
        return cache.Ajouter(stockee.Id, definition);
    }
}
