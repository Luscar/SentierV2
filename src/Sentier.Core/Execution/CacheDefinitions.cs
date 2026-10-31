using System.Collections.Concurrent;
using Sentier.Definitions;

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
