namespace Sentier.Definitions;

internal sealed class BrancheEnConstruction(string nomSortie)
{
    public string NomSortie { get; } = nomSortie;

    public SequenceEnConstruction? Bloc { get; set; }

    public string? Lien { get; init; }

    public AppelRequete? Requete { get; init; }

    public bool EstSinon { get; init; }
}
