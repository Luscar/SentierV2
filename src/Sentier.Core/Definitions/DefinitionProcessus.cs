using System.Text.Json.Serialization;
using Sentier.Variables;

namespace Sentier.Definitions;

/// <summary>
/// Définition d'un processus : clé, variables déclarées, nœuds et transitions.
/// Le contenu est entièrement sérialisable en JSON ; une version déployée n'est jamais modifiée.
/// </summary>
public sealed class DefinitionProcessus
{
    private Dictionary<string, Noeud>? _noeuds;
    private Dictionary<string, List<Transition>>? _sorties;
    private Dictionary<string, DeclarationVariable>? _declarations;

    /// <summary>Clé du processus, qui donne son sens à la clé d'affaire.</summary>
    public required string CleProcessus { get; init; }

    /// <summary>Nom lisible.</summary>
    public required string Nom { get; init; }

    /// <summary>Une seule instance active par clé d'affaire (vrai par défaut).</summary>
    public bool InstanceUnique { get; init; } = true;

    /// <summary>Migration automatique des instances actives lors d'un changement compatible.</summary>
    public bool MigrationAutomatique { get; init; }

    /// <summary>Variables déclarées.</summary>
    public IReadOnlyList<DeclarationVariable> Variables { get; init; } = [];

    /// <summary>Nœuds, dans l'ordre de déclaration.</summary>
    public IReadOnlyList<Noeud> Noeuds { get; init; } = [];

    /// <summary>Transitions, dans l'ordre d'évaluation.</summary>
    public IReadOnlyList<Transition> Transitions { get; init; } = [];

    /// <summary>Identifiant de la version déployée (nul pour une définition construite en mémoire).</summary>
    [JsonIgnore]
    public IdDefinition? Id { get; internal set; }

    /// <summary>Numéro de la version déployée (0 pour une définition construite en mémoire).</summary>
    [JsonIgnore]
    public int Version { get; internal set; }

    /// <summary>Déclarations des variables, par nom.</summary>
    [JsonIgnore]
    public IReadOnlyDictionary<string, DeclarationVariable> Declarations
        => _declarations ??= Variables.GroupBy(v => v.Nom).ToDictionary(g => g.Key, g => g.First());

    /// <summary>Nœud de début.</summary>
    [JsonIgnore]
    public NoeudDebut NoeudDebut => Noeuds.OfType<NoeudDebut>().FirstOrDefault()
        ?? throw new DefinitionInvalideException(CleProcessus, ["Aucun nœud de début."]);

    /// <summary>Codes des nœuds de fin (codes de fin possibles).</summary>
    [JsonIgnore]
    public IEnumerable<string> CodesFin => Noeuds.OfType<NoeudFin>().Select(n => n.Code);

    /// <summary>Retourne le nœud de code donné, ou <c>null</c>.</summary>
    public Noeud? TrouverNoeud(string code) => IndexNoeuds.GetValueOrDefault(code);

    /// <summary>Retourne le nœud de code donné, ou lève une exception.</summary>
    public Noeud Noeud(string code) => TrouverNoeud(code)
        ?? throw new SentierException($"Processus '{CleProcessus}' v{Version} : le nœud '{code}' n'existe pas.");

    /// <summary>Transitions sortantes d'un nœud, dans l'ordre d'évaluation.</summary>
    public IReadOnlyList<Transition> Sorties(string code)
        => IndexSorties.TryGetValue(code, out var liste) ? liste : [];

    /// <summary>
    /// Transition à suivre pour une sortie nommée : la transition de ce nom si elle existe,
    /// sinon la sortie par défaut (sans nom).
    /// </summary>
    public Transition? Sortie(string code, string? nomSortie)
    {
        var sorties = Sorties(code);
        if (nomSortie is not null)
        {
            var nommee = sorties.FirstOrDefault(t => t.NomSortie == nomSortie);
            if (nommee is not null)
            {
                return nommee;
            }
        }

        return sorties.FirstOrDefault(t => t.NomSortie is null);
    }

    private Dictionary<string, Noeud> IndexNoeuds
        => _noeuds ??= Noeuds.GroupBy(n => n.Code).ToDictionary(g => g.Key, g => g.First());

    private Dictionary<string, List<Transition>> IndexSorties
        => _sorties ??= Transitions.GroupBy(t => t.Source).ToDictionary(g => g.Key, g => g.ToList());
}
