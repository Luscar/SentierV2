namespace Sentier.Variables;

/// <summary>Déclaration d'une variable dans une définition, sous forme sérialisable.</summary>
public sealed class DeclarationVariable
{
    /// <summary>Nom de la variable.</summary>
    public required string Nom { get; init; }

    /// <summary>Type déclaré.</summary>
    public required TypeVariable Type { get; init; }

    /// <summary>La variable doit être fournie au démarrage ou avoir une valeur par défaut.</summary>
    public bool Obligatoire { get; init; }

    /// <summary>Valeur par défaut, optionnelle.</summary>
    public ValeurTypee? Defaut { get; init; }
}
