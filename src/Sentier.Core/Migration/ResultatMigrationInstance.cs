namespace Sentier.Migration;

/// <summary>Résultat de migration d'une instance.</summary>
public sealed class ResultatMigrationInstance
{
    /// <summary>Instance concernée.</summary>
    public required IdInstance IdInstance { get; init; }

    /// <summary>Version source.</summary>
    public required int VersionSource { get; init; }

    /// <summary>Version cible.</summary>
    public required int VersionCible { get; init; }

    /// <summary>Vrai si l'instance est migrable (ou a été migrée).</summary>
    public required bool Migrable { get; init; }

    /// <summary>Raison du blocage.</summary>
    public string? Raison { get; init; }

    /// <summary>Actions prévues ou effectuées.</summary>
    public IReadOnlyList<string> Actions { get; init; } = [];
}
