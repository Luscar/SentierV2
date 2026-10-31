namespace Sentier.Migration;

/// <summary>
/// Plan de migration écrit par le client, pour un processus et une plage de versions source.
/// Découvert par balayage d'assemblys.
/// </summary>
public interface IPlanMigration
{
    /// <summary>Clé du processus concerné.</summary>
    string CleProcessus { get; }

    /// <summary>Plus petite version source couverte par le plan.</summary>
    int VersionSourceMin { get; }

    /// <summary>Plus grande version source couverte par le plan.</summary>
    int VersionSourceMax { get; }

    /// <summary>Décrit les règles du plan.</summary>
    void Definir(ConstructeurPlanMigration plan);
}
