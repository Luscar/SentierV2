using Dapper;
using static Sentier.Persistance.Dapper.ContexteSql;

namespace Sentier.Persistance.Dapper.Depots;

/// <summary>Dépôt Dapper des instances, avec verrou optimiste sur la colonne VERSI.</summary>
internal sealed class DepotInstancesDapper(ContexteSql sql) : IDepotInstances
{
    private const string Colonnes =
        "NO_SEQ_INSTC_PROCS, NO_SEQ_DEFIN_PROCS, CLE_DEFIN, ID_INSTC_PARN, ID_NOEUD_PARN, AGGRE_ID, IND_INSTC_UNIQ, STAT, ID_NOEUD_FIN, " +
        "ID_NOEUD_COUR, ID_TACH_EXTE, VAR_JSON, MESS_ERR, DH_ERR, VERSI, DH_CREA, DH_MODIF, DH_FIN";

    private string Table => sql.Noms.Instance;

    public async Task<InstanceProcessus?> ObtenirAsync(IdInstance id, CancellationToken ct)
    {
        var ligne = await sql.Connexion.QueryFirstOrDefaultAsync<Ligne>(sql.Lecture($"SELECT {Colonnes} FROM {Table} WHERE NO_SEQ_INSTC_PROCS = @id", new { id = id.Valeur }, ct));
        return ligne?.VersDomaine();
    }

    public async Task InsererAsync(InstanceProcessus instance, CancellationToken ct)
    {
        instance.Id = new IdInstance(await sql.ProchainIdAsync(sql.Noms.SeqInstance, ct));
        await sql.Connexion.ExecuteAsync(sql.Ecriture(
            $"INSERT INTO {Table} ({Colonnes}) VALUES (@id, @idDefinition, @cleProcessus, @idParent, @codeNoeudParent, @cleAffaire, @instanceUnique, @statut, @codeFin, " +
            "@codeNoeudCourant, @idTacheCourante, @variables, @messageErreur, @dateErreur, @version, @dateCreation, @dateMaj, @dateFin)",
            new
            {
                id = instance.Id.Valeur,
                idDefinition = instance.IdDefinition.Valeur,
                cleProcessus = instance.CleProcessus,
                idParent = instance.IdInstanceParent?.Valeur,
                codeNoeudParent = instance.CodeNoeudParent,
                cleAffaire = instance.CleAffaire,
                instanceUnique = Booleen(instance.InstanceUnique),
                statut = CodesBase.Code(instance.Statut),
                codeFin = instance.CodeFin,
                codeNoeudCourant = instance.CodeNoeudCourant,
                idTacheCourante = instance.IdTacheCourante,
                variables = instance.VariablesJson,
                messageErreur = instance.MessageErreur,
                dateErreur = instance.DateErreur,
                version = instance.Version,
                dateCreation = instance.DateCreation,
                dateMaj = instance.DateMaj,
                dateFin = instance.DateFin,
            },
            ct));
    }

    public async Task MettreAJourAsync(InstanceProcessus instance, CancellationToken ct)
    {
        var lignes = await sql.Connexion.ExecuteAsync(sql.Ecriture(
            $"UPDATE {Table} SET NO_SEQ_DEFIN_PROCS = @idDefinition, STAT = @statut, ID_NOEUD_FIN = @codeFin, ID_NOEUD_COUR = @codeNoeudCourant, ID_TACH_EXTE = @idTacheCourante, " +
            "VAR_JSON = @variables, MESS_ERR = @messageErreur, DH_ERR = @dateErreur, DH_MODIF = @dateMaj, DH_FIN = @dateFin, " +
            "VERSI = VERSI + 1 WHERE NO_SEQ_INSTC_PROCS = @id AND VERSI = @version",
            new
            {
                id = instance.Id.Valeur,
                idDefinition = instance.IdDefinition.Valeur,
                statut = CodesBase.Code(instance.Statut),
                codeFin = instance.CodeFin,
                codeNoeudCourant = instance.CodeNoeudCourant,
                idTacheCourante = instance.IdTacheCourante,
                variables = instance.VariablesJson,
                messageErreur = instance.MessageErreur,
                dateErreur = instance.DateErreur,
                dateMaj = instance.DateMaj,
                dateFin = instance.DateFin,
                version = instance.Version,
            },
            ct));

        if (lignes != 1)
        {
            throw new ConflitConcurrenceException(instance.Id, instance.Version);
        }

        instance.Version++;
    }

