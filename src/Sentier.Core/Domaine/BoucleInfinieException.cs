namespace Sentier;

/// <summary>Le nombre maximal d'étapes par exécution a été dépassé.</summary>
public sealed class BoucleInfinieException : SentierException
{
    /// <summary>Crée l'exception.</summary>
    public BoucleInfinieException(IdInstance idInstance, int maxEtapes)
        : base($"L'instance {idInstance} a dépassé le nombre maximal de {maxEtapes} étapes par exécution : boucle infinie probable.")
    {
    }
}
