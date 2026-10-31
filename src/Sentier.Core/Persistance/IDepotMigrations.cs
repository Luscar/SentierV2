namespace Sentier.Persistance;

/// <summary>Dépôt des traces de migration.</summary>
public interface IDepotMigrations
{
    /// <summary>Insère une trace.</summary>
    Task InsererAsync(TraceMigration trace, CancellationToken ct);
}
