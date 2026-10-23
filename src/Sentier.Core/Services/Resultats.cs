namespace Sentier;

/// <summary>Résultat d'une action de <see cref="IServiceSentier"/>.</summary>
public sealed record ResultatExecution
{
    /// <summary>Instance concernée, si elle existe.</summary>
    public IdInstance? IdInstance { get; init; }

    /// <summary>Statut de l'instance après l'action.</summary>
    public StatutInstance? Statut { get; init; }

    /// <summary>Code du nœud courant après l'action.</summary>
    public string? CodeNoeudCourant { get; init; }

    /// <summary>Code de fin, si l'instance est terminée.</summary>
    public string? CodeFin { get; init; }

    /// <summary>Vrai s'il ne s'est rien passé (action idempotente).</summary>
    public bool SansEffet { get; init; }

    /// <summary>Raison de l'absence d'effet.</summary>
    public string? Raison { get; init; }

    /// <summary>Résultat d'une action effectuée.</summary>
    public static ResultatExecution Depuis(InstanceProcessus instance) => new()
    {
        IdInstance = instance.Id,
        Statut = instance.Statut,
        CodeNoeudCourant = instance.CodeNoeudCourant,
        CodeFin = instance.CodeFin,
    };

    /// <summary>Résultat d'une action sans effet.</summary>
    public static ResultatExecution Aucun(string raison, InstanceProcessus? instance = null) => new()
    {
        IdInstance = instance?.Id,
        Statut = instance?.Statut,
        CodeNoeudCourant = instance?.CodeNoeudCourant,
        CodeFin = instance?.CodeFin,
        SansEffet = true,
        Raison = raison,
    };
}

/// <summary>
/// Signal nommé envoyé aux instances qui l'attendent. Il cible un processus et une clé d'affaire,
/// une clé de corrélation, ou les deux. Une clé d'affaire n'est jamais utilisée seule.
/// </summary>
public sealed class Signal
{
    /// <summary>Nom du signal.</summary>
    public required string Nom { get; init; }

    /// <summary>Processus visé (obligatoire avec une clé d'affaire).</summary>
    public string? CleProcessus { get; init; }

    /// <summary>Clé d'affaire visée.</summary>
    public long? CleAffaire { get; init; }

    /// <summary>Clé de corrélation visée.</summary>
    public string? CleCorrelation { get; init; }

    /// <summary>Charge utile, fusionnée dans les variables déclarées.</summary>
    public IReadOnlyDictionary<string, object?>? ChargeUtile { get; init; }
}

/// <summary>Résultat de l'envoi d'un signal.</summary>
public sealed record ResultatSignal
{
    /// <summary>Nombre d'instances reprises.</summary>
    public int NombreInstancesReprises => Resultats.Count(r => !r.SansEffet);

    /// <summary>Résultat par instance visée.</summary>
    public IReadOnlyList<ResultatExecution> Resultats { get; init; } = [];

    /// <summary>Vrai si aucune instance n'a été reprise.</summary>
    public bool SansEffet => NombreInstancesReprises == 0;

    /// <summary>Raison de l'absence d'effet.</summary>
    public string? Raison { get; init; }
}

/// <summary>Échéance atteinte, à traiter par le batch externe.</summary>
public sealed record EcheanceDue(IdAttente IdAttente, IdInstance IdInstance, string CleProcessus, long CleAffaire, DateTime DateEcheance);

/// <summary>Nombre d'instances actives d'un processus pour une version et un nœud.</summary>
public sealed record CompteInstancesActives(IdDefinition IdDefinition, int Version, string CodeNoeud, int Nombre);

/// <summary>État consultable d'une instance.</summary>
public sealed record EtatInstance
{
    /// <summary>Identifiant.</summary>
    public required IdInstance Id { get; init; }

    /// <summary>Clé du processus.</summary>
    public required string CleProcessus { get; init; }

    /// <summary>Version de définition suivie.</summary>
    public required IdDefinition IdDefinition { get; init; }

    /// <summary>Numéro de version de la définition.</summary>
    public required int VersionDefinition { get; init; }

    /// <summary>Clé d'affaire.</summary>
    public required long CleAffaire { get; init; }

    /// <summary>Statut.</summary>
    public required StatutInstance Statut { get; init; }

    /// <summary>Nœud courant.</summary>
    public required string CodeNoeudCourant { get; init; }

    /// <summary>Code de fin.</summary>
    public string? CodeFin { get; init; }

    /// <summary>Instance parente.</summary>
    public IdInstance? IdInstanceParent { get; init; }

    /// <summary>Variables, relues avec leur type déclaré.</summary>
    public IReadOnlyDictionary<string, object?> Variables { get; init; } = new Dictionary<string, object?>();

    /// <summary>Date de création (UTC).</summary>
    public DateTime DateCreation { get; init; }

    /// <summary>Date de dernière mise à jour (UTC).</summary>
    public DateTime DateMaj { get; init; }

    /// <summary>Date de fin (UTC).</summary>
    public DateTime? DateFin { get; init; }

    /// <summary>Message d'erreur.</summary>
    public string? MessageErreur { get; init; }

    /// <summary>Date de l'erreur (UTC).</summary>
    public DateTime? DateErreur { get; init; }
}

/// <summary>Issue du déploiement d'une définition.</summary>
public enum EtatDeploiement
{
    /// <summary>Empreinte identique : rien n'a été créé.</summary>
    Inchangee,

    /// <summary>Nouvelle version enregistrée ; instances migrées automatiquement le cas échéant.</summary>
    NouvelleVersion,

    /// <summary>Nouvelle version enregistrée, mais des instances restent sur leur version (changement incompatible ou migration automatique désactivée).</summary>
    NouvelleVersionIncompatible,
}

/// <summary>Résultat du déploiement d'une définition.</summary>
public sealed record ResultatDeploiement
{
    /// <summary>Clé du processus.</summary>
    public required string CleProcessus { get; init; }

    /// <summary>Issue du déploiement.</summary>
    public required EtatDeploiement Etat { get; init; }

    /// <summary>Version active après le déploiement.</summary>
    public required int Version { get; init; }

    /// <summary>Nombre d'instances migrées automatiquement.</summary>
    public int InstancesMigrees { get; init; }

    /// <summary>Nombre d'instances actives restées sur une version antérieure.</summary>
    public int InstancesRestees { get; init; }

    /// <summary>Avertissements (codes de nœuds disparus alors que des instances s'y trouvent…).</summary>
    public IReadOnlyList<string> Avertissements { get; init; } = [];
}

/// <summary>Rapport de déploiement des définitions.</summary>
public sealed record RapportDeploiement(IReadOnlyList<ResultatDeploiement> Definitions);
