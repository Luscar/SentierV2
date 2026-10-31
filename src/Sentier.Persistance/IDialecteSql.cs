using System.Data;

namespace Sentier.Persistance;

/// <summary>Différences de syntaxe et de comportement entre bases (Oracle, SQLite).</summary>
public interface IDialecteSql
{
    /// <summary>Dialecte représenté.</summary>
    TypeBaseDonnees Type { get; }

    /// <summary>Marque d'un paramètre nommé (<c>:p</c> sous Oracle, <c>@p</c> sous SQLite).</summary>
    string Parametre(string nom);

    /// <summary>Obtient l'identifiant suivant d'une séquence, avant l'insertion.</summary>
    Task<long> ProchainIdentifiantAsync(IExecuteurSqlSentier executeur, NomsObjets noms, string nomSequence, CancellationToken ct);

    /// <summary>Limite une requête ordonnée à ses <paramref name="taille"/> premières lignes.</summary>
    string Limiter(string sql, int taille);

    /// <summary>Pagine une requête ordonnée.</summary>
    string Paginer(string sql, int decalage, int taille);

    /// <summary>
    /// Vrai si une connexion ouverte dans un <see cref="System.Transactions.TransactionScope"/> y est enrôlée
    /// (Oracle). Faux sinon (SQLite) : les écritures exigent alors une <see cref="IDbTransaction"/>.
    /// </summary>
    bool SupporteTransactionAmbiante { get; }

    /// <summary>Suffixe de verrouillage sans attente (<c>FOR UPDATE SKIP LOCKED</c> sous Oracle, vide sous SQLite).</summary>
    string VerrouillageSansAttente { get; }

    /// <summary>Script DDL de création du schéma, avec le préfixe appliqué.</summary>
    string ScriptSchema(NomsObjets noms);

    /// <summary>Découpe un script en instructions exécutables une à une.</summary>
    IReadOnlyList<string> DecouperScript(string script);
}
