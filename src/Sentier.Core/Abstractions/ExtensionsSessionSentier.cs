using System.Data;

namespace Sentier;

/// <summary>Méthodes utilitaires sur <see cref="ISessionSentier"/>.</summary>
public static class ExtensionsSessionSentier
{
    /// <summary>
    /// Retourne la transaction du client, ou <c>null</c> s'il travaille dans une transaction ambiante
    /// (<see cref="System.Transactions.TransactionScope"/>) : les commandes sont alors exécutées sans transaction
    /// explicite, sur la connexion enrôlée dans la portée. Sinon, lève <see cref="TransactionRequiseException"/>.
    /// Une transaction validée ou annulée (sans connexion) n'est plus considérée comme active.
    /// </summary>
    public static IDbTransaction? ExigerTransaction(this ISessionSentier session)
    {
        var transaction = session.Transaction;
        if (transaction is { Connection: not null })
        {
            return transaction;
        }

        if (transaction is null && System.Transactions.Transaction.Current?.TransactionInformation.Status == System.Transactions.TransactionStatus.Active)
        {
            return null;
        }

        throw new TransactionRequiseException();
    }
}
