using System.Data;

namespace Sentier;

/// <summary>Implémentation par défaut de <see cref="IAccesseurTransactionSentier"/>.</summary>
public sealed class AccesseurTransactionSentier : IAccesseurTransactionSentier
{
    /// <inheritdoc />
    public IDbTransaction? Transaction { get; set; }
}
