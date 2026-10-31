using System.Data;
using Dapper;
using Sentier.Persistance.Dapper;

namespace Sentier.Persistance.Oracle;

/// <summary>
/// Dialecte Oracle : paramètres <c>:p</c>, identifiants par séquence (<c>NEXTVAL</c>),
/// <c>FOR UPDATE SKIP LOCKED</c>, limitation par <c>ROWNUM</c> (compatible avec les anciennes versions).
/// </summary>
public sealed class DialecteOracle : DialecteSqlBase
{
    /// <inheritdoc />
    public override TypeBaseDonnees Type => TypeBaseDonnees.Oracle;

    /// <inheritdoc />
    public override string VerrouillageSansAttente => " FOR UPDATE SKIP LOCKED";

    /// <inheritdoc />
    public override bool SupporteTransactionAmbiante => true;

    /// <inheritdoc />
    protected override string RessourceScript => "Sentier.Persistance.Oracle.Scripts.oracle.sql";

    /// <inheritdoc />
    public override string Parametre(string nom) => ":" + nom;

    /// <inheritdoc />
    public override Task<long> ProchainIdentifiantAsync(IDbConnection connexion, IDbTransaction? transaction, NomsObjets noms, string nomSequence, CancellationToken ct)
        => connexion.ExecuteScalarAsync<long>(new CommandDefinition($"SELECT {nomSequence}.NEXTVAL FROM DUAL", transaction: transaction, cancellationToken: ct));

    /// <inheritdoc />
    public override string Limiter(string sql, int taille) => $"SELECT * FROM ({sql}) WHERE ROWNUM <= {taille}";

    /// <inheritdoc />
    public override string Paginer(string sql, int decalage, int taille)
        => $"SELECT * FROM (SELECT t__.*, ROWNUM AS RN__ FROM ({sql}) t__ WHERE ROWNUM <= {(long)decalage + taille}) WHERE RN__ > {decalage}";
}