    public Task<IReadOnlyList<InstanceProcessus>> ObtenirActivesAsync(string cleProcessus, long cleAffaire, CancellationToken ct)
        => ListerAsync(
            $"SELECT {Colonnes} FROM {Table} WHERE CLE_DEFIN = @cle AND AGGRE_ID = @cleAffaire AND STAT IN {CodesBase.StatutsActifs} ORDER BY NO_SEQ_INSTC_PROCS",
            new { cle = cleProcessus, cleAffaire },
            ct);

    public Task<IReadOnlyList<InstanceProcessus>> RechercherAsync(long cleAffaire, IReadOnlyCollection<string> clesProcessus, IReadOnlyCollection<StatutInstance>? statuts, CancellationToken ct)
    {
        var requete = $"SELECT {Colonnes} FROM {Table} WHERE AGGRE_ID = @cleAffaire AND CLE_DEFIN IN @cles";
        if (statuts is { Count: > 0 })
        {
            requete += " AND STAT IN @statuts";
        }

        return ListerAsync(
            requete + " ORDER BY NO_SEQ_INSTC_PROCS",
            new { cleAffaire, cles = clesProcessus.ToArray(), statuts = statuts?.Select(CodesBase.Code).ToArray() ?? [] },
            ct);
    }

    public Task<IReadOnlyList<InstanceProcessus>> ObtenirLotParProcessusAsync(
        string cleProcessus,
        IReadOnlyCollection<StatutInstance>? statuts,
        IReadOnlyList<string> motifsVariables,
        IdInstance? avant,
        int taille,
        CancellationToken ct)
    {
        var parametres = new DynamicParameters();
        parametres.Add("cle", cleProcessus);
        var requete = $"SELECT {Colonnes} FROM {Table} WHERE CLE_DEFIN = @cle";
        if (statuts is { Count: > 0 })
        {
            requete += " AND STAT IN @statuts";
            parametres.Add("statuts", statuts.Select(CodesBase.Code).ToArray());
        }

        if (avant is { } id)
        {
            requete += " AND NO_SEQ_INSTC_PROCS < @avant";
            parametres.Add("avant", id.Valeur);
        }

        for (var i = 0; i < motifsVariables.Count; i++)
        {
            requete += $" AND VAR_JSON LIKE @motif{i} ESCAPE '\\'";
            parametres.Add($"motif{i}", motifsVariables[i]);
        }

        return ListerAsync(sql.Dialecte.Limiter(requete + " ORDER BY NO_SEQ_INSTC_PROCS DESC", taille), parametres, ct);
    }

    public Task<IReadOnlyList<InstanceProcessus>> ObtenirEnfantsActifsAsync(IdInstance idParent, CancellationToken ct)
        => ListerAsync(
            $"SELECT {Colonnes} FROM {Table} WHERE ID_INSTC_PARN = @id AND STAT IN {CodesBase.StatutsActifs} ORDER BY NO_SEQ_INSTC_PROCS",
            new { id = idParent.Valeur },
            ct);

    public Task<IReadOnlyList<InstanceProcessus>> ObtenirEnErreurAsync(int decalage, int taille, CancellationToken ct)
        => ListerAsync(
            sql.Dialecte.Paginer($"SELECT {Colonnes} FROM {Table} WHERE STAT = 'EN_ERREUR' ORDER BY DH_ERR DESC, NO_SEQ_INSTC_PROCS", decalage, taille),
            null,
            ct);

    public async Task<IReadOnlyList<CompteInstancesActives>> CompterActivesAsync(string cleProcessus, CancellationToken ct)
    {
        var lignes = await sql.Connexion.QueryAsync<LigneCompte>(sql.Lecture(
            $"SELECT i.NO_SEQ_DEFIN_PROCS AS NO_SEQ_DEFIN_PROCS, d.VERSI AS VERSI, i.ID_NOEUD_COUR AS ID_NOEUD, COUNT(*) AS NOMBRE " +
            $"FROM {Table} i JOIN {sql.Noms.Definition} d ON d.NO_SEQ_DEFIN_PROCS = i.NO_SEQ_DEFIN_PROCS " +
            $"WHERE i.CLE_DEFIN = @cle AND i.STAT IN {CodesBase.StatutsActifs} " +
            "GROUP BY i.NO_SEQ_DEFIN_PROCS, d.VERSI, i.ID_NOEUD_COUR ORDER BY d.VERSI, i.ID_NOEUD_COUR",
            new { cle = cleProcessus },
            ct));
        return lignes.Select(l => new CompteInstancesActives(new IdDefinition(l.NO_SEQ_DEFIN_PROCS), l.VERSI, l.ID_NOEUD, l.NOMBRE)).ToList();
    }

