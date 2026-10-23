using System.Data;
using Dapper;
using Sentier.Persistance.Dapper;

namespace Sentier.Persistance.Sqlite;

/// <summary>
/// Dialecte SQLite : paramètres <c>@p</c>, séquences émulées par la table <c>{Prefixe}_SEQ</c>
/// incrémentée dans la transaction, pas de verrouillage de ligne, <c>LIMIT</c>/<c>OFFSET</c>.
/// </summary>
public sealed class DialecteSqlite : DialecteSqlBase
{
    /// <inheritdoc />
    public override DialecteBase Type => DialecteBase.Sqlite;

    /// <inheritdoc />
    public override string VerrouillageSansAttente => "";

    /// <inheritdoc />
    protected override string RessourceScript => "Sentier.Persistance.Sqlite.Scripts.sqlite.sql";

    /// <inheritdoc />
    public override string Parametre(string nom) => "@" + nom;

    /// <inheritdoc />
    public override async Task<long> ProchainIdentifiantAsync(IDbConnection connexion, IDbTransaction transaction, NomsObjets noms, string nomSequence, CancellationToken ct)
    {
        var table = noms.TableSequences;
        var lignes = await connexion.ExecuteAsync(new CommandDefinition(
            $"UPDATE {table} SET VAL_SEQ = VAL_SEQ + 1 WHERE NOM_SEQ = @nom", new { nom = nomSequence }, transaction, cancellationToken: ct));
        if (lignes == 0)
        {
            await connexion.ExecuteAsync(new CommandDefinition(
                $"INSERT INTO {table} (NOM_SEQ, VAL_SEQ) VALUES (@nom, 1)", new { nom = nomSequence }, transaction, cancellationToken: ct));
            return 1;
        }

        return await connexion.ExecuteScalarAsync<long>(new CommandDefinition(
            $"SELECT VAL_SEQ FROM {table} WHERE NOM_SEQ = @nom", new { nom = nomSequence }, transaction, cancellationToken: ct));
    }

    /// <inheritdoc />
    public override string Limiter(string sql, int taille) => $"{sql} LIMIT {taille}";

    /// <inheritdoc />
    public override string Paginer(string sql, int decalage, int taille) => $"{sql} LIMIT {taille} OFFSET {decalage}";
}
