namespace Sentier;

/// <summary>Déclencheur en cours (échéance ou signal) d'une instance suspendue sur un nœud d'attente.</summary>
public sealed class Attente
{
    /// <summary>Identifiant de l'attente.</summary>
    public IdAttente Id { get; internal set; }

    /// <summary>Instance en attente.</summary>
    public IdInstance IdInstance { get; internal set; }

    /// <summary>Code du nœud d'attente.</summary>
    public string CodeNoeud { get; internal set; } = "";

    /// <summary>Type du déclencheur.</summary>
    public TypeDeclencheur TypeDeclencheur { get; internal set; }

    /// <summary>Nom de la sortie suivie si ce déclencheur gagne.</summary>
    public string NomSortie { get; internal set; } = "";

    /// <summary>Date (UTC) d'échéance, pour un déclencheur d'échéance.</summary>
    public DateTime? DateEcheance { get; internal set; }

    /// <summary>Nom du signal attendu, pour un déclencheur de signal.</summary>
    public string? NomSignal { get; internal set; }

    /// <summary>Clé de corrélation du signal, optionnelle.</summary>
    public string? CleCorrelation { get; internal set; }

    /// <summary>Date (UTC) de création.</summary>
    public DateTime DateCreation { get; internal set; }
}
