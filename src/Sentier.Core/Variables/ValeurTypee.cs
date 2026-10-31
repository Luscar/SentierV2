using System.Text.Json;

namespace Sentier.Variables;

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
