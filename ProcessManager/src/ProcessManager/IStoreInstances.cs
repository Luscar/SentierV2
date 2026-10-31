namespace ProcessManager;

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
