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
