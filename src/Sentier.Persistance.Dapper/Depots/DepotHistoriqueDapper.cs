using Dapper;
using static Sentier.Persistance.Dapper.ContexteSql;

namespace Sentier.Persistance.Dapper.Depots;

/// <summary>Dépôt Dapper de l'historique des passages.</summary>
internal sealed class DepotHistoriqueDapper(ContexteSql sql) : IDepotHistorique
{
    private const string Colonnes = "ID, ID_INSTANCE, CODE_NOEUD, TYPE_NOEUD, DATE_ENTREE, DATE_SORTIE, RESULTAT";

    private string Table => sql.Noms.HistNoeud;

    public async Task InsererAsync(PassageNoeud passage, CancellationToken ct)
    {
        passage.Id = new IdPassageNoeud(await sql.ProchainIdAsync(sql.Noms.SeqHistNoeud, ct));
        await sql.Connexion.ExecuteAsync(sql.Ecriture(
            $"INSERT INTO {Table} ({Colonnes}) VALUES (@id, @idInstance, @codeNoeud, @typeNoeud, @dateEntree, @dateSortie, @resultat)",
            new
            {
                id = passage.Id.Valeur,
                idInstance = passage.IdInstance.Valeur,
                codeNoeud = passage.CodeNoeud,
                typeNoeud = CodesBase.Code(passage.TypeNoeud),
                dateEntree = passage.DateEntree,
                dateSortie = passage.DateSortie,
                resultat = passage.Resultat,
            },
            ct));
    }

    public Task FermerPassagesOuvertsAsync(IdInstance idInstance, DateTime dateSortie, string? resultat, CancellationToken ct)
        => sql.Connexion.ExecuteAsync(sql.Ecriture(
            $"UPDATE {Table} SET DATE_SORTIE = @dateSortie, RESULTAT = @resultat WHERE ID_INSTANCE = @id AND DATE_SORTIE IS NULL",
            new { id = idInstance.Valeur, dateSortie, resultat },
            ct));

    public async Task<IReadOnlyList<PassageNoeud>> ObtenirAsync(IdInstance idInstance, CancellationToken ct)
    {
        var lignes = await sql.Connexion.QueryAsync<Ligne>(sql.Lecture(
            $"SELECT {Colonnes} FROM {Table} WHERE ID_INSTANCE = @id ORDER BY ID",
            new { id = idInstance.Valeur },
            ct));
        return lignes.Select(l => new PassageNoeud
        {
            Id = new IdPassageNoeud(l.ID),
            IdInstance = new IdInstance(l.ID_INSTANCE),
            CodeNoeud = l.CODE_NOEUD,
            TypeNoeud = CodesBase.LireTypeNoeud(l.TYPE_NOEUD),
            DateEntree = Utc(l.DATE_ENTREE),
            DateSortie = Utc(l.DATE_SORTIE),
            Resultat = l.RESULTAT,
        }).ToList();
    }

    private sealed class Ligne
    {
        public long ID { get; set; }

        public long ID_INSTANCE { get; set; }

        public string CODE_NOEUD { get; set; } = "";

        public string TYPE_NOEUD { get; set; } = "";

        public DateTime DATE_ENTREE { get; set; }

        public DateTime? DATE_SORTIE { get; set; }

        public string? RESULTAT { get; set; }
    }
}
