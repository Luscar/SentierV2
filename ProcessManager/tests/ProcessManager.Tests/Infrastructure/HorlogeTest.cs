using ProcessManager;

namespace ProcessManager.Tests.Infrastructure;

public sealed class HorlogeTest : IHorloge
{
    public DateTimeOffset Maintenant { get; set; } = new(2026, 1, 1, 8, 0, 0, TimeSpan.Zero);

    public void Avancer(TimeSpan duree) => Maintenant += duree;
}
