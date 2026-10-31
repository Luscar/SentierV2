namespace Sentier.Variables;

/// <summary>Sérialisation des variables d'une instance.</summary>
public interface ISerialiseurVariables
{
    /// <summary>Sérialise des valeurs canoniques en JSON.</summary>
    string Serialiser(IReadOnlyDictionary<string, object?> valeurs);

    /// <summary>
    /// Désérialise un JSON en utilisant les types déclarés (jamais le type deviné par le JSON).
    /// Les variables système (<c>_…</c>) sont relues en texte, les clés non déclarées en JSON brut.
    /// </summary>
    Dictionary<string, object?> Deserialiser(string? json, IReadOnlyDictionary<string, TypeVariable> types);
}
