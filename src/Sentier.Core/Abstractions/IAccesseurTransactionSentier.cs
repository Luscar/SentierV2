using System.Data;

namespace Sentier;

/// <summary>
/// Accesseur que le client alimente avec sa transaction courante, utilisé par
/// l'implémentation par défaut de <see cref="ISessionSentier"/>. Enregistré en Scoped.
/// </summary>
public interface IAccesseurTransactionSentier
{
    /// <summary>Transaction courante du client.</summary>
    IDbTransaction? Transaction { get; set; }
}
