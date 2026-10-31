namespace Sentier.Definitions;

/// <summary>Branche telle que déclarée par l'auteur, avant construction de son bloc.</summary>
internal sealed class BrancheDeclaree(string nomSortie)
{
    public string NomSortie { get; set; } = nomSortie;

    public Action<ConstructeurBloc>? Bloc { get; set; }

    public string? Lien { get; set; }

    public AppelRequete? Requete { get; init; }

    public bool EstSinon { get; init; }
}
