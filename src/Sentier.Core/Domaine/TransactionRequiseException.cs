namespace Sentier;

/// <summary>Aucune transaction n'est active au moment de l'appel.</summary>
public sealed class TransactionRequiseException : SentierException
{
    /// <summary>Crée l'exception.</summary>
    public TransactionRequiseException()
        : base("Sentier exige une transaction active : une IDbTransaction fournie par le client via ISessionSentier, ou une transaction ambiante (TransactionScope). Sentier n'ouvre jamais de transaction lui-même.")
    {
    }

    /// <summary>Crée l'exception avec un message précis.</summary>
    public TransactionRequiseException(string message)
        : base(message)
    {
    }
}
