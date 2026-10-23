using System.Globalization;
using System.Text.Json;

namespace Sentier.Variables;

/// <summary>
/// Conversion des valeurs vers leur représentation canonique selon le type déclaré :
/// <see cref="string"/>, <see cref="long"/>, <see cref="decimal"/>, <see cref="bool"/>,
/// <see cref="DateTime"/> (UTC) ou <see cref="JsonElement"/>.
/// </summary>
public static class ConvertisseurValeurs
{
    /// <summary>Type CLR canonique associé à un type de variable.</summary>
    public static Type TypeClr(TypeVariable type) => type switch
    {
        TypeVariable.Texte => typeof(string),
        TypeVariable.Entier => typeof(long),
        TypeVariable.Decimal => typeof(decimal),
        TypeVariable.Booleen => typeof(bool),
        TypeVariable.Date => typeof(DateTime),
        _ => typeof(JsonElement),
    };

    /// <summary>Déduit le type de variable d'une valeur CLR, pour une valeur statique.</summary>
    public static TypeVariable DeduireType(object valeur) => valeur switch
    {
        string or char => TypeVariable.Texte,
        long or int or short or byte or sbyte or ushort or uint => TypeVariable.Entier,
        decimal or double or float => TypeVariable.Decimal,
        bool => TypeVariable.Booleen,
        DateTime or DateTimeOffset => TypeVariable.Date,
        _ => TypeVariable.Json,
    };

    /// <summary>
    /// Convertit une valeur dans la forme canonique du type déclaré.
    /// Lève <see cref="VariableInvalideException"/> si la valeur n'est pas compatible.
    /// </summary>
    /// <param name="type">Type déclaré.</param>
    /// <param name="valeur">Valeur à convertir.</param>
    /// <param name="nom">Nom de la variable ou du paramètre, pour les messages d'erreur.</param>
    public static object? Normaliser(TypeVariable type, object? valeur, string nom)
    {
        if (valeur is null)
        {
            return null;
        }

        if (valeur is JsonElement element)
        {
            return DepuisJson(type, element, nom);
        }

        object? resultat = type switch
        {
            TypeVariable.Texte => valeur switch
            {
                string s => s,
                char c => c.ToString(),
                _ => null,
            },
            TypeVariable.Entier => valeur switch
            {
                long l => l,
                int i => (long)i,
                short s => (long)s,
                byte b => (long)b,
                sbyte sb => (long)sb,
                ushort us => (long)us,
                uint ui => (long)ui,
                ulong ul when ul <= long.MaxValue => (long)ul,
                _ => null,
            },
            TypeVariable.Decimal => valeur switch
            {
                decimal d => d,
                double db => (decimal)db,
                float f => (decimal)f,
                long l => (decimal)l,
                int i => (decimal)i,
                short s => (decimal)s,
                byte b => (decimal)b,
                _ => null,
            },
            TypeVariable.Booleen => valeur is bool bo ? bo : null,
            TypeVariable.Date => valeur switch
            {
                DateTime dt => VersUtc(dt),
                DateTimeOffset dto => dto.UtcDateTime,
                _ => null,
            },
            TypeVariable.Json => JsonSerializer.SerializeToElement(valeur, valeur.GetType()),
            _ => null,
        };

        return resultat ?? throw new VariableInvalideException(
            $"La valeur de '{nom}' est de type {valeur.GetType().Name}, incompatible avec le type déclaré {type}.");
    }

    /// <summary>Convertit un élément JSON selon le type déclaré (jamais selon le type deviné par le JSON).</summary>
    public static object? DepuisJson(TypeVariable type, JsonElement element, string nom)
    {
        if (element.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return null;
        }

        object? resultat = type switch
        {
            TypeVariable.Texte when element.ValueKind == JsonValueKind.String => element.GetString(),
            TypeVariable.Entier when element.ValueKind == JsonValueKind.Number && element.TryGetInt64(out var l) => l,
            TypeVariable.Decimal when element.ValueKind == JsonValueKind.Number && element.TryGetDecimal(out var d) => d,
            TypeVariable.Booleen when element.ValueKind is JsonValueKind.True or JsonValueKind.False => element.GetBoolean(),
            TypeVariable.Date when element.ValueKind == JsonValueKind.String && element.TryGetDateTime(out var dt) => VersUtc(dt),
            TypeVariable.Json => element.Clone(),
            _ => null,
        };

        return resultat ?? throw new VariableInvalideException(
            $"La valeur JSON de '{nom}' ({element.ValueKind}) est incompatible avec le type déclaré {type}.");
    }

    /// <summary>Indique si une valeur d'un type source peut alimenter une variable d'un type cible.</summary>
    public static bool SontCompatibles(TypeVariable source, TypeVariable cible)
        => source == cible || (source == TypeVariable.Entier && cible == TypeVariable.Decimal) || cible == TypeVariable.Json;

    /// <summary>Convertit une valeur canonique vers le type demandé par l'appelant.</summary>
    public static T VersType<T>(object? valeur, string nom) => (T)VersType(valeur, typeof(T), nom)!;

    /// <summary>Convertit une valeur canonique vers le type demandé par l'appelant.</summary>
    public static object? VersType(object? valeur, Type type, string nom)
    {
        var cible = Nullable.GetUnderlyingType(type) ?? type;
        if (valeur is null)
        {
            return cible.IsValueType && Nullable.GetUnderlyingType(type) is null ? Activator.CreateInstance(cible) : null;
        }

        if (type.IsInstanceOfType(valeur))
        {
            return valeur;
        }

        try
        {
            if (valeur is JsonElement element)
            {
                return element.Deserialize(type);
            }

            if (cible.IsEnum && valeur is string texte)
            {
                return Enum.Parse(cible, texte, ignoreCase: true);
            }

            if (valeur is IConvertible && typeof(IConvertible).IsAssignableFrom(cible))
            {
                return Convert.ChangeType(valeur, cible, CultureInfo.InvariantCulture);
            }
        }
        catch (Exception ex) when (ex is InvalidCastException or FormatException or OverflowException or JsonException or ArgumentException or NotSupportedException)
        {
            throw new VariableInvalideException($"La valeur de '{nom}' ne peut pas être convertie en {type.Name} : {ex.Message}");
        }

        throw new VariableInvalideException($"La valeur de '{nom}' ({valeur.GetType().Name}) ne peut pas être convertie en {type.Name}.");
    }

    private static DateTime VersUtc(DateTime date) => date.Kind switch
    {
        DateTimeKind.Utc => date,
        DateTimeKind.Local => date.ToUniversalTime(),
        _ => DateTime.SpecifyKind(date, DateTimeKind.Utc),
    };
}
