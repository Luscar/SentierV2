namespace Sentier.Migration;

/// <summary>Demande de simulation ou de migration d'instances.</summary>
public sealed class DemandeMigration
{
    /// <summary>Clé du processus.</summary>
    public required string CleProcessus { get; init; }

    /// <summary>Version cible ; par défaut, la dernière version active.</summary>
    public int? VersionCible { get; init; }

    /// <summary>Instances visées par identifiant ; sinon, sélection par critère.</summary>
    public IReadOnlyList<IdInstance>? Instances { get; init; }

    /// <summary>Critère : version source.</summary>
    public int? VersionSource { get; init; }

    /// <summary>Critère : code du nœud courant.</summary>
    public string? CodeNoeud { get; init; }

    /// <summary>Nombre maximal d'instances traitées (par lot).</summary>
    public int TailleMax { get; init; } = 100;

    /// <summary>Utilisateur à l'origine de la migration (sinon, <see cref="IContexteUtilisateur"/>).</summary>
    public string? Utilisateur { get; init; }
}
