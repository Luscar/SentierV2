using System.Data;
using System.Reflection;
using System.Text.RegularExpressions;

namespace Sentier.Persistance;

/// <summary>Base commune des dialectes : lecture des scripts embarqués et découpage.</summary>
public abstract partial class DialecteSqlBase : IDialecteSql
{
    /// <inheritdoc />
    public abstract TypeBaseDonnees Type { get; }

    /// <inheritdoc />
    public abstract string VerrouillageSansAttente { get; }

    /// <inheritdoc />
    public abstract bool SupporteTransactionAmbiante { get; }

    /// <inheritdoc />
    public abstract string Parametre(string nom);

    /// <inheritdoc />
    public abstract Task<long> ProchainIdentifiantAsync(IExecuteurSqlSentier executeur, NomsObjets noms, string nomSequence, CancellationToken ct);

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
