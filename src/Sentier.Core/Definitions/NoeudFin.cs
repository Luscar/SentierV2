using System.Text.Json.Serialization;

namespace Sentier.Definitions;

/// <summary>Termine l'instance. Le code du nœud est le code de fin.</summary>
public sealed class NoeudFin : Noeud
{
    /// <inheritdoc />
    [JsonIgnore]
    public override TypeNoeud Type => TypeNoeud.Fin;
}
