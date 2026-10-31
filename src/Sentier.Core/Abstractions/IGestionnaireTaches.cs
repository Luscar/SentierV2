namespace Sentier;

/// <summary>
/// Gestion des tâches utilisateurs, implémentée par le client et injectée (Scoped), au même titre que ses
/// commandes et requêtes : Sentier ne stocke aucune tâche. Le client crée, conserve, complète et annule ses tâches ;
/// Sentier lui délègue chaque étape du cycle de vie. S'il est en base, il doit utiliser la même session
/// (donc la même transaction) que Sentier.
/// </summary>
/// <remarks>
/// Découverte par balayage d'assemblys (une seule implémentation), ou enregistrée explicitement par
/// <see cref="OptionsSentier.UtiliserGestionnaireTaches{T}"/>.
/// </remarks>
public interface IGestionnaireTaches
{
    /// <summary>Crée une tâche ouverte pour un nœud interactif et retourne son identifiant.</summary>
    Task<long> CreerTacheAsync(DemandeTacheUtilisateur demande, CancellationToken ct = default);

    /// <summary>Tâche par identifiant, ou <c>null</c> si elle est inconnue.</summary>
    Task<TacheUtilisateur?> ObtenirTacheAsync(long idTache, CancellationToken ct = default);

    /// <summary>Tâches ouvertes d'une instance.</summary>
    Task<IReadOnlyList<TacheUtilisateur>> ObtenirTachesOuvertesAsync(IdInstance idInstance, CancellationToken ct = default);

    /// <summary>
    /// Marque une tâche complétée, avec les données de complétion reçues par <c>IServiceSentier.CompleterTacheAsync</c>.
    /// Appelée après les commandes de sortie. Doit être idempotente : elle est rappelée si la transaction de Sentier
    /// a été annulée après un premier appel.
    /// </summary>
    Task CompleterTacheAsync(long idTache, IReadOnlyDictionary<string, object?>? donnees, string? utilisateur, CancellationToken ct = default);

    /// <summary>
    /// Assigne (ou réassigne) une tâche ouverte à un utilisateur. Appelée par <c>IServiceSentier.AssignerTacheAsync</c>,
    /// seulement pour la tâche que l'instance attend.
    /// </summary>
    Task AssignerTacheAsync(long idTache, string utilisateur, CancellationToken ct = default);

    /// <summary>Annule une tâche ouverte (annulation de l'instance, migration).</summary>
    Task AnnulerTacheAsync(long idTache, string motif, CancellationToken ct = default);

    /// <summary>Rattache les tâches ouvertes d'une instance à un nœud renommé (migration).</summary>
    Task RenommerNoeudAsync(IdInstance idInstance, string ancienCode, string nouveauCode, CancellationToken ct = default);
}
