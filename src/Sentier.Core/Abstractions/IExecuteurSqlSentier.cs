namespace Sentier;

/// <summary>
/// Exécute les requêtes SQL de Sentier. Par défaut, Sentier les exécute avec Dapper sur l'<see cref="System.Data.IDbConnection"/>
/// de la portée (<see cref="ISessionSentier"/>). Le client peut le remplacer par sa propre couche d'accès aux données
/// (<c>OptionsSentier.UtiliserExecuteurSql&lt;T&gt;()</c>) : Sentier n'a alors plus besoin d'<see cref="System.Data.IDbConnection"/>.
/// Enregistré en Scoped.
/// </summary>
/// <remarks>
/// Le SQL reçu est déjà dans la syntaxe du dialecte (paramètres <c>:nom</c> sous Oracle, <c>@nom</c> sous SQLite) ;
/// les paramètres sont des objets anonymes dont les propriétés portent les noms des paramètres (convention Dapper), et
/// les lignes lues sont mappées par nom de colonne sur des propriétés de même nom. L'implémentation doit utiliser la
/// connexion et la transaction de l'unité de travail du client : Sentier vérifie qu'une transaction est active
/// (<see cref="System.Transactions.TransactionScope"/>, ou <see cref="ISessionSentier.Transaction"/>) avant d'écrire,
/// mais ne l'ouvre, ne la valide et ne l'annule jamais.
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
}
