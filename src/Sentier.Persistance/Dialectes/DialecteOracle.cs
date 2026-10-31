namespace Sentier.Persistance;

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
    protected override string RessourceScript => "Sentier.Persistance.Scripts.oracle.sql";

    /// <inheritdoc />
    public override string Parametre(string nom) => ":" + nom;

    /// <inheritdoc />
    public override async Task<long> ProchainIdentifiantAsync(IExecuteurSqlSentier executeur, NomsObjets noms, string nomSequence, CancellationToken ct)
        => await executeur.ExecuterScalaireAsync<long>($"SELECT {nomSequence}.NEXTVAL FROM DUAL", null, ct);

    /// <inheritdoc />
    public override string Limiter(string sql, int taille) => $"SELECT * FROM ({sql}) WHERE ROWNUM <= {taille}";

    /// <inheritdoc />
    public override string Paginer(string sql, int decalage, int taille)
        => $"SELECT * FROM (SELECT t__.*, ROWNUM AS RN__ FROM ({sql}) t__ WHERE ROWNUM <= {(long)decalage + taille}) WHERE RN__ > {decalage}";
}
