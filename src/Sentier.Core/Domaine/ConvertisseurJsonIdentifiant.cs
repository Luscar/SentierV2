using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sentier;

/// <summary>Sérialise un identifiant typé comme un simple nombre JSON.</summary>
/// <typeparam name="T">Type d'identifiant.</typeparam>
public sealed class ConvertisseurJsonIdentifiant<T> : JsonConverter<T> where T : struct, IIdentifiant<T>
{
    /// <inheritdoc />
    public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => T.Depuis(reader.GetInt64());

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
        => writer.WriteNumberValue(value.Valeur);
}
