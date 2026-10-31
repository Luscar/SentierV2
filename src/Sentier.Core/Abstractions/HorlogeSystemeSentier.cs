namespace Sentier;

/// <summary>Horloge système par défaut.</summary>
public sealed class HorlogeSystemeSentier : IHorlogeSentier
{
    /// <inheritdoc />
    public DateTime MaintenantUtc => DateTime.UtcNow;
}
