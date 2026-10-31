namespace Sentier;

/// <summary>Un paramètre de commande ou de requête est absent ou invalide.</summary>
public sealed class ParametreInvalideException : SentierException
{
    /// <summary>Crée l'exception.</summary>
    public ParametreInvalideException(string message) : base(message)
    {
    }
}
