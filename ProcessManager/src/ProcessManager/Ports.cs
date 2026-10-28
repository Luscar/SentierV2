namespace ProcessManager;

/// <summary>Heure courante, injectable pour tester les délais.</summary>
public interface IHorloge
{
    DateTimeOffset Maintenant { get; }
}

/// <summary>Horloge système par défaut.</summary>
public sealed class HorlogeSysteme : IHorloge
{
    public DateTimeOffset Maintenant => DateTimeOffset.UtcNow;
}

/// <summary>Transaction applicative. Le moteur ne commit qu'aux points de suspension.</summary>
public interface IUniteDeTravail
{
    Task Commit(CancellationToken ct = default);
}

/// <summary>Crée un processus à partir de son type (typiquement via le conteneur DI).</summary>
public interface IFabriqueProcessus
{
    ProcessusBase Creer(Type typeProcessus);
}

/// <summary>Persistance des instances, dans la transaction de l'unité de travail.</summary>
public interface IStoreInstances
{
    Task<InstanceProcessus?> Charger(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Insère (Version = 0) ou met à jour l'instance si la version stockée est égale à <see cref="InstanceProcessus.Version"/>,
    /// puis incrémente <see cref="InstanceProcessus.Version"/>. Lève une exception en cas de conflit de version.
    /// </summary>
    Task Sauvegarder(InstanceProcessus instance, CancellationToken ct = default);

    /// <summary>Instances EnAttente dont l'échéance est inférieure ou égale à <paramref name="maintenant"/>.</summary>
    Task<IReadOnlyList<InstanceProcessus>> ListerEchues(DateTimeOffset maintenant, CancellationToken ct = default);

    /// <summary>Instance EnAttente du type donné portant la clé de corrélation donnée, ou null.</summary>
    Task<InstanceProcessus?> TrouverEnAttente(string typeProcessus, string cleCorrelation, CancellationToken ct = default);
}
