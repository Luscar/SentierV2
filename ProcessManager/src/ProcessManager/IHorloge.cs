namespace ProcessManager;

/// <summary>Heure courante, injectable pour tester les délais.</summary>
public interface IHorloge
{
    DateTimeOffset Maintenant { get; }
}
