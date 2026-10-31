namespace Sentier;

/// <summary>Variable non déclarée, de mauvais type, ou obligatoire manquante.</summary>
public sealed class VariableInvalideException : SentierException
{
    /// <summary>Crée l'exception.</summary>
    public VariableInvalideException(string message) : base(message)
    {
    }
}
