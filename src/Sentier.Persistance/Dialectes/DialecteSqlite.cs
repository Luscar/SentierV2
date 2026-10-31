namespace Sentier.Persistance;

/// <summary>
/// Dialecte SQLite : paramètres <c>@p</c>, séquences émulées par la table <c>{Prefixe}_SEQ</c>
/// incrémentée dans la transaction, pas de verrouillage de ligne, <c>LIMIT</c>/<c>OFFSET</c>.
/// </summary>
public sealed class DialecteSqlite : DialecteSqlBase
{
    /// <inheritdoc />
    public override TypeBaseDonnees Type => TypeBaseDonnees.Sqlite;

    /// <inheritdoc />
    public override string VerrouillageSansAttente => "";

    /// <inheritdoc />
    /// <remarks>Microsoft.Data.Sqlite ne s'enrôle pas dans un TransactionScope : chaque instruction serait validée immédiatement.</remarks>
    public override bool SupporteTransactionAmbiante => false;

    /// <inheritdoc />
    protected override string RessourceScript => "Sentier.Persistance.Scripts.sqlite.sql";

    /// <inheritdoc />
    public override string Parametre(string nom) => "@" + nom;

    /// <inheritdoc />
    public override async Task<long> ProchainIdentifiantAsync(IExecuteurSqlSentier executeur, NomsObjets noms, string nomSequence, CancellationToken ct)
    {
        var table = noms.TableSequences;
        var parametres = new { nom = nomSequence };
        var lignes = await executeur.ExecuterAsync($"UPDATE {table} SET VAL_SEQ = VAL_SEQ + 1 WHERE NOM_SEQ = @nom", parametres, ct);
        if (lignes == 0)
        {
            await executeur.ExecuterAsync($"INSERT INTO {table} (NOM_SEQ, VAL_SEQ) VALUES (@nom, 1)", parametres, ct);
            return 1;
        }

        return await executeur.ExecuterScalaireAsync<long>($"SELECT VAL_SEQ FROM {table} WHERE NOM_SEQ = @nom", parametres, ct);
    }

    /// <inheritdoc />
    public override string Limiter(string sql, int taille) => $"{sql} LIMIT {taille}";

    /// <inheritdoc />
    public override string Paginer(string sql, int decalage, int taille) => $"{sql} LIMIT {taille} OFFSET {decalage}";
}
