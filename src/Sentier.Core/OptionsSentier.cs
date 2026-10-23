using System.Reflection;
using System.Text.RegularExpressions;

namespace Sentier;

/// <summary>Options de configuration de Sentier, renseignées dans <c>AddSentier</c>.</summary>
public sealed partial class OptionsSentier
{
    /// <summary>Longueur maximale du préfixe, pour que tous les noms d'objets tiennent en 30 caractères.</summary>
    public const int LongueurMaxPrefixe = 12;

    /// <summary>Préfixe des tables, vues et séquences (obligatoire, ex. <c>SENTIER</c>).</summary>
    public string PrefixeTables { get; set; } = "";

    /// <summary>Nombre maximal d'étapes par exécution, protection contre les boucles infinies (1000 par défaut).</summary>
    public int MaxEtapesParExecution { get; set; } = 1000;

    /// <summary>Dialecte SQL de la base.</summary>
    public DialecteBase Dialecte { get; set; } = DialecteBase.Oracle;

    /// <summary>Assemblys balayés pour trouver commandes, requêtes, définitions, plans de migration et gestionnaire de tâches.</summary>
    public List<Assembly> Assemblys { get; } = [];

    /// <summary>
    /// Implémentation de <see cref="IGestionnaireTaches"/> à enregistrer (Scoped). Si elle n'est pas fournie,
    /// celle trouvée par balayage est utilisée.
    /// </summary>
    public Type? TypeGestionnaireTaches { get; private set; }

    /// <summary>Implémentation de <see cref="ISessionSentier"/> à enregistrer (Scoped) à la place de celle par défaut.</summary>
    public Type? TypeSession { get; private set; }

    /// <summary>Utilise le dialecte Oracle.</summary>
    public OptionsSentier UtiliserOracle()
    {
        Dialecte = DialecteBase.Oracle;
        return this;
    }

    /// <summary>Utilise le dialecte SQLite.</summary>
    public OptionsSentier UtiliserSqlite()
    {
        Dialecte = DialecteBase.Sqlite;
        return this;
    }

    /// <summary>Utilise le dialecte Oracle avec le préfixe de tables <paramref name="prefixeTables"/>.</summary>
    public OptionsSentier UtiliserOracle(string prefixeTables)
    {
        PrefixeTables = prefixeTables;
        return UtiliserOracle();
    }

    /// <summary>Utilise le dialecte SQLite avec le préfixe de tables <paramref name="prefixeTables"/>.</summary>
    public OptionsSentier UtiliserSqlite(string prefixeTables)
    {
        PrefixeTables = prefixeTables;
        return UtiliserSqlite();
    }

    /// <summary>Ajoute un assembly à balayer.</summary>
    public OptionsSentier Balayer(Assembly assembly)
    {
        if (!Assemblys.Contains(assembly))
        {
            Assemblys.Add(assembly);
        }

        return this;
    }

    /// <summary>Ajoute l'assembly qui contient <typeparamref name="T"/>.</summary>
    public OptionsSentier BalayerAssemblyDe<T>() => Balayer(typeof(T).Assembly);

    /// <summary>Enregistre l'implémentation du gestionnaire de tâches du client, à la place de celle trouvée par balayage.</summary>
    public OptionsSentier UtiliserGestionnaireTaches<T>() where T : class, IGestionnaireTaches
    {
        TypeGestionnaireTaches = typeof(T);
        return this;
    }

    /// <summary>Remplace l'implémentation par défaut de <see cref="ISessionSentier"/> (ex. unité de travail du client).</summary>
    public OptionsSentier UtiliserSession<T>() where T : class, ISessionSentier
    {
        TypeSession = typeof(T);
        return this;
    }

    /// <summary>Vérifie la cohérence des options.</summary>
    public void Valider()
    {
        if (string.IsNullOrWhiteSpace(PrefixeTables))
        {
            throw new SentierException("OptionsSentier.PrefixeTables est obligatoire.");
        }

        if (!RegexPrefixe().IsMatch(PrefixeTables))
        {
            throw new SentierException($"Le préfixe '{PrefixeTables}' est invalide : seuls les lettres, chiffres et '_' sont permis, et il doit commencer par une lettre.");
        }

        if (PrefixeTables.Length > LongueurMaxPrefixe)
        {
            throw new SentierException($"Le préfixe '{PrefixeTables}' dépasse {LongueurMaxPrefixe} caractères : les noms d'objets dépasseraient 30 caractères.");
        }

        if (MaxEtapesParExecution < 1)
        {
            throw new SentierException("OptionsSentier.MaxEtapesParExecution doit être positif.");
        }
    }

    [GeneratedRegex("^[A-Za-z][A-Za-z0-9_]*$")]
    private static partial Regex RegexPrefixe();
}
