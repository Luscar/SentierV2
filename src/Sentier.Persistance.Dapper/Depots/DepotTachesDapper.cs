using Dapper;
using static Sentier.Persistance.Dapper.ContexteSql;

namespace Sentier.Persistance.Dapper.Depots;

/// <summary>Dépôt Dapper des tâches utilisateurs.</summary>
internal sealed class DepotTachesDapper(ContexteSql sql) : IDepotTaches
{
    private const string Colonnes = "ID, ID_INSTANCE, CODE_NOEUD, ID_TACHE_EXTERNE, STATUT, DATE_CREATION, DATE_COMPLETION";

    private string Table => sql.Noms.Tache;

    public async Task InsererAsync(TacheUtilisateur tache, CancellationToken ct)
    {
        tache.Id = new IdTache(await sql.ProchainIdAsync(sql.Noms.SeqTache, ct));
        await sql.Connexion.ExecuteAsync(sql.Ecriture(
            $"INSERT INTO {Table} ({Colonnes}) VALUES (@id, @idInstance, @codeNoeud, @idExterne, @statut, @dateCreation, @dateCompletion)",
            new
            {
                id = tache.Id.Valeur,
                idInstance = tache.IdInstance.Valeur,
                codeNoeud = tache.CodeNoeud,
                idExterne = tache.IdTacheExterne,
                statut = CodesBase.Code(tache.Statut),
                dateCreation = tache.DateCreation,
                dateCompletion = tache.DateCompletion,
            },
            ct));
    }

    public async Task<TacheUtilisateur?> ObtenirParIdExterneAsync(long idTacheExterne, CancellationToken ct)
    {
        var requete = sql.Dialecte.Limiter($"SELECT {Colonnes} FROM {Table} WHERE ID_TACHE_EXTERNE = @id ORDER BY ID DESC", 1);
        var ligne = await sql.Connexion.QueryFirstOrDefaultAsync<Ligne>(sql.Lecture(requete, new { id = idTacheExterne }, ct));
        return ligne?.VersDomaine();
    }

    public async Task<IReadOnlyList<TacheUtilisateur>> ObtenirOuvertesAsync(IdInstance idInstance, CancellationToken ct)
    {
        var lignes = await sql.Connexion.QueryAsync<Ligne>(sql.Lecture(
            $"SELECT {Colonnes} FROM {Table} WHERE ID_INSTANCE = @id AND STATUT = 'OUVERTE' ORDER BY ID",
            new { id = idInstance.Valeur },
            ct));
        return lignes.Select(l => l.VersDomaine()).ToList();
    }

    public Task MettreAJourAsync(TacheUtilisateur tache, CancellationToken ct)
        => sql.Connexion.ExecuteAsync(sql.Ecriture(
            $"UPDATE {Table} SET STATUT = @statut, DATE_COMPLETION = @dateCompletion WHERE ID = @id",
            new { id = tache.Id.Valeur, statut = CodesBase.Code(tache.Statut), dateCompletion = tache.DateCompletion },
            ct));

    public Task RenommerNoeudAsync(IdInstance idInstance, string ancienCode, string nouveauCode, CancellationToken ct)
        => sql.Connexion.ExecuteAsync(sql.Ecriture(
            $"UPDATE {Table} SET CODE_NOEUD = @nouveau WHERE ID_INSTANCE = @id AND CODE_NOEUD = @ancien AND STATUT = 'OUVERTE'",
            new { id = idInstance.Valeur, ancien = ancienCode, nouveau = nouveauCode },
            ct));

    private sealed class Ligne
    {
        public long ID { get; set; }

        public long ID_INSTANCE { get; set; }

        public string CODE_NOEUD { get; set; } = "";

        public long ID_TACHE_EXTERNE { get; set; }

        public string STATUT { get; set; } = "";

        public DateTime DATE_CREATION { get; set; }

        public DateTime? DATE_COMPLETION { get; set; }

        public TacheUtilisateur VersDomaine() => new()
        {
            Id = new IdTache(ID),
            IdInstance = new IdInstance(ID_INSTANCE),
            CodeNoeud = CODE_NOEUD,
            IdTacheExterne = ID_TACHE_EXTERNE,
            Statut = CodesBase.LireStatutTache(STATUT),
            DateCreation = Utc(DATE_CREATION),
            DateCompletion = Utc(DATE_COMPLETION),
        };
    }
}
