namespace Sentier;

/// <summary>Opérateur d'une condition sur variable (branche de décision, filtre de recherche).</summary>
public enum OperateurComparaison
{
    /// <summary>Égal à la valeur.</summary>
    Egal,

    /// <summary>Différent de la valeur ; vrai aussi lorsque la variable n'a pas de valeur.</summary>
    Different,

    /// <summary>Strictement supérieur (entier, décimal, date, texte en ordre ordinal).</summary>
    Superieur,

    /// <summary>Supérieur ou égal.</summary>
    SuperieurOuEgal,

    /// <summary>Strictement inférieur.</summary>
    Inferieur,

    /// <summary>Inférieur ou égal.</summary>
    InferieurOuEgal,

    /// <summary>Le texte contient la valeur (sensible à la casse).</summary>
    Contient,

    /// <summary>Le texte commence par la valeur (sensible à la casse).</summary>
    CommencePar,

    /// <summary>La variable a une valeur (non nulle).</summary>
    EstRenseignee,

    /// <summary>La variable n'a pas de valeur (absente ou nulle).</summary>
    EstVide,
}
