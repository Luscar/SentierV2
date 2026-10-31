using System.Data;

namespace Sentier;

/// <summary>
/// Fournit à Sentier la connexion et la transaction courantes du client.
/// Enregistrée en Scoped. Sentier n'ouvre, ne valide et n'annule jamais de transaction.
/// </summary>
public interface ISessionSentier
{
    /// <summary>Connexion du client, déjà ouverte.</summary>
    IDbConnection Connexion { get; }

    /// <summary>
    /// Transaction courante du client, ou <c>null</c> si aucune n'est active. Peut rester <c>null</c> si le client
    /// travaille dans un <see cref="System.Transactions.TransactionScope"/> où la connexion est enrôlée.
    /// </summary>
    IDbTransaction? Transaction { get; }
}
