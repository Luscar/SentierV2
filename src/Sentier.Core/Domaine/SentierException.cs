namespace Sentier;

/// <summary>Exception de base de toutes les erreurs levées par Sentier.</summary>
public class SentierException : Exception
{
    /// <summary>Crée une exception Sentier.</summary>
    public SentierException(string message, Exception? interne = null) : base(message, interne)
    {
    }
}
