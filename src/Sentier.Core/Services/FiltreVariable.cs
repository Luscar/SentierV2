using Sentier.Variables;

namespace Sentier;

/// <summary>
/// Filtre d'une recherche d'instances sur une variable : <c>variable opérateur valeur</c>.
/// La valeur est convertie dans le type déclaré par la définition de chaque instance.
/// </summary>
/// <param name="Nom">Nom de la variable.</param>
/// <param name="Operateur">Opérateur.</param>
/// <param name="Valeur">Valeur de comparaison ; ignorée pour <c>EstRenseignee</c> et <c>EstVide</c>.</param>
public sealed record FiltreVariable(string Nom, OperateurComparaison Operateur, object? Valeur = null)
{
    /// <summary>Variable égale à la valeur.</summary>
    public static FiltreVariable Egal<T>(Variable<T> variable, T valeur) => new(variable.Nom, OperateurComparaison.Egal, valeur);

    /// <summary>Variable différente de la valeur (ou vide).</summary>
    public static FiltreVariable Different<T>(Variable<T> variable, T valeur) => new(variable.Nom, OperateurComparaison.Different, valeur);

    /// <summary>Variable strictement supérieure à la valeur.</summary>
    public static FiltreVariable Superieur<T>(Variable<T> variable, T valeur) => new(variable.Nom, OperateurComparaison.Superieur, valeur);

    /// <summary>Variable supérieure ou égale à la valeur.</summary>
    public static FiltreVariable SuperieurOuEgal<T>(Variable<T> variable, T valeur) => new(variable.Nom, OperateurComparaison.SuperieurOuEgal, valeur);

    /// <summary>Variable strictement inférieure à la valeur.</summary>
    public static FiltreVariable Inferieur<T>(Variable<T> variable, T valeur) => new(variable.Nom, OperateurComparaison.Inferieur, valeur);

    /// <summary>Variable inférieure ou égale à la valeur.</summary>
    public static FiltreVariable InferieurOuEgal<T>(Variable<T> variable, T valeur) => new(variable.Nom, OperateurComparaison.InferieurOuEgal, valeur);

    /// <summary>Variable texte contenant la valeur (sensible à la casse).</summary>
    public static FiltreVariable Contient(Variable<string> variable, string texte) => new(variable.Nom, OperateurComparaison.Contient, texte);

    /// <summary>Variable texte commençant par la valeur (sensible à la casse).</summary>
    public static FiltreVariable CommencePar(Variable<string> variable, string texte) => new(variable.Nom, OperateurComparaison.CommencePar, texte);

    /// <summary>Variable renseignée.</summary>
    public static FiltreVariable EstRenseignee(IVariable variable) => new(variable.Nom, OperateurComparaison.EstRenseignee);

    /// <summary>Variable vide (absente ou nulle).</summary>
    public static FiltreVariable EstVide(IVariable variable) => new(variable.Nom, OperateurComparaison.EstVide);
}
