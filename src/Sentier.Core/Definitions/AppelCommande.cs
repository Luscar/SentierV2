namespace Sentier.Definitions;

/// <summary>Utilisation d'une commande dans une définition, référencée par nom.</summary>
public sealed class AppelCommande
{
    /// <summary>Nom de la commande enregistrée.</summary>
    public required string Nom { get; init; }

    /// <summary>Paramètres de l'appel.</summary>
    public IReadOnlyList<Parametre> Parametres { get; init; } = [];
}
