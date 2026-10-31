using System.Text.Json.Serialization;

namespace Sentier.Definitions;

/// <summary>Point d'entrée unique du processus.</summary>
public sealed class NoeudDebut : Noeud
{
    /// <inheritdoc />
    [JsonIgnore]
    public override TypeNoeud Type => TypeNoeud.Debut;
}
