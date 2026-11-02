namespace Sentier.Integration.Tests.Infrastructure;

/// <summary>Horloge pilotée par le test : les échéances se déclenchent en avançant le temps.</summary>
public sealed class HorlogeTest : IHorlogeSentier
{
    public DateTime MaintenantUtc { get; private set; } = new(2026, 3, 2, 9, 0, 0, DateTimeKind.Utc);

    public void Avancer(TimeSpan duree) => MaintenantUtc += duree;
}
