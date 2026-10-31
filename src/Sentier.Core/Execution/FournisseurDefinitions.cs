using Sentier.Definitions;
using Sentier.Persistance;

namespace Sentier.Execution;

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
