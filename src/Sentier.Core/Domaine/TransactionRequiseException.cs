namespace Sentier;

/// <summary>Aucune transaction n'est active au moment de l'appel.</summary>
public sealed class TransactionRequiseException : SentierException
{
    /// <summary>Crée l'exception.</summary>
    public TransactionRequiseException()
        : base("Sentier exige une transaction active, fournie par le client via ISessionSentier. Sentier n'ouvre jamais de transaction lui-même.")
    {
    }
}
