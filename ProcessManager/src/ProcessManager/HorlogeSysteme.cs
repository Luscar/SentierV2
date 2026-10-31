namespace ProcessManager;

/// <summary>Horloge système par défaut.</summary>
public sealed class HorlogeSysteme : IHorloge
{
    public DateTimeOffset Maintenant => DateTimeOffset.UtcNow;
}
