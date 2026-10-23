using Dapper;

namespace Sentier.Persistance.Dapper;

/// <summary>
/// Génère et exécute le DDL de Sentier avec le préfixe configuré, via la connexion injectée par le client
/// (utile pour SQLite en tests ; optionnel pour Oracle, où le script peut être remis à un DBA).
/// </summary>
public sealed class InstallateurSchemaSentier
{
    private readonly ISessionSentier _session;
    private readonly IDialecteSql _dialecte;
    private readonly NomsObjets _noms;

    /// <summary>Crée l'installateur.</summary>
    public InstallateurSchemaSentier(ISessionSentier session, IDialecteSql dialecte, NomsObjets noms)
    {
        _session = session;
        _dialecte = dialecte;
        _noms = noms;
    }

    /// <summary>Script DDL complet, avec le préfixe appliqué.</summary>
    public string GenererScript() => _dialecte.ScriptSchema(_noms);

    /// <summary>
    /// Exécute le script instruction par instruction, dans la transaction courante si elle existe
    /// (sous Oracle, le DDL valide implicitement la transaction).
    /// </summary>
    public async Task InstallerAsync(CancellationToken ct = default)
    {
        foreach (var instruction in _dialecte.DecouperScript(GenererScript()))
        {
            await _session.Connexion.ExecuteAsync(new CommandDefinition(instruction, transaction: _session.Transaction, cancellationToken: ct));
        }
    }
}
