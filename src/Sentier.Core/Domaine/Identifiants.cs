using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sentier;

/// <summary>Identifiant d'une version de définition de processus.</summary>
[JsonConverter(typeof(ConvertisseurJsonIdentifiant<IdDefinition>))]
public readonly record struct IdDefinition(long Valeur) : IIdentifiant<IdDefinition>
{
    /// <inheritdoc />
    public static IdDefinition Depuis(long valeur) => new(valeur);

    /// <inheritdoc />
    public override string ToString() => Valeur.ToString(CultureInfo.InvariantCulture);
}

/// <summary>Identifiant d'une instance de processus.</summary>
[JsonConverter(typeof(ConvertisseurJsonIdentifiant<IdInstance>))]
public readonly record struct IdInstance(long Valeur) : IIdentifiant<IdInstance>
{
    /// <inheritdoc />
    public static IdInstance Depuis(long valeur) => new(valeur);

    /// <inheritdoc />
    public override string ToString() => Valeur.ToString(CultureInfo.InvariantCulture);
}

/// <summary>Identifiant d'un passage dans l'historique des nœuds.</summary>
[JsonConverter(typeof(ConvertisseurJsonIdentifiant<IdPassageNoeud>))]
public readonly record struct IdPassageNoeud(long Valeur) : IIdentifiant<IdPassageNoeud>
{
    /// <inheritdoc />
    public static IdPassageNoeud Depuis(long valeur) => new(valeur);

    /// <inheritdoc />
    public override string ToString() => Valeur.ToString(CultureInfo.InvariantCulture);
}

/// <summary>Identifiant d'une attente (un déclencheur en cours).</summary>
[JsonConverter(typeof(ConvertisseurJsonIdentifiant<IdAttente>))]
public readonly record struct IdAttente(long Valeur) : IIdentifiant<IdAttente>
{
    /// <inheritdoc />
    public static IdAttente Depuis(long valeur) => new(valeur);

    /// <inheritdoc />
    public override string ToString() => Valeur.ToString(CultureInfo.InvariantCulture);
}

/// <summary>Identifiant d'une trace de migration.</summary>
[JsonConverter(typeof(ConvertisseurJsonIdentifiant<IdMigration>))]
public readonly record struct IdMigration(long Valeur) : IIdentifiant<IdMigration>
{
    /// <inheritdoc />
    public static IdMigration Depuis(long valeur) => new(valeur);

    /// <inheritdoc />
    public override string ToString() => Valeur.ToString(CultureInfo.InvariantCulture);
}

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
