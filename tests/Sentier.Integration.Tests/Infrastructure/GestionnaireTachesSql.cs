using System.Text.Json;
using Dapper;

namespace Sentier.Integration.Tests.Infrastructure;

/// <summary>
/// Gestionnaire de tâches « client » : les tâches sont stockées dans une table de la même base et écrites dans la
/// transaction de Sentier, comme le ferait une application réelle. Une transaction annulée annule donc aussi la tâche.
/// </summary>
public sealed class GestionnaireTachesSql(ISessionSentier session) : IGestionnaireTachesSentier
{
    public const string Table = "TEST_TACHES";

    public const string ScriptCreation = $"""
        CREATE TABLE {Table} (
            ID INTEGER PRIMARY KEY AUTOINCREMENT,
            ID_INSTANCE INTEGER NOT NULL,
            CLE_PROCESSUS TEXT NOT NULL,
            CLE_AFFAIRE INTEGER NOT NULL,
            CODE_NOEUD TEXT NOT NULL,
            NOM_TACHE TEXT NOT NULL,
            ROLE TEXT NULL,
            ASSIGNE TEXT NULL,
            DATE_ECHEANCE TEXT NULL,
            DONNEES TEXT NULL,
            STATUT TEXT NOT NULL,
            COMPLETEE_PAR TEXT NULL,
            MOTIF_ANNULATION TEXT NULL
        )
        """;

    public async Task<long> CreerTacheAsync(DemandeTacheSentier demande, CancellationToken ct = default)
        => await session.Connexion.ExecuteScalarAsync<long>(
            $"""
            INSERT INTO {Table} (ID_INSTANCE, CLE_PROCESSUS, CLE_AFFAIRE, CODE_NOEUD, NOM_TACHE, ROLE, ASSIGNE, DATE_ECHEANCE, DONNEES, STATUT)
            VALUES (@IdInstance, @CleProcessus, @CleAffaire, @CodeNoeud, @NomTache, @Role, @Assigne, @DateEcheance, @Donnees, @Statut);
            SELECT last_insert_rowid();
            """,
            new
            {
                IdInstance = demande.IdInstance.Valeur,
                demande.CleProcessus,
                demande.CleAffaire,
                demande.CodeNoeud,
                demande.NomTache,
                demande.Role,
                demande.Assigne,
                DateEcheance = demande.DateEcheance?.ToString("O"),
                Donnees = JsonSerializer.Serialize(demande.Donnees),
                Statut = nameof(StatutTacheSentier.Ouverte),
            },
            session.Transaction);

    public async Task<TacheSentier?> ObtenirTacheAsync(long idTache, CancellationToken ct = default)
        => (await LireAsync("ID = @idTache", new { idTache })).Select(t => t.VersTache()).SingleOrDefault();

    public async Task<IReadOnlyList<TacheSentier>> ObtenirTachesOuvertesAsync(IdInstance idInstance, CancellationToken ct = default)
        => (await LireAsync("ID_INSTANCE = @id AND STATUT = 'Ouverte'", new { id = idInstance.Valeur })).Select(t => t.VersTache()).ToList();

    public Task CompleterTacheAsync(long idTache, IReadOnlyDictionary<string, object?>? donnees, string? utilisateur, CancellationToken ct = default)
        => Executer($"UPDATE {Table} SET STATUT = 'Completee', COMPLETEE_PAR = @utilisateur WHERE ID = @idTache", new { idTache, utilisateur });

    public Task AssignerTacheAsync(long idTache, string utilisateur, CancellationToken ct = default)
        => Executer($"UPDATE {Table} SET ASSIGNE = @utilisateur WHERE ID = @idTache", new { idTache, utilisateur });

    public Task AnnulerTacheAsync(long idTache, string motif, CancellationToken ct = default)
        => Executer($"UPDATE {Table} SET STATUT = 'Annulee', MOTIF_ANNULATION = @motif WHERE ID = @idTache", new { idTache, motif });

    public Task RenommerNoeudAsync(IdInstance idInstance, string ancienCode, string nouveauCode, CancellationToken ct = default)
        => Executer($"UPDATE {Table} SET CODE_NOEUD = @nouveauCode WHERE ID_INSTANCE = @id AND CODE_NOEUD = @ancienCode", new { id = idInstance.Valeur, ancienCode, nouveauCode });

    /// <summary>Lecture complète d'une tâche, pour les assertions des tests.</summary>
    public async Task<LigneTache?> LireLigneAsync(long idTache)
        => (await LireAsync("ID = @idTache", new { idTache })).SingleOrDefault();

    /// <summary>Toutes les tâches d'une instance, quel que soit leur statut.</summary>
    public async Task<IReadOnlyList<LigneTache>> LireLignesAsync(IdInstance idInstance)
        => (await LireAsync("ID_INSTANCE = @id", new { id = idInstance.Valeur })).ToList();

    private Task<IEnumerable<LigneTache>> LireAsync(string filtre, object parametres)
        => session.Connexion.QueryAsync<LigneTache>(
            $"""
            SELECT ID AS Id, ID_INSTANCE AS IdInstance, CLE_PROCESSUS AS CleProcessus, CLE_AFFAIRE AS CleAffaire,
                   CODE_NOEUD AS CodeNoeud, NOM_TACHE AS NomTache, ROLE AS Role, ASSIGNE AS Assigne,
                   DONNEES AS Donnees, STATUT AS Statut, COMPLETEE_PAR AS CompleteePar, MOTIF_ANNULATION AS MotifAnnulation
            FROM {Table} WHERE {filtre} ORDER BY ID
            """,
            parametres,
            session.Transaction);

    private Task Executer(string sql, object parametres) => session.Connexion.ExecuteAsync(sql, parametres, session.Transaction);

    public sealed class LigneTache
    {
        public long Id { get; init; }
        public long IdInstance { get; init; }
        public string CleProcessus { get; init; } = "";
        public long CleAffaire { get; init; }
        public string CodeNoeud { get; init; } = "";
        public string NomTache { get; init; } = "";
        public string? Role { get; init; }
        public string? Assigne { get; init; }
        public string? Donnees { get; init; }
        public string Statut { get; init; } = "";
        public string? CompleteePar { get; init; }
        public string? MotifAnnulation { get; init; }

        public TacheSentier VersTache() => new()
        {
            Id = Id,
            IdInstance = Sentier.IdInstance.Depuis(IdInstance),
            CodeNoeud = CodeNoeud,
            Statut = Enum.Parse<StatutTacheSentier>(Statut),
        };
    }
}
