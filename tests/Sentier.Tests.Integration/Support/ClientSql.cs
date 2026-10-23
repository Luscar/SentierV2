using System.Data;
using System.Text.Json;
using Dapper;

namespace Sentier.Tests.Integration.Support;

/// <summary>Tables de l'application cliente, dans la même base (et la même transaction) que Sentier.</summary>
public static class SchemaClient
{
    public static async Task InstallerAsync(IDbConnection connexion, IDbTransaction transaction)
    {
        string[] instructions =
        [
            "CREATE TABLE CLIENT_EMPLOYE (ID INTEGER PRIMARY KEY, NOM TEXT NOT NULL, SOLDE INTEGER NOT NULL)",
            "CREATE TABLE CLIENT_JOURNAL (ID INTEGER PRIMARY KEY AUTOINCREMENT, ID_EMPLOYE INTEGER NOT NULL, EVENEMENT TEXT NOT NULL, DETAIL TEXT, UTILISATEUR TEXT)",
            "CREATE TABLE CLIENT_ARCHIVE (NUMERO TEXT PRIMARY KEY, ID_EMPLOYE INTEGER NOT NULL, JOURS INTEGER NOT NULL)",
            "CREATE TABLE CLIENT_TACHE (ID INTEGER PRIMARY KEY AUTOINCREMENT, ID_INSTANCE INTEGER NOT NULL, CODE_NOEUD TEXT NOT NULL, NOM TEXT NOT NULL, " +
            "ROLE TEXT, ASSIGNE TEXT, DH_ECHEA TEXT, DONNEES TEXT, STATUT TEXT NOT NULL, COMPLETEE_PAR TEXT, DONNEES_COMPLETION TEXT, MOTIF TEXT)",
        ];

        foreach (var instruction in instructions)
        {
            await connexion.ExecuteAsync(instruction, transaction: transaction);
        }
    }
}

/// <summary>Tâche telle que stockée par le client.</summary>
public sealed record TacheClient(long Id, long IdInstance, string CodeNoeud, string Nom, string? Role, string? Assigne, string? DonneesJson, string Statut, string? CompleteePar, string? DonneesCompletion, string? Motif)
{
    public T Donnee<T>(string nom) => JsonDocument.Parse(DonneesJson!).RootElement.GetProperty(nom).Deserialize<T>()!;
}

/// <summary>
/// Gestionnaire de tâches du client, en base, dans la même session (donc la même transaction) que Sentier :
/// c'est l'implémentation recommandée par le README.
/// </summary>
public sealed class GestionnaireTachesSql(ISessionSentier session) : IGestionnaireTaches
{
    public Task<long> CreerTacheAsync(DemandeTacheUtilisateur demande, CancellationToken ct = default)
        => session.Connexion.ExecuteScalarAsync<long>(new CommandDefinition(
            "INSERT INTO CLIENT_TACHE (ID_INSTANCE, CODE_NOEUD, NOM, ROLE, ASSIGNE, DH_ECHEA, DONNEES, STATUT) " +
            "VALUES (@IdInstance, @CodeNoeud, @Nom, @Role, @Utilisateur, @DateEcheance, @Donnees, 'OUVERTE'); SELECT last_insert_rowid();",
            new
            {
                IdInstance = demande.IdInstance.Valeur,
                demande.CodeNoeud,
                demande.Nom,
                demande.Role,
                demande.Utilisateur,
                demande.DateEcheance,
                Donnees = JsonSerializer.Serialize(demande.Donnees),
            },
            session.ExigerTransaction(),
            cancellationToken: ct));

    public async Task<TacheUtilisateur?> ObtenirTacheAsync(long idTache, CancellationToken ct = default)
        => (await LireAsync("WHERE ID = @idTache", new { idTache }, ct)).Select(VersSentier).FirstOrDefault();

    public async Task<IReadOnlyList<TacheUtilisateur>> ObtenirTachesOuvertesAsync(IdInstance idInstance, CancellationToken ct = default)
        => (await LireAsync("WHERE ID_INSTANCE = @id AND STATUT = 'OUVERTE'", new { id = idInstance.Valeur }, ct)).Select(VersSentier).ToList();

    public Task CompleterTacheAsync(long idTache, IReadOnlyDictionary<string, object?>? donnees, string? utilisateur, CancellationToken ct = default)
        => EcrireAsync(
            "UPDATE CLIENT_TACHE SET STATUT = 'COMPLETEE', COMPLETEE_PAR = @utilisateur, DONNEES_COMPLETION = @donnees WHERE ID = @idTache",
            new { idTache, utilisateur, donnees = donnees is null ? null : JsonSerializer.Serialize(donnees) },
            ct);

    public Task AnnulerTacheAsync(long idTache, string motif, CancellationToken ct = default)
        => EcrireAsync("UPDATE CLIENT_TACHE SET STATUT = 'ANNULEE', MOTIF = @motif WHERE ID = @idTache AND STATUT = 'OUVERTE'", new { idTache, motif }, ct);

    public Task RenommerNoeudAsync(IdInstance idInstance, string ancienCode, string nouveauCode, CancellationToken ct = default)
        => EcrireAsync(
            "UPDATE CLIENT_TACHE SET CODE_NOEUD = @nouveauCode WHERE ID_INSTANCE = @id AND CODE_NOEUD = @ancienCode AND STATUT = 'OUVERTE'",
            new { id = idInstance.Valeur, ancienCode, nouveauCode },
            ct);

    /// <summary>Lecture directe, pour les assertions des tests.</summary>
    public static TacheClient Lire(IDbConnection connexion, long idTache)
        => connexion.QuerySingle<TacheClient>(Selection + " WHERE ID = @idTache", new { idTache });

    public static IReadOnlyList<TacheClient> LireParInstance(IDbConnection connexion, IdInstance idInstance)
        => connexion.Query<TacheClient>(Selection + " WHERE ID_INSTANCE = @id ORDER BY ID", new { id = idInstance.Valeur }).ToList();

    private const string Selection =
        "SELECT ID AS Id, ID_INSTANCE AS IdInstance, CODE_NOEUD AS CodeNoeud, NOM AS Nom, ROLE AS Role, ASSIGNE AS Assigne, DONNEES AS DonneesJson, " +
        "STATUT AS Statut, COMPLETEE_PAR AS CompleteePar, DONNEES_COMPLETION AS DonneesCompletion, MOTIF AS Motif FROM CLIENT_TACHE";

    private async Task<IEnumerable<TacheClient>> LireAsync(string filtre, object parametres, CancellationToken ct)
        => await session.Connexion.QueryAsync<TacheClient>(new CommandDefinition(Selection + " " + filtre, parametres, session.Transaction, cancellationToken: ct));

    private Task EcrireAsync(string sql, object parametres, CancellationToken ct)
        => session.Connexion.ExecuteAsync(new CommandDefinition(sql, parametres, session.ExigerTransaction(), cancellationToken: ct));

    private static TacheUtilisateur VersSentier(TacheClient t) => new()
    {
        Id = t.Id,
        IdInstance = new IdInstance(t.IdInstance),
        CodeNoeud = t.CodeNoeud,
        Statut = t.Statut switch
        {
            "OUVERTE" => StatutTache.Ouverte,
            "COMPLETEE" => StatutTache.Completee,
            _ => StatutTache.Annulee,
        },
    };
}
