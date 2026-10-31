namespace Sentier;

/// <summary>Horloge système par défaut.</summary>
public sealed class HorlogeSysteme : IHorloge
{
    /// <inheritdoc />
    public DateTime MaintenantUtc => DateTime.UtcNow;
}
