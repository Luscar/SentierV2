using Sentier.Variables;

namespace Sentier.Definitions;

/// <summary>
/// Paramètre nommé d'un appel de commande ou de requête, ou entrée d'un mappage de sous-processus.
/// Sa valeur provient d'une source statique, d'une variable ou de la clé d'affaire.
/// </summary>
public sealed class Parametre
{
    /// <summary>Nom du paramètre (ou de la variable de l'enfant, pour un mappage).</summary>
    public required string Nom { get; init; }

    /// <summary>Source de la valeur.</summary>
    public required SourceParametre Source { get; init; }

    /// <summary>Valeur fixe, pour une source statique.</summary>
    public ValeurTypee? Valeur { get; init; }

    /// <summary>Nom de la variable lue, pour une source variable.</summary>
    public string? Variable { get; init; }

    /// <summary>Le paramètre est optionnel : une variable absente n'est pas une erreur.</summary>
    public bool Optionnel { get; init; }

    /// <summary>Valeur utilisée lorsque la variable d'un paramètre optionnel est absente.</summary>
    public ValeurTypee? Defaut { get; init; }
}

/// <summary>Utilisation d'une commande dans une définition, référencée par nom.</summary>
public sealed class AppelCommande
{
    /// <summary>Nom de la commande enregistrée.</summary>
    public required string Nom { get; init; }

    /// <summary>Paramètres de l'appel.</summary>
    public IReadOnlyList<Parametre> Parametres { get; init; } = [];
}

/// <summary>Utilisation d'une requête sur une branche de décision, référencée par nom.</summary>
public sealed class AppelRequete
{
    /// <summary>Nom de la requête enregistrée.</summary>
    public required string Nom { get; init; }

    /// <summary>Paramètres de l'appel.</summary>
    public IReadOnlyList<Parametre> Parametres { get; init; } = [];

    /// <summary>Inverse le résultat de la requête.</summary>
    public bool Inverser { get; init; }
}

/// <summary>Mappage de sortie d'un sous-processus : variable de l'enfant recopiée dans une variable du parent.</summary>
public sealed class MappageSortie
{
    /// <summary>Variable de l'enfant.</summary>
    public required string VariableEnfant { get; init; }

    /// <summary>Variable du parent.</summary>
    public required string VariableParent { get; init; }
}
