using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sentier;

/// <summary>Identifiant d'un passage dans l'historique des nœuds.</summary>
[JsonConverter(typeof(ConvertisseurJsonIdentifiant<IdPassageNoeud>))]
public readonly record struct IdPassageNoeud(long Valeur) : IIdentifiant<IdPassageNoeud>
{
    /// <inheritdoc />
    public static IdPassageNoeud Depuis(long valeur) => new(valeur);

    /// <inheritdoc />
    public override string ToString() => Valeur.ToString(CultureInfo.InvariantCulture);
}
