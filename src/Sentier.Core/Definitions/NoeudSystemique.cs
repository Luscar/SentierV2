using System.Text.Json.Serialization;

namespace Sentier.Definitions;

/// <summary>Exécute une commande obligatoire.</summary>
public sealed class NoeudSystemique : Noeud
{
    /// <inheritdoc />
    [JsonIgnore]
    public override TypeNoeud Type => TypeNoeud.Systemique;

    /// <summary>Commande exécutée.</summary>
    public required AppelCommande Commande { get; init; }
}
