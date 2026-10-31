using Sentier.Variables;

namespace Sentier;

/// <summary>Consultation de Sentier, en lecture seule (transaction facultative).</summary>
public interface ISuiviSentier
{
    /// <summary>État d'une instance, ou <c>null</c>.</summary>
    Task<EtatInstance?> ObtenirInstanceAsync(IdInstance idInstance, CancellationToken ct = default);

    /// <summary>Instances d'une clé d'affaire pour une ou plusieurs clés de processus, avec filtre optionnel sur le statut.</summary>
    Task<IReadOnlyList<EtatInstance>> ObtenirInstancesParCleAffaireAsync(long cleAffaire, IReadOnlyCollection<string> clesProcessus, IReadOnlyCollection<StatutInstance>? statuts = null, CancellationToken ct = default);

    /// <summary>
    /// Instances d'un processus dont les variables respectent tous les filtres, des plus récentes aux plus anciennes.
    /// Les filtres sont évalués avec le type déclaré par la version de chaque instance ; une variable absente
    /// de cette version est vide. Lève <see cref="ArgumentException"/> si un filtre est incompatible avec le type déclaré.
    /// </summary>
    Task<IReadOnlyList<EtatInstance>> RechercherParVariablesAsync(RechercheParVariables recherche, CancellationToken ct = default);

    /// <summary>Instances d'un processus dont une variable est égale à une valeur, des plus récentes aux plus anciennes.</summary>
    Task<IReadOnlyList<EtatInstance>> RechercherParVariableAsync<T>(string cleProcessus, Variable<T> variable, T valeur, IReadOnlyCollection<StatutInstance>? statuts = null, int tailleMax = 100, CancellationToken ct = default);

    /// <summary>Nœuds traversés par une instance, dans l'ordre.</summary>
    Task<IReadOnlyList<PassageNoeud>> ObtenirHistoriqueAsync(IdInstance idInstance, CancellationToken ct = default);

    /// <summary>Déclencheurs en cours d'une instance.</summary>
    Task<IReadOnlyList<Attente>> ObtenirAttentesAsync(IdInstance idInstance, CancellationToken ct = default);

    /// <summary>Tâches utilisateurs ouvertes d'une instance, lues par le gestionnaire de tâches du client.</summary>
    Task<IReadOnlyList<TacheUtilisateur>> ObtenirTachesOuvertesAsync(IdInstance idInstance, CancellationToken ct = default);

    /// <summary>Instances en erreur, paginées (page à partir de 0).</summary>
    Task<IReadOnlyList<EtatInstance>> ObtenirInstancesEnErreurAsync(int page = 0, int taille = 50, CancellationToken ct = default);

    /// <summary>Nombre d'instances actives d'un processus, par version et par nœud.</summary>
    Task<IReadOnlyList<CompteInstancesActives>> CompterInstancesActivesAsync(string cleProcessus, CancellationToken ct = default);

    /// <summary>Diagramme Mermaid d'une définition (dernière version active, ou version donnée).</summary>
    Task<string> ExporterDiagrammeAsync(string cleProcessus, int? version = null, CancellationToken ct = default);
}
