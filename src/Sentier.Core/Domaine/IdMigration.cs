using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sentier;

/// <summary>Identifiant d'une trace de migration.</summary>
[JsonConverter(typeof(ConvertisseurJsonIdentifiant<IdMigration>))]
public readonly record struct IdMigration(long Valeur) : IIdentifiant<IdMigration>
{
    /// <inheritdoc />
    public static IdMigration Depuis(long valeur) => new(valeur);

    /// <inheritdoc />
    public override string ToString() => Valeur.ToString(CultureInfo.InvariantCulture);
}
