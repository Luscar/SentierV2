namespace Sentier.Definitions;

internal sealed class ElementSequence(Noeud noeud)
{
    public Noeud Noeud { get; } = noeud;

    public string? Lien { get; set; }

    public List<BrancheEnConstruction>? Branches { get; set; }
}
