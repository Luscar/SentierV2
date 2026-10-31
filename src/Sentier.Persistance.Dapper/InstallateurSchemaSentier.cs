namespace Sentier.Persistance.Dapper;

/// <summary>
/// Génère et exécute le DDL de Sentier avec le préfixe configuré, via l'exécuteur SQL (connexion du client par défaut)
/// (utile pour SQLite en tests ; optionnel pour Oracle, où le script peut être remis à un DBA).
/// </summary>
public sealed class InstallateurSchemaSentier
{
    private readonly IExecuteurSqlSentier _executeur;
    private readonly IDialecteSql _dialecte;
    private readonly NomsObjets _noms;

    /// <summary>Crée l'installateur.</summary>
    public InstallateurSchemaSentier(IExecuteurSqlSentier executeur, IDialecteSql dialecte, NomsObjets noms)
    {
        _executeur = executeur;
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
            await _executeur.ExecuterAsync(instruction, null, ct);
        }
    }
}
