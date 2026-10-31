using System.Text.Json;

namespace Sentier.Variables;

/// <summary>Sérialiseur par défaut, basé sur <c>System.Text.Json</c>.</summary>
public sealed class SerialiseurVariablesJson : ISerialiseurVariables
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = false };

    /// <inheritdoc />
    public string Serialiser(IReadOnlyDictionary<string, object?> valeurs)
        => JsonSerializer.Serialize(valeurs, Options);

    /// <inheritdoc />
    public Dictionary<string, object?> Deserialiser(string? json, IReadOnlyDictionary<string, TypeVariable> types)
    {
        var resultat = new Dictionary<string, object?>();
        if (string.IsNullOrWhiteSpace(json))
        {
            return resultat;
        }

        using var document = JsonDocument.Parse(json);
        foreach (var propriete in document.RootElement.EnumerateObject())
        {
            var type = types.TryGetValue(propriete.Name, out var declare)
                ? declare
                : VariablesProcessus.EstReserve(propriete.Name) ? TypeVariable.Texte : TypeVariable.Json;
            resultat[propriete.Name] = ConvertisseurValeurs.DepuisJson(type, propriete.Value, propriete.Name);
        }

        return resultat;
    }
}
