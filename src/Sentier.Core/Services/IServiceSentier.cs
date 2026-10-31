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
    /// <paramref name="utilisateur"/> est l'auteur pour l'audit ; par défaut, celui de <see cref="IContexteUtilisateurSentier"/>.
    /// </summary>
    Task<ResultatExecution> DemarrerAsync(string cleProcessus, long cleAffaire, IReadOnlyDictionary<string, object?>? variables = null, string? utilisateur = null, CancellationToken ct = default);

    /// <summary>
    /// Complète une tâche utilisateur à partir de son identifiant chez le client, puis reprend l'instance
    /// (fusion des données, commandes de sortie, puis <see cref="IGestionnaireTachesSentier.CompleterTacheAsync"/>).
    /// </summary>
    Task<ResultatExecution> CompleterTacheAsync(long idTache, IReadOnlyDictionary<string, object?>? donnees = null, string? utilisateur = null, CancellationToken ct = default);

    /// <summary>
    /// Assigne (ou réassigne) une tâche utilisateur, à partir de son identifiant chez le client
    /// (<see cref="IGestionnaireTachesSentier.AssignerTacheAsync"/>). <c>SansEffet</c> si la tâche est inconnue,
    /// n'est plus ouverte ou n'est pas celle que l'instance attend.
    /// </summary>
    Task<ResultatExecution> AssignerTacheAsync(long idTache, string utilisateur, CancellationToken ct = default);

    /// <summary>
    /// Modifie des variables d'une instance au repos (suspendue ou en erreur), sans la faire avancer.
    /// Chaque variable doit être déclarée dans la version suivie et la valeur du bon type ; une valeur <c>null</c>
    /// efface la variable, sauf si elle est obligatoire. <c>SansEffet</c> si l'instance est introuvable, terminée,
    /// annulée ou en cours d'exécution.
    /// </summary>
    /// <remarks>
    /// Les valeurs déjà calculées à l'entrée du nœud courant (échéance, clé de corrélation, données de la tâche)
    /// ne sont pas recalculées. <paramref name="utilisateur"/> est l'auteur pour le journal ; par défaut, celui de
    /// <see cref="IContexteUtilisateurSentier"/>.
    /// </remarks>
    Task<ResultatExecution> ModifierVariablesAsync(IdInstance idInstance, IReadOnlyDictionary<string, object?> valeurs, string? utilisateur = null, CancellationToken ct = default);

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
