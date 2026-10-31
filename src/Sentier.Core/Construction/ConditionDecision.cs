using Sentier.Variables;

namespace Sentier.Definitions;

/// <summary>
/// Opérateur d'une branche conditionnelle sur variable, déclarée par <c>.Si(variable)</c>.
/// Chaque méthode termine la branche et rend le constructeur de la décision, pour enchaîner
/// <c>.Alors(...)</c>, <c>.Vers(...)</c>, <c>.Nommee(...)</c> ou la branche suivante.
/// </summary>
/// <typeparam name="T">Type de la variable testée.</typeparam>
public sealed class ConditionDecision<T>
{
    private readonly ConstructeurDecision _decision;
    private readonly string _variable;
    private readonly TypeVariable _type;

    internal ConditionDecision(ConstructeurDecision decision, string variable, TypeVariable type)
    {
        _decision = decision;
        _variable = variable;
        _type = type;
    }

    /// <summary>Variable égale à la valeur (<c>null</c> : variable vide).</summary>
    public ConstructeurDecision EgalA(T valeur) => Valeur(OperateurComparaison.Egal, valeur);

    /// <summary>Variable égale à une autre variable.</summary>
    public ConstructeurDecision EgalA(Variable<T> autre) => Comparee(OperateurComparaison.Egal, autre);

    /// <summary>Variable différente de la valeur, ou vide (<c>null</c> : variable renseignée).</summary>
    public ConstructeurDecision DifferentDe(T valeur) => Valeur(OperateurComparaison.Different, valeur);

    /// <summary>Variable différente d'une autre variable.</summary>
    public ConstructeurDecision DifferentDe(Variable<T> autre) => Comparee(OperateurComparaison.Different, autre);

    /// <summary>Variable strictement supérieure à la valeur.</summary>
    public ConstructeurDecision SuperieurA(T valeur) => Valeur(OperateurComparaison.Superieur, valeur);

    /// <summary>Variable strictement supérieure à une autre variable.</summary>
    public ConstructeurDecision SuperieurA(Variable<T> autre) => Comparee(OperateurComparaison.Superieur, autre);

    /// <summary>Variable supérieure ou égale à la valeur.</summary>
    public ConstructeurDecision SuperieurOuEgalA(T valeur) => Valeur(OperateurComparaison.SuperieurOuEgal, valeur);

    /// <summary>Variable supérieure ou égale à une autre variable.</summary>
    public ConstructeurDecision SuperieurOuEgalA(Variable<T> autre) => Comparee(OperateurComparaison.SuperieurOuEgal, autre);

    /// <summary>Variable strictement inférieure à la valeur.</summary>
    public ConstructeurDecision InferieurA(T valeur) => Valeur(OperateurComparaison.Inferieur, valeur);

    /// <summary>Variable strictement inférieure à une autre variable.</summary>
    public ConstructeurDecision InferieurA(Variable<T> autre) => Comparee(OperateurComparaison.Inferieur, autre);

    /// <summary>Variable inférieure ou égale à la valeur.</summary>
    public ConstructeurDecision InferieurOuEgalA(T valeur) => Valeur(OperateurComparaison.InferieurOuEgal, valeur);

    /// <summary>Variable inférieure ou égale à une autre variable.</summary>
    public ConstructeurDecision InferieurOuEgalA(Variable<T> autre) => Comparee(OperateurComparaison.InferieurOuEgal, autre);

    /// <summary>Variable texte contenant la valeur (sensible à la casse).</summary>
    public ConstructeurDecision Contient(string texte) => Texte(OperateurComparaison.Contient, texte);

    /// <summary>Variable texte commençant par la valeur (sensible à la casse).</summary>
    public ConstructeurDecision CommencePar(string texte) => Texte(OperateurComparaison.CommencePar, texte);

    /// <summary>Variable renseignée (non nulle).</summary>
    public ConstructeurDecision EstRenseignee() => Unaire(OperateurComparaison.EstRenseignee);

    /// <summary>Variable vide (absente ou nulle).</summary>
    public ConstructeurDecision EstVide() => Unaire(OperateurComparaison.EstVide);

    /// <summary>Variable booléenne vraie.</summary>
    public ConstructeurDecision EstVrai() => Booleen(true);

    /// <summary>Variable booléenne fausse.</summary>
    public ConstructeurDecision EstFaux() => Booleen(false);

    private ConstructeurDecision Valeur(OperateurComparaison operateur, T valeur)
    {
        if (valeur is null)
        {
            return operateur switch
            {
                OperateurComparaison.Egal => Unaire(OperateurComparaison.EstVide),
                OperateurComparaison.Different => Unaire(OperateurComparaison.EstRenseignee),
                _ => throw new ArgumentNullException(nameof(valeur), $"L'opérateur {operateur} exige une valeur de comparaison (variable '{_variable}')."),
            };
        }

        return _decision.AjouterCondition(new ConditionVariable
        {
            Variable = _variable,
            Operateur = operateur,
            Valeur = ValeurTypee.Depuis(valeur, _type, _variable),
        });
    }

    private ConstructeurDecision Comparee(OperateurComparaison operateur, Variable<T> autre)
        => _decision.AjouterCondition(new ConditionVariable { Variable = _variable, Operateur = operateur, VariableComparee = autre.Nom });

    private ConstructeurDecision Texte(OperateurComparaison operateur, string texte)
    {
        ArgumentNullException.ThrowIfNull(texte);
        return _decision.AjouterCondition(new ConditionVariable
        {
            Variable = _variable,
            Operateur = operateur,
            Valeur = ValeurTypee.Depuis(texte, TypeVariable.Texte, _variable),
        });
    }

    private ConstructeurDecision Booleen(bool valeur)
        => _decision.AjouterCondition(new ConditionVariable
        {
            Variable = _variable,
            Operateur = OperateurComparaison.Egal,
            Valeur = ValeurTypee.Depuis(valeur, TypeVariable.Booleen, _variable),
        });

    private ConstructeurDecision Unaire(OperateurComparaison operateur)
        => _decision.AjouterCondition(new ConditionVariable { Variable = _variable, Operateur = operateur });
}
