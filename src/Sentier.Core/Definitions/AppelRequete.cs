namespace Sentier.Definitions;

/// <summary>Utilisation d'une requête sur une branche de décision, référencée par nom.</summary>
public sealed class AppelRequete
{
    /// <summary>Nom de la requête enregistrée.</summary>
    public required string Nom { get; init; }

    /// <summary>Paramètres de l'appel.</summary>
    public IReadOnlyList<Parametre> Parametres { get; init; } = [];

    /// <summary>Inverse le résultat de la requête.</summary>
    public bool Inverser { get; init; }
}
