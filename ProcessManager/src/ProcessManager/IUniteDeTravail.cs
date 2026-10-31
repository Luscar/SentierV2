namespace ProcessManager;

/// <summary>Transaction applicative. Le moteur ne commit qu'aux points de suspension.</summary>
public interface IUniteDeTravail
{
    Task Commit(CancellationToken ct = default);
}
