namespace Sentier;

/// <summary>Une commande référencée n'est pas enregistrée.</summary>
public sealed class CommandeIntrouvableException : SentierException
{
    /// <summary>Crée l'exception.</summary>
    public CommandeIntrouvableException(string nomCommande)
        : base($"La commande '{nomCommande}' n'est pas enregistrée. Vérifiez les assemblys balayés par AddSentier.")
    {
    }
}
