namespace Sentier.Migration;

/// <summary>Rapport d'une simulation ou d'une migration.</summary>
public sealed class RapportMigration
{
    /// <summary>Clé du processus.</summary>
    public required string CleProcessus { get; init; }

    /// <summary>Version cible.</summary>
    public required int VersionCible { get; init; }

    /// <summary>Vrai pour une simulation (rien n'est écrit).</summary>
    public required bool Simulation { get; init; }

    /// <summary>Résultat par instance.</summary>
    public required IReadOnlyList<ResultatMigrationInstance> Instances { get; init; }

    /// <summary>Nombre d'instances migrables (ou migrées).</summary>
    public int NombreMigrables => Instances.Count(i => i.Migrable);

    /// <summary>Nombre d'instances bloquées.</summary>
    public int NombreBloquees => Instances.Count(i => !i.Migrable);
}
