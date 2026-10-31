using System.Text.Json.Serialization;

namespace Sentier.Definitions;

/// <summary>
/// Suspend l'instance jusqu'au premier de ses déclencheurs (une échéance et/ou des signaux).
/// Chaque déclencheur a sa propre sortie.
/// </summary>
public sealed class NoeudAttente : Noeud
{
    /// <inheritdoc />
    [JsonIgnore]
    public override TypeNoeud Type => TypeNoeud.Attente;

    /// <inheritdoc />
    [JsonIgnore]
    public override bool EstSuspensif => true;

    /// <summary>Commandes exécutées à l'entrée, avant l'enregistrement des déclencheurs.</summary>
    public IReadOnlyList<AppelCommande> CommandesEntree { get; init; } = [];

    /// <summary>Déclencheurs (au moins un, au plus une échéance).</summary>
    public IReadOnlyList<Declencheur> Declencheurs { get; init; } = [];
}
