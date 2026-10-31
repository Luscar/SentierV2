namespace Sentier;

/// <summary>Aucune transition sortante ne correspond au résultat d'un nœud.</summary>
public sealed class AucuneTransitionException : SentierException
{
    /// <summary>Crée l'exception.</summary>
    public AucuneTransitionException(string cleProcessus, string codeNoeud, string? nomSortie)
        : base(nomSortie is null
            ? $"Processus '{cleProcessus}' : aucune transition sortante pour le nœud '{codeNoeud}'."
            : $"Processus '{cleProcessus}' : aucune transition '{nomSortie}' pour le nœud '{codeNoeud}'.")
    {
    }
}
