using System.Text.RegularExpressions;

namespace Sentier.Persistance;

/// <summary>
/// Composant unique qui centralise tous les noms d'objets de base de données (tables, vue, séquences),
/// construits sous la forme <c>{Prefixe}_{Objet}</c> avec les mnémoniques de BpmPlus. Aucun nom n'est codé en dur ailleurs.
/// </summary>
public sealed partial class NomsObjets
{
    /// <summary>Jeton remplacé par le préfixe dans les scripts DDL.</summary>
    public const string JetonPrefixe = "{{P}}";

    /// <summary>Longueur maximale d'un nom d'objet (compatibilité avec les anciennes versions d'Oracle).</summary>
    public const int LongueurMaxNom = 30;

    /// <summary>Crée les noms pour un préfixe ; lève une exception si le préfixe est invalide.</summary>
    public NomsObjets(string prefixe)
    {
        if (string.IsNullOrWhiteSpace(prefixe) || !RegexPrefixe().IsMatch(prefixe))
        {
            throw new SentierException($"Le préfixe '{prefixe}' est invalide : seuls les lettres, chiffres et '_' sont permis, et il doit commencer par une lettre.");
        }

        Prefixe = prefixe.ToUpperInvariant();
        var plusLong = Nom("_HIST_NOEUD_PROCS");
        if (plusLong.Length > LongueurMaxNom)
        {
            throw new SentierException($"Le préfixe '{prefixe}' est trop long : le nom '{plusLong}' dépasse {LongueurMaxNom} caractères.");
        }
    }

    /// <summary>Préfixe (en majuscules).</summary>
    public string Prefixe { get; }

    /// <summary>Table des versions de définitions.</summary>
    public string Definition => Nom("_DEFIN_PROCS");

    /// <summary>Table des instances.</summary>
    public string Instance => Nom("_INSTC_PROCS");

    /// <summary>Table de l'historique des passages.</summary>
    public string HistNoeud => Nom("_HIST_NOEUD_PROCS");

    /// <summary>Table des attentes.</summary>
    public string Attente => Nom("_ATTEN_PROCS");

    /// <summary>Table des traces de migration.</summary>
    public string Migration => Nom("_MIGRA_PROCS");

    /// <summary>Vue des instances actives.</summary>
    public string VueInstanceActive => Nom("_V_INSTC_ACTIV");

    /// <summary>Table des compteurs émulant les séquences (SQLite).</summary>
    public string TableSequences => Nom("_SEQ");

    /// <summary>Séquence des définitions.</summary>
    public string SeqDefinition => Nom("_SEQ_DEFIN");

    /// <summary>Séquence des instances.</summary>
    public string SeqInstance => Nom("_SEQ_INSTC");

    /// <summary>Séquence de l'historique.</summary>
    public string SeqHistNoeud => Nom("_SEQ_HIST_NOEUD");

    /// <summary>Séquence des attentes.</summary>
    public string SeqAttente => Nom("_SEQ_ATTEN");

    /// <summary>Séquence des migrations.</summary>
    public string SeqMigration => Nom("_SEQ_MIGRA");

    /// <summary>Remplace le jeton de préfixe dans un script DDL.</summary>
    public string Appliquer(string script) => script.Replace(JetonPrefixe, Prefixe, StringComparison.Ordinal);

    private string Nom(string suffixe) => Prefixe + suffixe;

    [GeneratedRegex("^[A-Za-z][A-Za-z0-9_]*$")]
    private static partial Regex RegexPrefixe();
}
