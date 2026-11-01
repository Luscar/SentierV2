using static Sentier.Persistance.ContexteSql;

namespace Sentier.Persistance.Depots;

/// <summary>Dépôt Dapper des versions de définitions.</summary>
internal sealed class DepotDefinitionsDapper(ContexteSql sql) : IDepotDefinitions
{
    private const string Colonnes = "NO_SEQ_DEFIN_PROCS, CLE, VERSI, NOM_DEFIN, IND_INSTC_UNIQ, IND_MIGRA_AUTO, DEFIN_JSON, EMPRE, IND_ACTIV, DH_CREA";

    private string Table => sql.Noms.Definition;

    public async Task<DefinitionStockee?> ObtenirDerniereAsync(string cleProcessus, CancellationToken ct)
    {
        var requete = sql.Dialecte.Limiter($"SELECT {Colonnes} FROM {Table} WHERE CLE = @cle ORDER BY VERSI DESC", 1);
        var ligne = await sql.LirePremierAsync<Ligne>(requete, new { cle = cleProcessus }, ct);
        return ligne?.VersDomaine();
    }

    public async Task<IdDefinition?> ObtenirIdActifAsync(string cleProcessus, CancellationToken ct)
    {
        var requete = sql.Dialecte.Limiter($"SELECT NO_SEQ_DEFIN_PROCS FROM {Table} WHERE CLE = @cle AND IND_ACTIV = 1 ORDER BY VERSI DESC", 1);
        var id = await sql.LireScalaireAsync<long?>(requete, new { cle = cleProcessus }, ct);
        return id is null ? null : new IdDefinition(id.Value);
    }

    public async Task<DefinitionStockee?> ObtenirAsync(IdDefinition id, CancellationToken ct)
    {
        var ligne = await sql.LirePremierAsync<Ligne>($"SELECT {Colonnes} FROM {Table} WHERE NO_SEQ_DEFIN_PROCS = @id", new { id = id.Valeur }, ct);
        return ligne?.VersDomaine();
    }

    public async Task<DefinitionStockee?> ObtenirParVersionAsync(string cleProcessus, int version, CancellationToken ct)
    {
        var ligne = await sql.LirePremierAsync<Ligne>($"SELECT {Colonnes} FROM {Table} WHERE CLE = @cle AND VERSI = @version", new { cle = cleProcessus, version }, ct);
        return ligne?.VersDomaine();
    }

    public async Task InsererAsync(DefinitionStockee definition, CancellationToken ct)
    {
        definition.Id = new IdDefinition(await sql.ProchainIdAsync(sql.Noms.SeqDefinition, ct));
        await sql.ExecuterAsync(
            $"UPDATE {Table} SET IND_ACTIV = 0 WHERE CLE = @cle AND IND_ACTIV = 1",
            new { cle = definition.CleProcessus }, ct);
        await sql.ExecuterAsync(
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
            ct);
    }

    private sealed class Ligne
    {
        public long NO_SEQ_DEFIN_PROCS { get; set; }

        public string CLE { get; set; } = "";

        public int VERSI { get; set; }

        public string NOM_DEFIN { get; set; } = "";

        public int IND_INSTC_UNIQ { get; set; }

        public int IND_MIGRA_AUTO { get; set; }

        public string DEFIN_JSON { get; set; } = "";

        public string EMPRE { get; set; } = "";

        public int IND_ACTIV { get; set; }

        public DateTime DH_CREA { get; set; }

        public DefinitionStockee VersDomaine() => new()
        {
            Id = new IdDefinition(NO_SEQ_DEFIN_PROCS),
            CleProcessus = CLE,
            Version = VERSI,
            Nom = NOM_DEFIN,
            InstanceUnique = IND_INSTC_UNIQ != 0,
            MigrationAuto = IND_MIGRA_AUTO != 0,
            Contenu = DEFIN_JSON,
            Empreinte = EMPRE,
            EstActive = IND_ACTIV != 0,
            DateCreation = Utc(DH_CREA),
        };
    }
}
