using Dapper;
using static Sentier.Persistance.Dapper.ContexteSql;

namespace Sentier.Persistance.Dapper.Depots;

/// <summary>Dépôt Dapper des instances, avec verrou optimiste sur la colonne VERSION.</summary>
internal sealed class DepotInstancesDapper(ContexteSql sql) : IDepotInstances
{
    private const string Colonnes =
        "ID, ID_DEFINITION, CLE_PROCESSUS, ID_INSTANCE_PARENT, CODE_NOEUD_PARENT, CLE_AFFAIRE, INSTANCE_UNIQUE, STATUT, CODE_FIN, " +
        "CODE_NOEUD_COURANT, VARIABLES, MESSAGE_ERREUR, DATE_ERREUR, VERSION, DATE_CREATION, DATE_MAJ, DATE_FIN";

    private string Table => sql.Noms.Instance;

    public async Task<InstanceProcessus?> ObtenirAsync(IdInstance id, CancellationToken ct)
    {
        var ligne = await sql.Connexion.QueryFirstOrDefaultAsync<Ligne>(sql.Lecture($"SELECT {Colonnes} FROM {Table} WHERE ID = @id", new { id = id.Valeur }, ct));
        return ligne?.VersDomaine();
    }

    public async Task InsererAsync(InstanceProcessus instance, CancellationToken ct)
    {
        instance.Id = new IdInstance(await sql.ProchainIdAsync(sql.Noms.SeqInstance, ct));
        await sql.Connexion.ExecuteAsync(sql.Ecriture(
            $"INSERT INTO {Table} ({Colonnes}) VALUES (@id, @idDefinition, @cleProcessus, @idParent, @codeNoeudParent, @cleAffaire, @instanceUnique, @statut, @codeFin, " +
            "@codeNoeudCourant, @variables, @messageErreur, @dateErreur, @version, @dateCreation, @dateMaj, @dateFin)",
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
            $"UPDATE {Table} SET ID_DEFINITION = @idDefinition, STATUT = @statut, CODE_FIN = @codeFin, CODE_NOEUD_COURANT = @codeNoeudCourant, " +
            "VARIABLES = @variables, MESSAGE_ERREUR = @messageErreur, DATE_ERREUR = @dateErreur, DATE_MAJ = @dateMaj, DATE_FIN = @dateFin, " +
            "VERSION = VERSION + 1 WHERE ID = @id AND VERSION = @version",
            new
            {
                id = instance.Id.Valeur,
                idDefinition = instance.IdDefinition.Valeur,
                statut = CodesBase.Code(instance.Statut),
                codeFin = instance.CodeFin,
                codeNoeudCourant = instance.CodeNoeudCourant,
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
            $"SELECT {Colonnes} FROM {Table} WHERE CLE_PROCESSUS = @cle AND CLE_AFFAIRE = @cleAffaire AND STATUT IN {CodesBase.StatutsActifs} ORDER BY ID",
            new { cle = cleProcessus, cleAffaire },
            ct);

    public Task<IReadOnlyList<InstanceProcessus>> RechercherAsync(long cleAffaire, IReadOnlyCollection<string> clesProcessus, IReadOnlyCollection<StatutInstance>? statuts, CancellationToken ct)
    {
        var requete = $"SELECT {Colonnes} FROM {Table} WHERE CLE_AFFAIRE = @cleAffaire AND CLE_PROCESSUS IN @cles";
        if (statuts is { Count: > 0 })
        {
            requete += " AND STATUT IN @statuts";
        }

        return ListerAsync(
            requete + " ORDER BY ID",
            new { cleAffaire, cles = clesProcessus.ToArray(), statuts = statuts?.Select(CodesBase.Code).ToArray() ?? [] },
            ct);
    }

    public Task<IReadOnlyList<InstanceProcessus>> ObtenirEnfantsActifsAsync(IdInstance idParent, CancellationToken ct)
        => ListerAsync(
            $"SELECT {Colonnes} FROM {Table} WHERE ID_INSTANCE_PARENT = @id AND STATUT IN {CodesBase.StatutsActifs} ORDER BY ID",
            new { id = idParent.Valeur },
            ct);

    public Task<IReadOnlyList<InstanceProcessus>> ObtenirEnErreurAsync(int decalage, int taille, CancellationToken ct)
        => ListerAsync(
            sql.Dialecte.Paginer($"SELECT {Colonnes} FROM {Table} WHERE STATUT = 'EN_ERREUR' ORDER BY DATE_ERREUR DESC, ID", decalage, taille),
            null,
            ct);

    public async Task<IReadOnlyList<CompteInstancesActives>> CompterActivesAsync(string cleProcessus, CancellationToken ct)
    {
        var lignes = await sql.Connexion.QueryAsync<LigneCompte>(sql.Lecture(
            $"SELECT i.ID_DEFINITION AS ID_DEFINITION, d.VERSION AS VERSION, i.CODE_NOEUD_COURANT AS CODE_NOEUD, COUNT(*) AS NOMBRE " +
            $"FROM {Table} i JOIN {sql.Noms.Definition} d ON d.ID = i.ID_DEFINITION " +
            $"WHERE i.CLE_PROCESSUS = @cle AND i.STATUT IN {CodesBase.StatutsActifs} " +
            "GROUP BY i.ID_DEFINITION, d.VERSION, i.CODE_NOEUD_COURANT ORDER BY d.VERSION, i.CODE_NOEUD_COURANT",
            new { cle = cleProcessus },
            ct));
        return lignes.Select(l => new CompteInstancesActives(new IdDefinition(l.ID_DEFINITION), l.VERSION, l.CODE_NOEUD, l.NOMBRE)).ToList();
    }

    public Task<IReadOnlyList<InstanceProcessus>> ObtenirAuReposAsync(IdDefinition idDefinition, string? codeNoeud, int taille, CancellationToken ct)
    {
        var requete = $"SELECT {Colonnes} FROM {Table} WHERE ID_DEFINITION = @id AND STATUT IN {CodesBase.StatutsAuRepos}";
        if (codeNoeud is not null)
        {
            requete += " AND CODE_NOEUD_COURANT = @code";
        }

        return ListerAsync(sql.Dialecte.Limiter(requete + " ORDER BY ID", taille), new { id = idDefinition.Valeur, code = codeNoeud }, ct);
    }

    public Task RenommerNoeudParentAsync(IdInstance idParent, string ancienCode, string nouveauCode, CancellationToken ct)
        => sql.Connexion.ExecuteAsync(sql.Ecriture(
            $"UPDATE {Table} SET CODE_NOEUD_PARENT = @nouveau WHERE ID_INSTANCE_PARENT = @id AND CODE_NOEUD_PARENT = @ancien AND STATUT IN {CodesBase.StatutsActifs}",
            new { id = idParent.Valeur, ancien = ancienCode, nouveau = nouveauCode },
            ct));

    private async Task<IReadOnlyList<InstanceProcessus>> ListerAsync(string requete, object? parametres, CancellationToken ct)
        => (await sql.Connexion.QueryAsync<Ligne>(sql.Lecture(requete, parametres, ct))).Select(l => l.VersDomaine()).ToList();

    private sealed class LigneCompte
    {
        public long ID_DEFINITION { get; set; }

        public int VERSION { get; set; }

        public string CODE_NOEUD { get; set; } = "";

        public int NOMBRE { get; set; }
    }

    private sealed class Ligne
    {
        public long ID { get; set; }

        public long ID_DEFINITION { get; set; }

        public string CLE_PROCESSUS { get; set; } = "";

        public long? ID_INSTANCE_PARENT { get; set; }

        public string? CODE_NOEUD_PARENT { get; set; }

        public long CLE_AFFAIRE { get; set; }

        public int INSTANCE_UNIQUE { get; set; }

        public string STATUT { get; set; } = "";

        public string? CODE_FIN { get; set; }

        public string CODE_NOEUD_COURANT { get; set; } = "";

        public string? VARIABLES { get; set; }

        public string? MESSAGE_ERREUR { get; set; }

        public DateTime? DATE_ERREUR { get; set; }

        public int VERSION { get; set; }

        public DateTime DATE_CREATION { get; set; }

        public DateTime DATE_MAJ { get; set; }

        public DateTime? DATE_FIN { get; set; }

        public InstanceProcessus VersDomaine() => new()
        {
            Id = new IdInstance(ID),
            IdDefinition = new IdDefinition(ID_DEFINITION),
            CleProcessus = CLE_PROCESSUS,
            IdInstanceParent = ID_INSTANCE_PARENT is null ? null : new IdInstance(ID_INSTANCE_PARENT.Value),
            CodeNoeudParent = CODE_NOEUD_PARENT,
            CleAffaire = CLE_AFFAIRE,
            InstanceUnique = INSTANCE_UNIQUE != 0,
            Statut = CodesBase.LireStatutInstance(STATUT),
            CodeFin = CODE_FIN,
            CodeNoeudCourant = CODE_NOEUD_COURANT,
            VariablesJson = VARIABLES,
            MessageErreur = MESSAGE_ERREUR,
            DateErreur = Utc(DATE_ERREUR),
            Version = VERSION,
            DateCreation = Utc(DATE_CREATION),
            DateMaj = Utc(DATE_MAJ),
            DateFin = Utc(DATE_FIN),
        };
    }
}
