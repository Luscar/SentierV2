using Sentier;

namespace Exemples.DeclarationSousProcessus;

/// <summary>Horloge déplacée à la main, pour simuler le passage du temps jusqu'à l'échéance de comptabilisation.</summary>
public sealed class HorlogeSimulee : IHorloge
{
    public DateTime MaintenantUtc { get; set; } = new(2026, 3, 2, 9, 0, 0, DateTimeKind.Utc);
}
