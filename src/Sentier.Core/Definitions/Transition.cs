namespace Sentier.Definitions;

/// <summary>Lien orienté d'un nœud vers un autre, créé par le constructeur.</summary>
public sealed class Transition
{
    /// <summary>Code du nœud source.</summary>
    public required string Source { get; init; }

    /// <summary>Code du nœud destination.</summary>
    public required string Destination { get; init; }

    /// <summary>Nom de la sortie (branche, déclencheur, code de fin) ; nul pour la sortie par défaut.</summary>
    public string? NomSortie { get; init; }

    /// <summary>Requête d'une branche conditionnelle de décision.</summary>
    public AppelRequete? Requete { get; init; }

    /// <summary>Condition sur variable d'une branche conditionnelle de décision (à la place d'une requête).</summary>
    public ConditionVariable? Condition { get; init; }

    /// <summary>Vrai pour la branche « sinon » d'une décision.</summary>
    public bool EstSinon { get; init; }
}
