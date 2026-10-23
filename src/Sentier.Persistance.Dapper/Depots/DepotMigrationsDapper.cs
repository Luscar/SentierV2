using Dapper;

namespace Sentier.Persistance.Dapper.Depots;

/// <summary>Dépôt Dapper des traces de migration.</summary>
internal sealed class DepotMigrationsDapper(ContexteSql sql) : IDepotMigrations
{
    public async Task InsererAsync(TraceMigration trace, CancellationToken ct)
    {
        trace.Id = new IdMigration(await sql.ProchainIdAsync(sql.Noms.SeqMigration, ct));
        await sql.Connexion.ExecuteAsync(sql.Ecriture(
            $"INSERT INTO {sql.Noms.Migration} (ID, ID_INSTANCE, ID_DEFINITION_SOURCE, ID_DEFINITION_CIBLE, MODE, DETAIL, UTILISATEUR, DATE_MIGRATION) " +
            "VALUES (@id, @idInstance, @source, @cible, @mode, @detail, @utilisateur, @dateMigration)",
            new
            {
                id = trace.Id.Valeur,
                idInstance = trace.IdInstance.Valeur,
                source = trace.IdDefinitionSource.Valeur,
                cible = trace.IdDefinitionCible.Valeur,
                mode = CodesBase.Code(trace.Mode),
                detail = trace.Detail,
                utilisateur = trace.Utilisateur,
                dateMigration = trace.DateMigration,
            },
            ct));
    }
}
