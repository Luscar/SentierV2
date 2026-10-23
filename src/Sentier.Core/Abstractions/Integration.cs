namespace Sentier;

/// <summary>Heure courante, injectable. Toutes les dates de Sentier sont en UTC.</summary>
public interface IHorloge
{
    /// <summary>Date et heure courantes, en UTC.</summary>
    DateTime MaintenantUtc { get; }
}

/// <summary>Horloge système par défaut.</summary>
public sealed class HorlogeSysteme : IHorloge
{
    /// <inheritdoc />
    public DateTime MaintenantUtc => DateTime.UtcNow;
}

/// <summary>Optionnel : fournit l'utilisateur courant, pour l'audit.</summary>
public interface IContexteUtilisateur
{
    /// <summary>Identifiant de l'utilisateur courant, ou <c>null</c>.</summary>
    string? Utilisateur { get; }
}

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

    /// <summary>Annule une tâche ouverte (annulation de l'instance, migration).</summary>
    Task AnnulerTacheAsync(long idTache, string motif, CancellationToken ct = default);

    /// <summary>Rattache les tâches ouvertes d'une instance à un nœud renommé (migration).</summary>
    Task RenommerNoeudAsync(IdInstance idInstance, string ancienCode, string nouveauCode, CancellationToken ct = default);
}

/// <summary>Tâche utilisateur telle que la voit Sentier, fournie par le <see cref="IGestionnaireTaches"/> du client.</summary>
public sealed class TacheUtilisateur
{
    /// <summary>Identifiant de la tâche chez le client.</summary>
    public required long Id { get; init; }

    /// <summary>Instance à l'origine de la tâche.</summary>
    public required IdInstance IdInstance { get; init; }

    /// <summary>Code du nœud interactif.</summary>
    public required string CodeNoeud { get; init; }

    /// <summary>Statut de la tâche.</summary>
    public required StatutTache Statut { get; init; }
}

/// <summary>Demande de création d'une tâche, transmise au <see cref="IGestionnaireTaches"/> du client.</summary>
public sealed class DemandeTacheUtilisateur
{
    /// <summary>Instance à l'origine de la tâche.</summary>
    public required IdInstance IdInstance { get; init; }

    /// <summary>Clé du processus.</summary>
    public required string CleProcessus { get; init; }

    /// <summary>Clé d'affaire de l'instance.</summary>
    public required long CleAffaire { get; init; }

    /// <summary>Code du nœud interactif.</summary>
    public required string CodeNoeud { get; init; }

    /// <summary>Nom de la tâche.</summary>
    public required string Nom { get; init; }

    /// <summary>Rôle assigné, optionnel.</summary>
    public string? Role { get; init; }

    /// <summary>Utilisateur assigné, optionnel.</summary>
    public string? Utilisateur { get; init; }

    /// <summary>Date (UTC) d'échéance indicative.</summary>
    public DateTime? DateEcheance { get; init; }

    /// <summary>Données transmises à la tâche (paramètres résolus du nœud).</summary>
    public IReadOnlyDictionary<string, object?> Donnees { get; init; } = new Dictionary<string, object?>();
}
