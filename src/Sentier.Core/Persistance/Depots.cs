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

/// <summary>Dépôt des attentes (une ligne par déclencheur).</summary>
public interface IDepotAttentes
{
    /// <summary>Insère une attente.</summary>
    Task InsererAsync(Attente attente, CancellationToken ct);

    /// <summary>Attentes d'une instance.</summary>
    Task<IReadOnlyList<Attente>> ObtenirParInstanceAsync(IdInstance idInstance, CancellationToken ct);

    /// <summary>
    /// Lit et verrouille une attente sans attendre (<c>FOR UPDATE SKIP LOCKED</c> sous Oracle).
    /// Retourne <c>null</c> si elle n'existe plus ou si un autre traitement la verrouille.
    /// </summary>
    Task<Attente?> VerrouillerAsync(IdAttente id, CancellationToken ct);

    /// <summary>Supprime toutes les attentes d'un nœud d'une instance ; retourne le nombre de lignes supprimées.</summary>
    Task<int> SupprimerParNoeudAsync(IdInstance idInstance, string codeNoeud, CancellationToken ct);

    /// <summary>Supprime toutes les attentes d'une instance.</summary>
    Task<int> SupprimerParInstanceAsync(IdInstance idInstance, CancellationToken ct);

    /// <summary>Échéances atteintes des instances suspendues, sans verrou.</summary>
    Task<IReadOnlyList<EcheanceDue>> ObtenirEcheancesDuesAsync(DateTime maintenant, int tailleMax, CancellationToken ct);

    /// <summary>Attentes de signal correspondant au signal envoyé, pour des instances au repos.</summary>
    Task<IReadOnlyList<Attente>> RechercherSignalAsync(Signal signal, CancellationToken ct);

    /// <summary>Renomme le nœud des attentes d'une instance (migration).</summary>
    Task RenommerNoeudAsync(IdInstance idInstance, string ancienCode, string nouveauCode, CancellationToken ct);
}

/// <summary>Dépôt des tâches utilisateurs.</summary>
public interface IDepotTaches
{
    /// <summary>Insère une tâche.</summary>
    Task InsererAsync(TacheUtilisateur tache, CancellationToken ct);

    /// <summary>Tâche par identifiant externe (la plus récente).</summary>
    Task<TacheUtilisateur?> ObtenirParIdExterneAsync(long idTacheExterne, CancellationToken ct);

    /// <summary>Tâches ouvertes d'une instance.</summary>
    Task<IReadOnlyList<TacheUtilisateur>> ObtenirOuvertesAsync(IdInstance idInstance, CancellationToken ct);

    /// <summary>Met à jour le statut et la date de complétion d'une tâche.</summary>
    Task MettreAJourAsync(TacheUtilisateur tache, CancellationToken ct);

    /// <summary>Renomme le nœud des tâches ouvertes d'une instance (migration).</summary>
    Task RenommerNoeudAsync(IdInstance idInstance, string ancienCode, string nouveauCode, CancellationToken ct);
}

/// <summary>Dépôt des traces de migration.</summary>
public interface IDepotMigrations
{
    /// <summary>Insère une trace.</summary>
    Task InsererAsync(TraceMigration trace, CancellationToken ct);
}
