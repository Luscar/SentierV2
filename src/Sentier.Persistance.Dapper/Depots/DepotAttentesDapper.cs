using Dapper;
using static Sentier.Persistance.Dapper.ContexteSql;

namespace Sentier.Persistance.Dapper.Depots;

/// <summary>Dépôt Dapper des attentes (une ligne par déclencheur).</summary>
internal sealed class DepotAttentesDapper(ContexteSql sql) : IDepotAttentes
{
    private const string Colonnes = "ID, ID_INSTANCE, CODE_NOEUD, TYPE_DECLENCHEUR, NOM_SORTIE, DATE_ECHEANCE, NOM_SIGNAL, CLE_CORRELATION, DATE_CREATION";

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
        => ListerAsync($"SELECT {Colonnes} FROM {Table} WHERE ID_INSTANCE = @id ORDER BY ID", new { id = idInstance.Valeur }, ct);

    public async Task<Attente?> VerrouillerAsync(IdAttente id, CancellationToken ct)
    {
        var requete = $"SELECT {Colonnes} FROM {Table} WHERE ID = @id{sql.Dialecte.VerrouillageSansAttente}";
        var ligne = await sql.Connexion.QueryFirstOrDefaultAsync<Ligne>(sql.Ecriture(requete, new { id = id.Valeur }, ct));
        return ligne?.VersDomaine();
    }

    public Task<int> SupprimerParNoeudAsync(IdInstance idInstance, string codeNoeud, CancellationToken ct)
        => sql.Connexion.ExecuteAsync(sql.Ecriture(
            $"DELETE FROM {Table} WHERE ID_INSTANCE = @id AND CODE_NOEUD = @code",
            new { id = idInstance.Valeur, code = codeNoeud },
            ct));

    public Task<int> SupprimerParInstanceAsync(IdInstance idInstance, CancellationToken ct)
        => sql.Connexion.ExecuteAsync(sql.Ecriture($"DELETE FROM {Table} WHERE ID_INSTANCE = @id", new { id = idInstance.Valeur }, ct));

    public async Task<IReadOnlyList<EcheanceDue>> ObtenirEcheancesDuesAsync(DateTime maintenant, int tailleMax, CancellationToken ct)
    {
        var requete = sql.Dialecte.Limiter(
            $"SELECT a.ID AS ID_ATTENTE, a.ID_INSTANCE AS ID_INSTANCE, i.CLE_PROCESSUS AS CLE_PROCESSUS, i.CLE_AFFAIRE AS CLE_AFFAIRE, a.DATE_ECHEANCE AS DATE_ECHEANCE " +
            $"FROM {Table} a JOIN {sql.Noms.Instance} i ON i.ID = a.ID_INSTANCE " +
            "WHERE a.TYPE_DECLENCHEUR = 'ECHEANCE' AND a.DATE_ECHEANCE <= @maintenant AND i.STATUT = 'SUSPENDUE' " +
            "ORDER BY a.DATE_ECHEANCE, a.ID",
            tailleMax);
        var lignes = await sql.Connexion.QueryAsync<LigneEcheance>(sql.Lecture(requete, new { maintenant }, ct));
        return lignes.Select(l => new EcheanceDue(new IdAttente(l.ID_ATTENTE), new IdInstance(l.ID_INSTANCE), l.CLE_PROCESSUS, l.CLE_AFFAIRE, Utc(l.DATE_ECHEANCE))).ToList();
    }

    public Task<IReadOnlyList<Attente>> RechercherSignalAsync(Signal signal, CancellationToken ct)
    {
        var colonnes = string.Join(", ", Colonnes.Split(", ").Select(c => "a." + c));
        var requete =
            $"SELECT {colonnes} FROM {Table} a JOIN {sql.Noms.Instance} i ON i.ID = a.ID_INSTANCE " +
            $"WHERE a.TYPE_DECLENCHEUR = 'SIGNAL' AND a.NOM_SIGNAL = @nom AND i.STATUT IN {CodesBase.StatutsAuRepos} AND i.CODE_NOEUD_COURANT = a.CODE_NOEUD";
        if (signal.CleProcessus is not null)
        {
            requete += " AND i.CLE_PROCESSUS = @cleProcessus AND i.CLE_AFFAIRE = @cleAffaire";
        }

        if (signal.CleCorrelation is not null)
        {
            requete += " AND a.CLE_CORRELATION = @cleCorrelation";
        }

        return ListerAsync(
            requete + " ORDER BY a.ID",
            new { nom = signal.Nom, cleProcessus = signal.CleProcessus, cleAffaire = signal.CleAffaire, cleCorrelation = signal.CleCorrelation },
            ct);
    }

    public Task RenommerNoeudAsync(IdInstance idInstance, string ancienCode, string nouveauCode, CancellationToken ct)
        => sql.Connexion.ExecuteAsync(sql.Ecriture(
            $"UPDATE {Table} SET CODE_NOEUD = @nouveau WHERE ID_INSTANCE = @id AND CODE_NOEUD = @ancien",
            new { id = idInstance.Valeur, ancien = ancienCode, nouveau = nouveauCode },
            ct));

    private async Task<IReadOnlyList<Attente>> ListerAsync(string requete, object parametres, CancellationToken ct)
        => (await sql.Connexion.QueryAsync<Ligne>(sql.Lecture(requete, parametres, ct))).Select(l => l.VersDomaine()).ToList();

    private sealed class LigneEcheance
    {
        public long ID_ATTENTE { get; set; }

        public long ID_INSTANCE { get; set; }

        public string CLE_PROCESSUS { get; set; } = "";

        public long CLE_AFFAIRE { get; set; }

        public DateTime DATE_ECHEANCE { get; set; }
    }

    private sealed class Ligne
    {
        public long ID { get; set; }

        public long ID_INSTANCE { get; set; }

        public string CODE_NOEUD { get; set; } = "";

        public string TYPE_DECLENCHEUR { get; set; } = "";

        public string NOM_SORTIE { get; set; } = "";

        public DateTime? DATE_ECHEANCE { get; set; }

        public string? NOM_SIGNAL { get; set; }

        public string? CLE_CORRELATION { get; set; }

        public DateTime DATE_CREATION { get; set; }

        public Attente VersDomaine() => new()
        {
            Id = new IdAttente(ID),
            IdInstance = new IdInstance(ID_INSTANCE),
            CodeNoeud = CODE_NOEUD,
            TypeDeclencheur = CodesBase.LireTypeDeclencheur(TYPE_DECLENCHEUR),
            NomSortie = NOM_SORTIE,
            DateEcheance = Utc(DATE_ECHEANCE),
            NomSignal = NOM_SIGNAL,
            CleCorrelation = CLE_CORRELATION,
            DateCreation = Utc(DATE_CREATION),
        };
    }
}
