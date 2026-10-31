using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sentier;

/// <summary>Identifiant d'une attente (un déclencheur en cours).</summary>
[JsonConverter(typeof(ConvertisseurJsonIdentifiant<IdAttente>))]
public readonly record struct IdAttente(long Valeur) : IIdentifiant<IdAttente>
{
    /// <inheritdoc />
    public static IdAttente Depuis(long valeur) => new(valeur);

    /// <inheritdoc />
    public override string ToString() => Valeur.ToString(CultureInfo.InvariantCulture);
}
