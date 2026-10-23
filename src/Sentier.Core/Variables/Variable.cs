using System.Text.Json;
using System.Text.Json.Serialization;

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

/// <summary>Valeur typée stockée dans une définition (valeur statique, valeur par défaut).</summary>
public sealed class ValeurTypee
{
    /// <summary>Type de la valeur.</summary>
    public required TypeVariable Type { get; init; }

    /// <summary>Valeur sérialisée.</summary>
    public required JsonElement Valeur { get; init; }

    /// <summary>Crée une valeur typée à partir d'une valeur CLR.</summary>
    /// <param name="valeur">Valeur (non nulle).</param>
    /// <param name="type">Type imposé ; sinon déduit de la valeur.</param>
    /// <param name="nom">Nom, pour les messages d'erreur.</param>
    public static ValeurTypee Depuis(object valeur, TypeVariable? type = null, string nom = "valeur")
    {
        var typeEffectif = type ?? ConvertisseurValeurs.DeduireType(valeur);
        var normalisee = ConvertisseurValeurs.Normaliser(typeEffectif, valeur, nom);
        return new ValeurTypee
        {
            Type = typeEffectif,
            Valeur = normalisee is JsonElement element ? element : JsonSerializer.SerializeToElement(normalisee),
        };
    }

    /// <summary>Retourne la valeur dans sa forme canonique.</summary>
    public object? VersObjet(string nom = "valeur") => ConvertisseurValeurs.DepuisJson(Type, Valeur, nom);
}

/// <summary>Variable typée non générique, telle que manipulée par le constructeur.</summary>
public interface IVariable
{
    /// <summary>Nom de la variable.</summary>
    string Nom { get; }

    /// <summary>Type déclaré.</summary>
    TypeVariable Type { get; }

    /// <summary>Retourne la déclaration sérialisable de la variable.</summary>
    DeclarationVariable VersDeclaration();
}

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
