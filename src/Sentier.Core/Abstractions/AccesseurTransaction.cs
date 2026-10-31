using System.Data;

namespace Sentier;

/// <summary>Implémentation par défaut de <see cref="IAccesseurTransaction"/>.</summary>
public sealed class AccesseurTransaction : IAccesseurTransaction
{
    /// <inheritdoc />
    public IDbTransaction? Transaction { get; set; }
}
