using System.Data;
using System.Reflection;
using System.Text.RegularExpressions;

namespace Sentier.Persistance.Dapper;

/// <summary>Différences de syntaxe et de comportement entre bases (Oracle, SQLite).</summary>
public interface IDialecteSql
{
    /// <summary>Dialecte représenté.</summary>
    DialecteBase Type { get; }

    /// <summary>Marque d'un paramètre nommé (<c>:p</c> sous Oracle, <c>@p</c> sous SQLite).</summary>
    string Parametre(string nom);

    /// <summary>Obtient l'identifiant suivant d'une séquence, avant l'insertion.</summary>
    Task<long> ProchainIdentifiantAsync(IDbConnection connexion, IDbTransaction transaction, NomsObjets noms, string nomSequence, CancellationToken ct);

    /// <summary>Limite une requête ordonnée à ses <paramref name="taille"/> premières lignes.</summary>
    string Limiter(string sql, int taille);

    /// <summary>Pagine une requête ordonnée.</summary>
    string Paginer(string sql, int decalage, int taille);

    /// <summary>Suffixe de verrouillage sans attente (<c>FOR UPDATE SKIP LOCKED</c> sous Oracle, vide sous SQLite).</summary>
    string VerrouillageSansAttente { get; }

    /// <summary>Script DDL de création du schéma, avec le préfixe appliqué.</summary>
    string ScriptSchema(NomsObjets noms);

    /// <summary>Découpe un script en instructions exécutables une à une.</summary>
    IReadOnlyList<string> DecouperScript(string script);
}

/// <summary>Base commune des dialectes : lecture des scripts embarqués et découpage.</summary>
public abstract partial class DialecteSqlBase : IDialecteSql
{
    /// <inheritdoc />
    public abstract DialecteBase Type { get; }

    /// <inheritdoc />
    public abstract string VerrouillageSansAttente { get; }

    /// <inheritdoc />
    public abstract string Parametre(string nom);

    /// <inheritdoc />
    public abstract Task<long> ProchainIdentifiantAsync(IDbConnection connexion, IDbTransaction transaction, NomsObjets noms, string nomSequence, CancellationToken ct);

    /// <inheritdoc />
    public abstract string Limiter(string sql, int taille);

    /// <inheritdoc />
    public abstract string Paginer(string sql, int decalage, int taille);

    /// <summary>Nom de la ressource embarquée contenant le script DDL.</summary>
    protected abstract string RessourceScript { get; }

    /// <inheritdoc />
    public string ScriptSchema(NomsObjets noms)
    {
        var assembly = GetType().Assembly;
        using var flux = assembly.GetManifestResourceStream(RessourceScript)
            ?? throw new SentierException($"Script DDL introuvable : {RessourceScript} dans {assembly.GetName().Name}.");
        using var lecteur = new StreamReader(flux);
        return noms.Appliquer(lecteur.ReadToEnd());
    }

    /// <inheritdoc />
    public virtual IReadOnlyList<string> DecouperScript(string script)
    {
        var sansCommentaires = string.Join('\n', script.Split('\n').Where(l => !l.TrimStart().StartsWith("--", StringComparison.Ordinal)));
        return RegexFinInstruction().Split(sansCommentaires)
            .Select(i => i.Trim())
            .Where(i => i.Length > 0)
            .ToList();
    }

    [GeneratedRegex(@";[ \t]*(?:\r?\n|$)")]
    private static partial Regex RegexFinInstruction();
}
