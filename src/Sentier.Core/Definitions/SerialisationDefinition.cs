using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sentier.Definitions;

/// <summary>Sérialisation JSON canonique des définitions et calcul de leur empreinte.</summary>
public static class SerialisationDefinition
{
    /// <summary>Options JSON utilisées pour le contenu des définitions.</summary>
    public static JsonSerializerOptions Options { get; } = CreerOptions();

    /// <summary>Sérialise une définition dans sa forme canonique.</summary>
    public static string Serialiser(DefinitionProcessus definition) => JsonSerializer.Serialize(definition, Options);

    /// <summary>Sérialise un nœud dans sa forme canonique (comparaison entre versions).</summary>
    public static string Serialiser(Noeud noeud) => JsonSerializer.Serialize(noeud, Options);

    /// <summary>Sérialise un déclencheur dans sa forme canonique (comparaison entre versions).</summary>
    public static string Serialiser(Declencheur declencheur) => JsonSerializer.Serialize(declencheur, Options);

    /// <summary>Désérialise une définition.</summary>
    public static DefinitionProcessus Deserialiser(string json)
        => JsonSerializer.Deserialize<DefinitionProcessus>(json, Options)
           ?? throw new SentierException("Contenu de définition vide.");

    /// <summary>Calcule l'empreinte SHA-256 (hexadécimal) d'un contenu canonique.</summary>
    public static string CalculerEmpreinte(string contenuCanonique)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(contenuCanonique)));

    private static JsonSerializerOptions CreerOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = false,
        };
        options.Converters.Add(new JsonStringEnumConverter());
        options.MakeReadOnly(populateMissingResolver: true);
        return options;
    }
}
