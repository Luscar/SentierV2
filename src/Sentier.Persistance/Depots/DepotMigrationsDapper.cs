
namespace Sentier.Persistance.Depots;

/// <summary>Dépôt Dapper des traces de migration.</summary>
internal sealed class DepotMigrationsDapper(ContexteSql sql) : IDepotMigrations
{
    public async Task InsererAsync(TraceMigration trace, CancellationToken ct)
    {
        trace.Id = new IdMigration(await sql.ProchainIdAsync(sql.Noms.SeqMigration, ct));
        await sql.ExecuterAsync(
            $"INSERT INTO {sql.Noms.Migration} (NO_SEQ_MIGRA_PROCS, NO_SEQ_INSTC_PROCS, NO_SEQ_DEFIN_SOURC, NO_SEQ_DEFIN_CIBLE, MODE_MIGRA, DETL, LOGON_UTIL, DH_MIGRA) " +
            "VALUES (@id, @idInstance, @source, @cible, @mode, @detail, @utilisateur, @dateMigration)",
            new
            {
                id = trace.Id.Valeur,
                idInstance = trace.IdInstance.Valeur,
                source = trace.IdDefinitionSource.Valeur,
                cible = trace.IdDefinitionCible.Valeur,
                mode = CodesBaseDonnees.Code(trace.Mode),
                detail = trace.Detail,
                utilisateur = trace.Utilisateur,
                dateMigration = trace.DateMigration,
            },
            ct);
    }
}
