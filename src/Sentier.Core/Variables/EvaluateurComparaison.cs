using System.Text.Json;

namespace Sentier.Variables;

/// <summary>
/// Comparaison de deux valeurs canoniques (<see cref="string"/>, <see cref="long"/>, <see cref="decimal"/>,
/// <see cref="bool"/>, <see cref="DateTime"/>, <see cref="JsonElement"/>), commune aux conditions de décision
/// et aux filtres de recherche.
/// </summary>
/// <remarks>
/// Une variable sans valeur ne satisfait que <see cref="OperateurComparaison.EstVide"/> et
/// <see cref="OperateurComparaison.Different"/> (d'une valeur renseignée). Un entier et un décimal se comparent
/// numériquement. Les textes se comparent en ordre ordinal, sensible à la casse.
/// </remarks>
public static class EvaluateurComparaison
{
    /// <summary>Évalue <paramref name="gauche"/> <paramref name="operateur"/> <paramref name="droite"/>.</summary>
    public static bool Evaluer(OperateurComparaison operateur, object? gauche, object? droite)
    {
        switch (operateur)
        {
            case OperateurComparaison.EstRenseignee:
                return gauche is not null;
            case OperateurComparaison.EstVide:
                return gauche is null;
            case OperateurComparaison.Egal:
                return gauche is null ? droite is null : droite is not null && SontEgales(gauche, droite);
            case OperateurComparaison.Different:
                return gauche is null ? droite is not null : droite is null || !SontEgales(gauche, droite);
        }

        if (gauche is null || droite is null)
        {
            return false;
        }

        switch (operateur)
        {
            case OperateurComparaison.Contient:
                return gauche is string texte && droite is string partie && texte.Contains(partie, StringComparison.Ordinal);
            case OperateurComparaison.CommencePar:
                return gauche is string texteDebut && droite is string debut && texteDebut.StartsWith(debut, StringComparison.Ordinal);
        }

        if (Comparer(gauche, droite) is not { } ordre)
        {
            return false;
        }

        return operateur switch
        {
            OperateurComparaison.Superieur => ordre > 0,
            OperateurComparaison.SuperieurOuEgal => ordre >= 0,
            OperateurComparaison.Inferieur => ordre < 0,
            OperateurComparaison.InferieurOuEgal => ordre <= 0,
            _ => false,
        };
    }

    private static bool SontEgales(object gauche, object droite)
    {
        if (gauche is JsonElement g && droite is JsonElement d)
        {
            return JsonElement.DeepEquals(g, d);
        }

        return Comparer(gauche, droite) is 0;
    }

    private static int? Comparer(object gauche, object droite) => (gauche, droite) switch
    {
        (long a, long b) => a.CompareTo(b),
        (long a, decimal b) => ((decimal)a).CompareTo(b),
        (decimal a, long b) => a.CompareTo(b),
        (decimal a, decimal b) => a.CompareTo(b),
        (DateTime a, DateTime b) => a.ToUniversalTime().CompareTo(b.ToUniversalTime()),
        (string a, string b) => string.CompareOrdinal(a, b),
        (bool a, bool b) => a.CompareTo(b),
        _ => null,
    };
}
