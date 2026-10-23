using Dapper;
using static Sentier.Persistance.Dapper.ContexteSql;

namespace Sentier.Persistance.Dapper.Depots;

/// <summary>Dépôt Dapper des versions de définitions.</summary>
internal sealed class DepotDefinitionsDapper(ContexteSql sql) : IDepotDefinitions
{
    private const string Colonnes = "ID, CLE_PROCESSUS, VERSION, NOM, INSTANCE_UNIQUE, MIGRATION_AUTO, CONTENU, EMPREINTE, EST_ACTIVE, DATE_CREATION";

    private string Table => sql.Noms.Definition;

    public async Task<DefinitionStockee?> ObtenirDerniereAsync(string cleProcessus, CancellationToken ct)
    {
        var requete = sql.Dialecte.Limiter($"SELECT {Colonnes} FROM {Table} WHERE CLE_PROCESSUS = @cle ORDER BY VERSION DESC", 1);
        var ligne = await sql.Connexion.QueryFirstOrDefaultAsync<Ligne>(sql.Lecture(requete, new { cle = cleProcessus }, ct));
        return ligne?.VersDomaine();
    }

    public async Task<IdDefinition?> ObtenirIdActifAsync(string cleProcessus, CancellationToken ct)
    {
        var requete = sql.Dialecte.Limiter($"SELECT ID FROM {Table} WHERE CLE_PROCESSUS = @cle AND EST_ACTIVE = 1 ORDER BY VERSION DESC", 1);
        var id = await sql.Connexion.ExecuteScalarAsync<long?>(sql.Lecture(requete, new { cle = cleProcessus }, ct));
        return id is null ? null : new IdDefinition(id.Value);
    }

    public async Task<DefinitionStockee?> ObtenirAsync(IdDefinition id, CancellationToken ct)
    {
        var ligne = await sql.Connexion.QueryFirstOrDefaultAsync<Ligne>(sql.Lecture($"SELECT {Colonnes} FROM {Table} WHERE ID = @id", new { id = id.Valeur }, ct));
        return ligne?.VersDomaine();
    }

    public async Task<DefinitionStockee?> ObtenirParVersionAsync(string cleProcessus, int version, CancellationToken ct)
    {
        var ligne = await sql.Connexion.QueryFirstOrDefaultAsync<Ligne>(
            sql.Lecture($"SELECT {Colonnes} FROM {Table} WHERE CLE_PROCESSUS = @cle AND VERSION = @version", new { cle = cleProcessus, version }, ct));
        return ligne?.VersDomaine();
    }

    public async Task InsererAsync(DefinitionStockee definition, CancellationToken ct)
    {
        definition.Id = new IdDefinition(await sql.ProchainIdAsync(sql.Noms.SeqDefinition, ct));
        await sql.Connexion.ExecuteAsync(sql.Ecriture(
            $"UPDATE {Table} SET EST_ACTIVE = 0 WHERE CLE_PROCESSUS = @cle AND EST_ACTIVE = 1",
            new { cle = definition.CleProcessus }, ct));
        await sql.Connexion.ExecuteAsync(sql.Ecriture(
            $"INSERT INTO {Table} ({Colonnes}) VALUES (@id, @cle, @version, @nom, @instanceUnique, @migrationAuto, @contenu, @empreinte, @estActive, @dateCreation)",
            new
            {
                id = definition.Id.Valeur,
                cle = definition.CleProcessus,
                version = definition.Version,
                nom = definition.Nom,
                instanceUnique = Booleen(definition.InstanceUnique),
                migrationAuto = Booleen(definition.MigrationAuto),
                contenu = definition.Contenu,
                empreinte = definition.Empreinte,
                estActive = Booleen(definition.EstActive),
                dateCreation = definition.DateCreation,
            },
            ct));
    }

    private sealed class Ligne
    {
        public long ID { get; set; }

        public string CLE_PROCESSUS { get; set; } = "";

        public int VERSION { get; set; }

        public string NOM { get; set; } = "";

        public int INSTANCE_UNIQUE { get; set; }

        public int MIGRATION_AUTO { get; set; }

        public string CONTENU { get; set; } = "";

        public string EMPREINTE { get; set; } = "";

        public int EST_ACTIVE { get; set; }

        public DateTime DATE_CREATION { get; set; }

        public DefinitionStockee VersDomaine() => new()
        {
            Id = new IdDefinition(ID),
            CleProcessus = CLE_PROCESSUS,
            Version = VERSION,
            Nom = NOM,
            InstanceUnique = INSTANCE_UNIQUE != 0,
            MigrationAuto = MIGRATION_AUTO != 0,
            Contenu = CONTENU,
            Empreinte = EMPREINTE,
            EstActive = EST_ACTIVE != 0,
            DateCreation = Utc(DATE_CREATION),
        };
    }
}
