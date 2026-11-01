using static Sentier.Persistance.ContexteSql;

namespace Sentier.Persistance.Depots;

/// <summary>Dépôt Dapper de l'historique des passages.</summary>
internal sealed class DepotHistoriqueDapper(ContexteSql sql) : IDepotHistorique
{
    private const string Colonnes = "NO_SEQ_HIST_NOEUD_PROCS, NO_SEQ_INSTC_PROCS, ID_NOEUD, TYP_NOEUD, DH_ENTRE, DH_SORTI, RESUL";

    private string Table => sql.Noms.HistNoeud;

    public async Task InsererAsync(PassageNoeud passage, CancellationToken ct)
    {
        passage.Id = new IdPassageNoeud(await sql.ProchainIdAsync(sql.Noms.SeqHistNoeud, ct));
        await sql.ExecuterAsync(
            $"INSERT INTO {Table} ({Colonnes}) VALUES (@id, @idInstance, @codeNoeud, @typeNoeud, @dateEntree, @dateSortie, @resultat)",
            new
            {
                id = passage.Id.Valeur,
                idInstance = passage.IdInstance.Valeur,
                codeNoeud = passage.CodeNoeud,
                typeNoeud = CodesBaseDonnees.Code(passage.TypeNoeud),
                dateEntree = passage.DateEntree,
                dateSortie = passage.DateSortie,
                resultat = passage.Resultat,
            },
            ct);
    }

    public Task FermerPassagesOuvertsAsync(IdInstance idInstance, DateTime dateSortie, string? resultat, CancellationToken ct)
        => sql.ExecuterAsync(
            $"UPDATE {Table} SET DH_SORTI = @dateSortie, RESUL = @resultat WHERE NO_SEQ_INSTC_PROCS = @id AND DH_SORTI IS NULL",
            new { id = idInstance.Valeur, dateSortie, resultat },
            ct);

    public async Task<IReadOnlyList<PassageNoeud>> ObtenirAsync(IdInstance idInstance, CancellationToken ct)
    {
        var lignes = await sql.LireAsync<Ligne>(
            $"SELECT {Colonnes} FROM {Table} WHERE NO_SEQ_INSTC_PROCS = @id ORDER BY NO_SEQ_HIST_NOEUD_PROCS",
            new { id = idInstance.Valeur },
            ct);
        return lignes.Select(l => new PassageNoeud
        {
            Id = new IdPassageNoeud(l.NO_SEQ_HIST_NOEUD_PROCS),
            IdInstance = new IdInstance(l.NO_SEQ_INSTC_PROCS),
            CodeNoeud = l.ID_NOEUD,
            TypeNoeud = CodesBaseDonnees.LireTypeNoeud(l.TYP_NOEUD),
            DateEntree = Utc(l.DH_ENTRE),
            DateSortie = Utc(l.DH_SORTI),
            Resultat = l.RESUL,
        }).ToList();
    }

    private sealed class Ligne
    {
        public long NO_SEQ_HIST_NOEUD_PROCS { get; set; }

        public long NO_SEQ_INSTC_PROCS { get; set; }

        public string ID_NOEUD { get; set; } = "";

        public string TYP_NOEUD { get; set; } = "";

        public DateTime DH_ENTRE { get; set; }

        public DateTime? DH_SORTI { get; set; }

        public string? RESUL { get; set; }
    }
}
