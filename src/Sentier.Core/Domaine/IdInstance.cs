using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sentier;

/// <summary>Identifiant d'une instance de processus.</summary>
[JsonConverter(typeof(ConvertisseurJsonIdentifiant<IdInstance>))]
public readonly record struct IdInstance(long Valeur) : IIdentifiant<IdInstance>
{
    /// <inheritdoc />
    public static IdInstance Depuis(long valeur) => new(valeur);

    /// <inheritdoc />
    public override string ToString() => Valeur.ToString(CultureInfo.InvariantCulture);
}
