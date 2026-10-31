using System.Data;

namespace Sentier;

/// <summary>Méthodes utilitaires sur <see cref="ISessionSentier"/>.</summary>
public static class ExtensionsSessionSentier
{
    /// <summary>
    /// Retourne la transaction active, ou lève <see cref="TransactionRequiseException"/>.
    /// Une transaction validée ou annulée (sans connexion) n'est plus considérée comme active.
    /// </summary>
    public static IDbTransaction ExigerTransaction(this ISessionSentier session)
    {
        var transaction = session.Transaction;
        if (transaction is null || transaction.Connection is null)
        {
            throw new TransactionRequiseException();
        }

        return transaction;
    }
}
