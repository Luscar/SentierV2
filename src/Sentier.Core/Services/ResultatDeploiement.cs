namespace Sentier;

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
