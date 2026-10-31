namespace Sentier;

/// <summary>Entrée de l'historique : un nœud traversé par une instance.</summary>
public sealed class PassageNoeud
{
    /// <summary>Identifiant du passage.</summary>
    public IdPassageNoeud Id { get; internal set; }

    /// <summary>Instance concernée.</summary>
    public IdInstance IdInstance { get; internal set; }

    /// <summary>Code du nœud traversé.</summary>
    public string CodeNoeud { get; internal set; } = "";

    /// <summary>Type du nœud traversé.</summary>
    public TypeNoeud TypeNoeud { get; internal set; }

    /// <summary>Date (UTC) d'entrée dans le nœud.</summary>
    public DateTime DateEntree { get; internal set; }

    /// <summary>Date (UTC) de sortie, nulle tant que le nœud est suspendu.</summary>
    public DateTime? DateSortie { get; internal set; }

    /// <summary>Résultat du passage (sortie choisie, déclencheur gagnant, code de fin…).</summary>
    public string? Resultat { get; internal set; }
}
