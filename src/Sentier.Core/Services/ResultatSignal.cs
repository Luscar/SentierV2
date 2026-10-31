namespace Sentier;

/// <summary>Résultat de l'envoi d'un signal.</summary>
public sealed record ResultatSignal
{
    /// <summary>Nombre d'instances reprises.</summary>
    public int NombreInstancesReprises => Resultats.Count(r => !r.SansEffet);

    /// <summary>Résultat par instance visée.</summary>
    public IReadOnlyList<ResultatExecution> Resultats { get; init; } = [];

    /// <summary>Vrai si aucune instance n'a été reprise.</summary>
    public bool SansEffet => NombreInstancesReprises == 0;

    /// <summary>Raison de l'absence d'effet.</summary>
    public string? Raison { get; init; }
}
