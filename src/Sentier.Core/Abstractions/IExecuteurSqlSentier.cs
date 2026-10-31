namespace Sentier;

/// <summary>
/// Exécute les requêtes SQL de Sentier. Par défaut, Sentier les exécute avec Dapper sur l'<see cref="System.Data.IDbConnection"/>
/// de la portée (<see cref="ISessionSentier"/>). Le client peut le remplacer par sa propre couche d'accès aux données
/// (<c>OptionsSentier.UtiliserExecuteurSql&lt;T&gt;()</c>) : Sentier n'a alors plus besoin d'<see cref="System.Data.IDbConnection"/>.
/// Enregistré en Scoped.
/// </summary>
/// <remarks>
/// Le SQL reçu est dans la syntaxe du dialecte (paramètres <c>:nom</c> sous Oracle, <c>@nom</c> sous SQLite), sauf si
/// <see cref="TraduitLesParametres"/> est vrai : les paramètres restent alors en <c>@nom</c> ;
/// les paramètres sont des objets anonymes dont les propriétés portent les noms des paramètres (convention Dapper), et
/// les lignes lues sont mappées par nom de colonne sur des propriétés de même nom. Aucune transaction n'est passée :
/// l'implémentation exécute ses commandes sur une connexion enrôlée dans le
/// <see cref="System.Transactions.TransactionScope"/> du client (Dapper avec <c>transaction: null</c>). Sentier vérifie
/// qu'un <see cref="System.Transactions.TransactionScope"/> est actif avant d'écrire, mais ne l'ouvre, ne le valide et
/// ne l'annule jamais.
/// </remarks>
public interface IExecuteurSqlSentier
{
    /// <summary>Exécute une instruction (INSERT, UPDATE, DELETE, DDL) et retourne le nombre de lignes touchées.</summary>
    Task<int> ExecuterAsync(string sql, object? parametres, CancellationToken ct);

    /// <summary>Retourne la première colonne de la première ligne, ou la valeur par défaut s'il n'y en a pas.</summary>
    Task<T?> ExecuterScalaireAsync<T>(string sql, object? parametres, CancellationToken ct);

    /// <summary>Retourne toutes les lignes, mappées sur <typeparamref name="T"/>.</summary>
    Task<IEnumerable<T>> LireAsync<T>(string sql, object? parametres, CancellationToken ct);

    /// <summary>Retourne la première ligne mappée sur <typeparamref name="T"/>, ou la valeur par défaut s'il n'y en a pas.</summary>
    Task<T?> LirePremierOuDefautAsync<T>(string sql, object? parametres, CancellationToken ct);

    /// <summary>
    /// Vrai si l'implémentation traduit elle-même les paramètres dans la syntaxe de la base (par exemple un wrapper dont
    /// le traducteur de requêtes change <c>@nom</c> en <c>:nom</c> sous Oracle). Sentier lui confie alors son SQL avec
    /// des paramètres <c>@nom</c>, sans les traduire. Faux par défaut : Sentier traduit selon le dialecte.
    /// </summary>
    /// <remarks>
    /// Seuls les marqueurs de paramètres sont concernés : la syntaxe propre à la base que Sentier construit lui-même
    /// (limitation par <c>ROWNUM</c>, <c>FOR UPDATE SKIP LOCKED</c>, <c>NEXTVAL</c>, script DDL) reste dictée par le dialecte.
    /// </remarks>
    bool TraduitLesParametres => false;
}
