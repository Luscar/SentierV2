namespace Sentier;

/// <summary>L'instance a été modifiée par un autre traitement (verrou optimiste).</summary>
public sealed class ConflitConcurrenceException : SentierException
{
    /// <summary>Crée l'exception.</summary>
    public ConflitConcurrenceException(IdInstance idInstance, int versionAttendue)
        : base($"L'instance {idInstance} a été modifiée par un autre traitement (version attendue : {versionAttendue}).")
    {
        IdInstance = idInstance;
    }

    /// <summary>Instance en conflit.</summary>
    public IdInstance IdInstance { get; }
}
