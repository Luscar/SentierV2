using Sentier.Migration;

namespace Sentier;

/// <summary>
/// Toutes les actions de Sentier, pour l'application comme pour le batch externe.
/// La connexion et la transaction proviennent de <see cref="ISessionSentier"/> : elles ne sont jamais passées en paramètre.
/// Les actions qui écrivent exigent une transaction active (<see cref="TransactionRequiseException"/>).
/// </summary>
public interface IServiceSentier
{
    /// <summary>
    /// Démarre une instance de la dernière version active du processus, sur une clé d'affaire.
    /// Si le processus est à instance unique et déjà actif pour cette clé, retourne <c>SansEffet</c> avec l'instance existante.
    /// </summary>
    Task<ResultatExecution> DemarrerAsync(string cleProcessus, long cleAffaire, IReadOnlyDictionary<string, object?>? variables = null, CancellationToken ct = default);

    /// <summary>
    /// Complète une tâche utilisateur à partir de son identifiant chez le client, puis reprend l'instance
    /// (fusion des données, commandes de sortie, puis <see cref="IGestionnaireTaches.CompleterTacheAsync"/>).
    /// </summary>
    Task<ResultatExecution> CompleterTacheAsync(long idTache, IReadOnlyDictionary<string, object?>? donnees = null, string? utilisateur = null, CancellationToken ct = default);

    /// <summary>Envoie un signal aux instances qui l'attendent (processus + clé d'affaire, corrélation, ou les deux).</summary>
    Task<ResultatSignal> EnvoyerSignalAsync(Signal signal, CancellationToken ct = default);

    /// <summary>Envoie un signal aux instances d'un processus pour une clé d'affaire.</summary>
    Task<ResultatSignal> EnvoyerSignalAsync(string nomSignal, string cleProcessus, long cleAffaire, IReadOnlyDictionary<string, object?>? chargeUtile = null, CancellationToken ct = default);

    /// <summary>Envoie un signal aux instances qui l'attendent avec cette clé de corrélation.</summary>
    Task<ResultatSignal> EnvoyerSignalCorrelationAsync(string nomSignal, string cleCorrelation, IReadOnlyDictionary<string, object?>? chargeUtile = null, CancellationToken ct = default);

    /// <summary>Annule une instance, ses tâches ouvertes et ses sous-processus actifs.</summary>
    Task<ResultatExecution> AnnulerAsync(IdInstance idInstance, string motif, CancellationToken ct = default);

    /// <summary>Déploie les définitions découvertes (nouvelle version si l'empreinte change), avec migration automatique si activée.</summary>
    Task<RapportDeploiement> DeployerDefinitionsAsync(CancellationToken ct = default);

    /// <summary>Applique un plan de migration à blanc : rapport par instance, rien n'est écrit.</summary>
    Task<RapportMigration> SimulerMigrationAsync(DemandeMigration demande, CancellationToken ct = default);

    /// <summary>Migre les instances visées vers la version cible, selon le plan applicable.</summary>
    Task<RapportMigration> MigrerAsync(DemandeMigration demande, CancellationToken ct = default);

    /// <summary>Échéances atteintes des instances suspendues, sans verrou (transaction facultative).</summary>
    Task<IReadOnlyList<EcheanceDue>> ObtenirEcheancesDuesAsync(int tailleMax = 100, CancellationToken ct = default);

    /// <summary>Traite une seule échéance et reprend son instance.</summary>
    Task<ResultatExecution> TraiterEcheanceAsync(IdAttente idAttente, CancellationToken ct = default);

    /// <summary>Passe une instance au statut <see cref="StatutInstance.EnErreur"/>, dans une transaction séparée.</summary>
    Task<ResultatExecution> MarquerEnErreurAsync(IdInstance idInstance, string message, CancellationToken ct = default);

    /// <summary>Relance une instance en erreur depuis son nœud courant.</summary>
    Task<ResultatExecution> ReprendreAsync(IdInstance idInstance, CancellationToken ct = default);
}

/// <summary>Consultation de Sentier, en lecture seule (transaction facultative).</summary>
public interface ISuiviSentier
{
    /// <summary>État d'une instance, ou <c>null</c>.</summary>
    Task<EtatInstance?> ObtenirInstanceAsync(IdInstance idInstance, CancellationToken ct = default);

    /// <summary>Instances d'une clé d'affaire pour une ou plusieurs clés de processus, avec filtre optionnel sur le statut.</summary>
    Task<IReadOnlyList<EtatInstance>> ObtenirInstancesParCleAffaireAsync(long cleAffaire, IReadOnlyCollection<string> clesProcessus, IReadOnlyCollection<StatutInstance>? statuts = null, CancellationToken ct = default);

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
