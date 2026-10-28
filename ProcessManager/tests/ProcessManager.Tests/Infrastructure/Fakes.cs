using ProcessManager;

namespace ProcessManager.Tests.Infrastructure;

public sealed class HorlogeTest : IHorloge
{
    public DateTimeOffset Maintenant { get; set; } = new(2026, 1, 1, 8, 0, 0, TimeSpan.Zero);

    public void Avancer(TimeSpan duree) => Maintenant += duree;
}

/// <summary>
/// Store en mémoire transactionnel : les sauvegardes sont en attente jusqu'au commit.
/// Les instances sont copiées en entrée et en sortie, comme avec une base de données.
/// </summary>
public sealed class StoreMemoire : IStoreInstances, IUniteDeTravail
{
    private readonly Dictionary<Guid, InstanceProcessus> _commitees = new();
    private readonly Dictionary<Guid, InstanceProcessus> _enCours = new();

    public int Sauvegardes { get; private set; }
    public int Commits { get; private set; }

    public IEnumerable<InstanceProcessus> Commitees => _commitees.Values.Select(i => i with { });

    public InstanceProcessus? Lire(Guid id) => _commitees.TryGetValue(id, out var i) ? i with { } : null;

    public void Annuler() => _enCours.Clear();

    public Task Commit(CancellationToken ct = default)
    {
        foreach (var (id, instance) in _enCours)
            _commitees[id] = instance;
        _enCours.Clear();
        Commits++;
        return Task.CompletedTask;
    }

    public Task<InstanceProcessus?> Charger(Guid id, CancellationToken ct = default) =>
        Task.FromResult(Courante(id) is { } i ? i with { } : null);

    public Task Sauvegarder(InstanceProcessus instance, CancellationToken ct = default)
    {
        var versionStockee = Courante(instance.Id)?.Version ?? 0;
        if (versionStockee != instance.Version)
            throw new InvalidOperationException($"Conflit de version sur {instance.Id} : {instance.Version} au lieu de {versionStockee}.");
        instance.Version++;
        _enCours[instance.Id] = instance with { };
        Sauvegardes++;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<InstanceProcessus>> ListerEchues(DateTimeOffset maintenant, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<InstanceProcessus>>(Vue()
            .Where(i => i.Statut == StatutProcessus.EnAttente && i.Echeance <= maintenant)
            .Select(i => i with { })
            .ToList());

    public Task<InstanceProcessus?> TrouverEnAttente(string typeProcessus, string cleCorrelation, CancellationToken ct = default) =>
        Task.FromResult(Vue().FirstOrDefault(i =>
            i.Statut == StatutProcessus.EnAttente && i.TypeProcessus == typeProcessus && i.CleCorrelation == cleCorrelation) is { } i
            ? i with { }
            : null);

    private InstanceProcessus? Courante(Guid id) =>
        _enCours.TryGetValue(id, out var i) || _commitees.TryGetValue(id, out i) ? i : null;

    private IEnumerable<InstanceProcessus> Vue() =>
        _commitees.Keys.Union(_enCours.Keys).Select(id => Courante(id)!);
}

/// <summary>Fabrique minimale jouant le rôle du conteneur DI.</summary>
public sealed class FabriqueTest : IFabriqueProcessus
{
    private readonly Dictionary<Type, Func<ProcessusBase>> _fabriques = new();

    public FabriqueTest Enregistrer<TProcessus>(Func<TProcessus> fabrique) where TProcessus : ProcessusBase
    {
        _fabriques[typeof(TProcessus)] = fabrique;
        return this;
    }

    public ProcessusBase Creer(Type typeProcessus) => _fabriques[typeProcessus]();
}
