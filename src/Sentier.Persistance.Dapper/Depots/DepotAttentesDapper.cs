using Dapper;
using static Sentier.Persistance.Dapper.ContexteSql;

namespace Sentier.Persistance.Dapper.Depots;

/// <summary>Dépôt Dapper des attentes (une ligne par déclencheur).</summary>
internal sealed class DepotAttentesDapper(ContexteSql sql) : IDepotAttentes
{
    private const string Colonnes = "NO_SEQ_ATTEN_PROCS, NO_SEQ_INSTC_PROCS, ID_NOEUD, TYP_DECLN, NOM_SORTI, DH_ECHEA, NOM_SIGNL, CLE_CORRL, DH_CREA";

    private string Table => sql.Noms.Attente;

    public async Task InsererAsync(Attente attente, CancellationToken ct)
    {
        attente.Id = new IdAttente(await sql.ProchainIdAsync(sql.Noms.SeqAttente, ct));
        await sql.Connexion.ExecuteAsync(sql.Ecriture(
            $"INSERT INTO {Table} ({Colonnes}) VALUES (@id, @idInstance, @codeNoeud, @type, @nomSortie, @dateEcheance, @nomSignal, @cleCorrelation, @dateCreation)",
            new
            {
                id = attente.Id.Valeur,
                idInstance = attente.IdInstance.Valeur,
                codeNoeud = attente.CodeNoeud,
                type = CodesBase.Code(attente.TypeDeclencheur),
                nomSortie = attente.NomSortie,
                dateEcheance = attente.DateEcheance,
                nomSignal = attente.NomSignal,
                cleCorrelation = attente.CleCorrelation,
                dateCreation = attente.DateCreation,
            },
            ct));
    }

    public Task<IReadOnlyList<Attente>> ObtenirParInstanceAsync(IdInstance idInstance, CancellationToken ct)
        => ListerAsync($"SELECT {Colonnes} FROM {Table} WHERE NO_SEQ_INSTC_PROCS = @id ORDER BY NO_SEQ_ATTEN_PROCS", new { id = idInstance.Valeur }, ct);

    public async Task<Attente?> VerrouillerAsync(IdAttente id, CancellationToken ct)
    {
        var requete = $"SELECT {Colonnes} FROM {Table} WHERE NO_SEQ_ATTEN_PROCS = @id{sql.Dialecte.VerrouillageSansAttente}";
        var ligne = await sql.Connexion.QueryFirstOrDefaultAsync<Ligne>(sql.Ecriture(requete, new { id = id.Valeur }, ct));
        return ligne?.VersDomaine();
    }

    public Task<int> SupprimerParNoeudAsync(IdInstance idInstance, string codeNoeud, CancellationToken ct)
        => sql.Connexion.ExecuteAsync(sql.Ecriture(
            $"DELETE FROM {Table} WHERE NO_SEQ_INSTC_PROCS = @id AND ID_NOEUD = @code",
            new { id = idInstance.Valeur, code = codeNoeud },
            ct));

    public Task<int> SupprimerParInstanceAsync(IdInstance idInstance, CancellationToken ct)
        => sql.Connexion.ExecuteAsync(sql.Ecriture($"DELETE FROM {Table} WHERE NO_SEQ_INSTC_PROCS = @id", new { id = idInstance.Valeur }, ct));

    public async Task<IReadOnlyList<EcheanceDue>> ObtenirEcheancesDuesAsync(DateTime maintenant, int tailleMax, CancellationToken ct)
    {
        var requete = sql.Dialecte.Limiter(
            $"SELECT a.NO_SEQ_ATTEN_PROCS AS ID_ATTENTE, a.NO_SEQ_INSTC_PROCS AS NO_SEQ_INSTC_PROCS, i.CLE_DEFIN AS CLE_DEFIN, i.AGGRE_ID AS AGGRE_ID, a.DH_ECHEA AS DH_ECHEA " +
            $"FROM {Table} a JOIN {sql.Noms.Instance} i ON i.NO_SEQ_INSTC_PROCS = a.NO_SEQ_INSTC_PROCS " +
            "WHERE a.TYP_DECLN = 'ECHEANCE' AND a.DH_ECHEA <= @maintenant AND i.STAT = 'SUSPENDUE' " +
            "ORDER BY a.DH_ECHEA, a.NO_SEQ_ATTEN_PROCS",
            tailleMax);
        var lignes = await sql.Connexion.QueryAsync<LigneEcheance>(sql.Lecture(requete, new { maintenant }, ct));
        return lignes.Select(l => new EcheanceDue(new IdAttente(l.ID_ATTENTE), new IdInstance(l.NO_SEQ_INSTC_PROCS), l.CLE_DEFIN, l.AGGRE_ID, Utc(l.DH_ECHEA))).ToList();
    }

