using System.Text.Json.Serialization;

namespace Sentier.Definitions;

/// <summary>
/// Passerelle exclusive : évalue dans l'ordre la requête de chaque branche sortante et prend
/// la première vraie, sinon la branche « sinon ». Les branches sont portées par les transitions.
/// </summary>
public sealed class NoeudDecision : Noeud
{
    /// <inheritdoc />
    [JsonIgnore]
    public override TypeNoeud Type => TypeNoeud.Decision;
}
