using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sentier.Variables;

/// <summary>
/// Variable typée, déclarée une fois comme constante dans la classe du processus et réutilisée
/// partout (paramètres, mappages, lecture et écriture dans les commandes).
/// </summary>
/// <typeparam name="T">Type CLR de la variable.</typeparam>
public sealed class Variable<T> : IVariable
{
    internal Variable(string nom, TypeVariable type, bool obligatoire = false, ValeurTypee? defaut = null)
    {
        if (string.IsNullOrWhiteSpace(nom))
        {
            throw new ArgumentException("Le nom d'une variable est obligatoire.", nameof(nom));
        }

        Nom = nom;
        Type = type;
        EstObligatoire = obligatoire;
        Defaut = defaut;
    }

    /// <inheritdoc />
    public string Nom { get; }

    /// <inheritdoc />
    public TypeVariable Type { get; }

    /// <summary>Vrai si la variable doit être fournie au démarrage (ou avoir une valeur par défaut).</summary>
    public bool EstObligatoire { get; }

    /// <summary>Valeur par défaut, optionnelle.</summary>
    [JsonIgnore]
    public ValeurTypee? Defaut { get; }

    /// <summary>Retourne une copie de la variable marquée obligatoire.</summary>
    public Variable<T> Obligatoire() => new(Nom, Type, true, Defaut);

    /// <summary>Retourne une copie de la variable avec une valeur par défaut.</summary>
    public Variable<T> ParDefaut(T valeur)
        => new(Nom, Type, EstObligatoire, valeur is null ? null : ValeurTypee.Depuis(valeur, Type, Nom));

    /// <inheritdoc />
    public DeclarationVariable VersDeclaration() => new()
    {
        Nom = Nom,
        Type = Type,
        Obligatoire = EstObligatoire,
        Defaut = Defaut,
    };

    /// <inheritdoc />
    public override string ToString() => Nom;
}

/// <summary>Fabrique des variables typées.</summary>
public static class Variable
{
    /// <summary>Variable texte.</summary>
    public static Variable<string> Texte(string nom) => new(nom, TypeVariable.Texte);

    /// <summary>Variable entière (<see cref="long"/>).</summary>
    public static Variable<long> Entier(string nom) => new(nom, TypeVariable.Entier);

    /// <summary>Variable décimale.</summary>
    public static Variable<decimal> Decimal(string nom) => new(nom, TypeVariable.Decimal);

    /// <summary>Variable booléenne.</summary>
    public static Variable<bool> Booleen(string nom) => new(nom, TypeVariable.Booleen);

    /// <summary>Variable date (UTC).</summary>
    public static Variable<DateTime> Date(string nom) => new(nom, TypeVariable.Date);

    /// <summary>Variable JSON libre, relue en <typeparamref name="T"/> sans garantie de type.</summary>
    public static Variable<T> Json<T>(string nom) => new(nom, TypeVariable.Json);
}
