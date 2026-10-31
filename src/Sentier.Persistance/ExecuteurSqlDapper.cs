using Dapper;

namespace Sentier.Persistance;

/// <summary>
/// Exécuteur SQL par défaut : Dapper sur la connexion et la transaction de <see cref="ISessionSentier"/>
/// (<c>transaction: null</c> si le client travaille dans un <see cref="System.Transactions.TransactionScope"/>).
/// </summary>
public sealed class ExecuteurSqlDapper : IExecuteurSqlSentier
{
    private readonly ISessionSentier _session;

    /// <summary>Crée l'exécuteur.</summary>
    public ExecuteurSqlDapper(ISessionSentier session)
    {
        _session = session;
        SentierDapper.EnregistrerGestionnairesTypes();
    }

    /// <inheritdoc />
    public Task<int> ExecuterAsync(string sql, object? parametres, CancellationToken ct)
        => _session.Connexion.ExecuteAsync(Commande(sql, parametres, ct));

    /// <inheritdoc />
    public Task<T?> ExecuterScalaireAsync<T>(string sql, object? parametres, CancellationToken ct)
        => _session.Connexion.ExecuteScalarAsync<T>(Commande(sql, parametres, ct));

    /// <inheritdoc />
    public Task<IEnumerable<T>> LireAsync<T>(string sql, object? parametres, CancellationToken ct)
        => _session.Connexion.QueryAsync<T>(Commande(sql, parametres, ct));

    /// <inheritdoc />
    public Task<T?> LirePremierOuDefautAsync<T>(string sql, object? parametres, CancellationToken ct)
        => _session.Connexion.QueryFirstOrDefaultAsync<T>(Commande(sql, parametres, ct));

    private CommandDefinition Commande(string sql, object? parametres, CancellationToken ct)
        => new(sql, parametres, _session.Transaction, cancellationToken: ct);
}
