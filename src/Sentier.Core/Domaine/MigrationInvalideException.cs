namespace Sentier;

/// <summary>Un plan de migration est incomplet ou incohérent.</summary>
public sealed class MigrationInvalideException : SentierException
{
    /// <summary>Crée l'exception.</summary>
    public MigrationInvalideException(string message) : base(message)
    {
    }
}