    public Task<IReadOnlyList<Attente>> RechercherSignalAsync(Signal signal, CancellationToken ct)
    {
        var colonnes = string.Join(", ", Colonnes.Split(", ").Select(c => "a." + c));
        var requete =
            $"SELECT {colonnes} FROM {Table} a JOIN {sql.Noms.Instance} i ON i.NO_SEQ_INSTC_PROCS = a.NO_SEQ_INSTC_PROCS " +
            $"WHERE a.TYP_DECLN = 'SIGNAL' AND a.NOM_SIGNL = @nom AND i.STAT IN {CodesBase.StatutsAuRepos} AND i.ID_NOEUD_COUR = a.ID_NOEUD";
        if (signal.CleProcessus is not null)
        {
            requete += " AND i.CLE_DEFIN = @cleProcessus AND i.AGGRE_ID = @cleAffaire";
        }

        if (signal.CleCorrelation is not null)
        {
            requete += " AND a.CLE_CORRL = @cleCorrelation";
        }

        return ListerAsync(
            requete + " ORDER BY a.NO_SEQ_ATTEN_PROCS",
            new { nom = signal.Nom, cleProcessus = signal.CleProcessus, cleAffaire = signal.CleAffaire, cleCorrelation = signal.CleCorrelation },
            ct);
    }

    public Task RenommerNoeudAsync(IdInstance idInstance, string ancienCode, string nouveauCode, CancellationToken ct)
        => sql.Connexion.ExecuteAsync(sql.Ecriture(
            $"UPDATE {Table} SET ID_NOEUD = @nouveau WHERE NO_SEQ_INSTC_PROCS = @id AND ID_NOEUD = @ancien",
            new { id = idInstance.Valeur, ancien = ancienCode, nouveau = nouveauCode },
            ct));

    private async Task<IReadOnlyList<Attente>> ListerAsync(string requete, object parametres, CancellationToken ct)
        => (await sql.Connexion.QueryAsync<Ligne>(sql.Lecture(requete, parametres, ct))).Select(l => l.VersDomaine()).ToList();

    private sealed class LigneEcheance
    {
        public long ID_ATTENTE { get; set; }

        public long NO_SEQ_INSTC_PROCS { get; set; }

        public string CLE_DEFIN { get; set; } = "";

        public long AGGRE_ID { get; set; }

        public DateTime DH_ECHEA { get; set; }
    }

    private sealed class Ligne
    {
        public long NO_SEQ_ATTEN_PROCS { get; set; }

        public long NO_SEQ_INSTC_PROCS { get; set; }

        public string ID_NOEUD { get; set; } = "";

        public string TYP_DECLN { get; set; } = "";

        public string NOM_SORTI { get; set; } = "";

        public DateTime? DH_ECHEA { get; set; }

        public string? NOM_SIGNL { get; set; }

        public string? CLE_CORRL { get; set; }

        public DateTime DH_CREA { get; set; }

        public Attente VersDomaine() => new()
        {
            Id = new IdAttente(NO_SEQ_ATTEN_PROCS),
            IdInstance = new IdInstance(NO_SEQ_INSTC_PROCS),
            CodeNoeud = ID_NOEUD,
            TypeDeclencheur = CodesBase.LireTypeDeclencheur(TYP_DECLN),
            NomSortie = NOM_SORTI,
            DateEcheance = Utc(DH_ECHEA),
            NomSignal = NOM_SIGNL,
            CleCorrelation = CLE_CORRL,
            DateCreation = Utc(DH_CREA),
        };
    }
}
