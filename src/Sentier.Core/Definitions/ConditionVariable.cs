using System.Text.Json;
using Sentier.Variables;

namespace Sentier.Definitions;

/// <summary>
/// Condition d'une branche de décision évaluée directement sur une variable de l'instance, sans requête :
/// comparaison avec une valeur fixe ou avec une autre variable.
/// </summary>
public sealed class ConditionVariable
{
    /// <summary>Nom de la variable testée.</summary>
    public required string Variable { get; init; }

    /// <summary>Opérateur.</summary>
    public required OperateurComparaison Operateur { get; init; }

    /// <summary>Valeur de comparaison fixe (absente pour <c>EstRenseignee</c>, <c>EstVide</c> ou une comparaison avec une variable).</summary>
    public ValeurTypee? Valeur { get; init; }

    /// <summary>Nom de la variable de comparaison, à la place d'une valeur fixe.</summary>
    public string? VariableComparee { get; init; }

    /// <summary>Indique si l'opérateur n'attend aucune valeur de comparaison.</summary>
    public static bool EstUnaire(OperateurComparaison operateur)
        => operateur is OperateurComparaison.EstRenseignee or OperateurComparaison.EstVide;

    /// <summary>Évalue la condition sur les variables de l'instance.</summary>
    public bool Evaluer(IVariablesLecture variables)
    {
        var gauche = variables.Lire(Variable);
        var droite = VariableComparee is not null ? variables.Lire(VariableComparee) : Valeur?.VersObjet(Variable);
        return EvaluateurComparaison.Evaluer(Operateur, gauche, droite);
    }

    /// <summary>Expression lisible de la condition (nom de sortie par défaut, diagramme).</summary>
    public string Expression()
    {
        var operande = VariableComparee ?? (Valeur is null ? "" : Texte(Valeur.Valeur));
        return Operateur switch
        {
            OperateurComparaison.EstRenseignee => $"{Variable} renseignée",
            OperateurComparaison.EstVide => $"{Variable} vide",
            _ => $"{Variable} {Symbole(Operateur)} {operande}",
        };
    }

    /// <summary>Symbole ou mot d'un opérateur binaire.</summary>
    public static string Symbole(OperateurComparaison operateur) => operateur switch
    {
        OperateurComparaison.Egal => "=",
        OperateurComparaison.Different => "!=",
        OperateurComparaison.Superieur => ">",
        OperateurComparaison.SuperieurOuEgal => ">=",
        OperateurComparaison.Inferieur => "<",
        OperateurComparaison.InferieurOuEgal => "<=",
        OperateurComparaison.Contient => "contient",
        OperateurComparaison.CommencePar => "commence par",
        OperateurComparaison.EstRenseignee => "renseignée",
        _ => "vide",
    };

    private static string Texte(JsonElement valeur)
        => valeur.ValueKind == JsonValueKind.String ? valeur.GetString() ?? "" : valeur.GetRawText();
}