    public Task<IReadOnlyList<InstanceProcessus>> ObtenirAuReposAsync(IdDefinition idDefinition, string? codeNoeud, int taille, CancellationToken ct)
    {
        var requete = $"SELECT {Colonnes} FROM {Table} WHERE NO_SEQ_DEFIN_PROCS = @id AND STAT IN {CodesBase.StatutsAuRepos}";
        if (codeNoeud is not null)
        {
            requete += " AND ID_NOEUD_COUR = @code";
        }

        return ListerAsync(sql.Dialecte.Limiter(requete + " ORDER BY NO_SEQ_INSTC_PROCS", taille), new { id = idDefinition.Valeur, code = codeNoeud }, ct);
    }

    public Task RenommerNoeudParentAsync(IdInstance idParent, string ancienCode, string nouveauCode, CancellationToken ct)
        => sql.Connexion.ExecuteAsync(sql.Ecriture(
            $"UPDATE {Table} SET ID_NOEUD_PARN = @nouveau WHERE ID_INSTC_PARN = @id AND ID_NOEUD_PARN = @ancien AND STAT IN {CodesBase.StatutsActifs}",
            new { id = idParent.Valeur, ancien = ancienCode, nouveau = nouveauCode },
            ct));

    private async Task<IReadOnlyList<InstanceProcessus>> ListerAsync(string requete, object? parametres, CancellationToken ct)
        => (await sql.Connexion.QueryAsync<Ligne>(sql.Lecture(requete, parametres, ct))).Select(l => l.VersDomaine()).ToList();

    private sealed class LigneCompte
    {
        public long NO_SEQ_DEFIN_PROCS { get; set; }

        public int VERSI { get; set; }

        public string ID_NOEUD { get; set; } = "";

        public int NOMBRE { get; set; }
    }

    private sealed class Ligne
    {
        public long NO_SEQ_INSTC_PROCS { get; set; }

        public long NO_SEQ_DEFIN_PROCS { get; set; }

        public string CLE_DEFIN { get; set; } = "";

        public long? ID_INSTC_PARN { get; set; }

        public string? ID_NOEUD_PARN { get; set; }

        public long AGGRE_ID { get; set; }

        public int IND_INSTC_UNIQ { get; set; }

        public string STAT { get; set; } = "";

        public string? ID_NOEUD_FIN { get; set; }

        public string ID_NOEUD_COUR { get; set; } = "";

        public long? ID_TACH_EXTE { get; set; }

        public string? VAR_JSON { get; set; }

        public string? MESS_ERR { get; set; }

        public DateTime? DH_ERR { get; set; }

        public int VERSI { get; set; }

        public DateTime DH_CREA { get; set; }

        public DateTime DH_MODIF { get; set; }

        public DateTime? DH_FIN { get; set; }

        public InstanceProcessus VersDomaine() => new()
        {
            Id = new IdInstance(NO_SEQ_INSTC_PROCS),
            IdDefinition = new IdDefinition(NO_SEQ_DEFIN_PROCS),
            CleProcessus = CLE_DEFIN,
            IdInstanceParent = ID_INSTC_PARN is null ? null : new IdInstance(ID_INSTC_PARN.Value),
            CodeNoeudParent = ID_NOEUD_PARN,
            CleAffaire = AGGRE_ID,
            InstanceUnique = IND_INSTC_UNIQ != 0,
            Statut = CodesBase.LireStatutInstance(STAT),
            CodeFin = ID_NOEUD_FIN,
            CodeNoeudCourant = ID_NOEUD_COUR,
            IdTacheCourante = ID_TACH_EXTE,
            VariablesJson = VAR_JSON,
            MessageErreur = MESS_ERR,
            DateErreur = Utc(DH_ERR),
            Version = VERSI,
            DateCreation = Utc(DH_CREA),
            DateMaj = Utc(DH_MODIF),
            DateFin = Utc(DH_FIN),
        };
    }
}
