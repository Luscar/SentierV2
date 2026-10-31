namespace Sentier;

/// <summary>Une requête référencée n'est pas enregistrée.</summary>
public sealed class RequeteIntrouvableException : SentierException
{
    /// <summary>Crée l'exception.</summary>
    public RequeteIntrouvableException(string nomRequete)
        : base($"La requête '{nomRequete}' n'est pas enregistrée. Vérifiez les assemblys balayés par AddSentier.")
    {
    }
}
