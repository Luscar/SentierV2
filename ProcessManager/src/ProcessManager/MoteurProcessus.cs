namespace ProcessManager;

/// <summary>
/// Exécute les processus jusqu'à leur prochaine suspension ou leur fin, puis commit une seule fois par point d'entrée.
/// </summary>
public sealed class MoteurProcessus(IStoreInstances store, IUniteDeTravail uniteDeTravail, IFabriqueProcessus fabrique, IHorloge horloge)
{
    /// <summary>Démarre un processus en appelant sa méthode Demarrer. Retourne l'identifiant de l'instance.</summary>
    public async Task<Guid> Demarrer<TProcessus>(Func<TProcessus, Task> demarrer, CancellationToken ct = default)
        where TProcessus : ProcessusBase
    {
        var id = await DemarrerInstance(typeof(TProcessus), p => demarrer((TProcessus)p), null, ct);
        await uniteDeTravail.Commit(ct);
        return id;
    }

    /// <summary>Envoie un événement à une instance en attente. Retourne false (sans rien faire) si elle n'est pas en attente.</summary>
    public async Task<bool> Signaler(Guid id, object evenement, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(evenement);
        return await ReveillerEtCommit(await store.Charger(id, ct), _ => evenement, ct);
    }

    /// <summary>Envoie un événement à l'instance en attente du type donné portant la clé de corrélation donnée.</summary>
    public async Task<bool> Signaler<TProcessus>(string cleCorrelation, object evenement, CancellationToken ct = default)
        where TProcessus : ProcessusBase
    {
        ArgumentNullException.ThrowIfNull(evenement);
        var instance = await store.TrouverEnAttente(NomType(typeof(TProcessus)), cleCorrelation, ct);
        return await ReveillerEtCommit(instance, _ => evenement, ct);
    }

    /// <summary>Abandonne l'attente d'une instance ; elle reçoit <see cref="AttenteAbandonnee"/>.</summary>
    public async Task<bool> Abandonner(Guid id, string? raison = null, CancellationToken ct = default) =>
        await ReveillerEtCommit(await store.Charger(id, ct), i => new AttenteAbandonnee(i.Etape!, raison), ct);

    /// <summary>Réveille avec <see cref="DelaiExpire"/> les instances dont l'échéance est dépassée. Retourne leur nombre.</summary>
    public async Task<int> Tick(CancellationToken ct = default)
    {
        var maintenant = horloge.Maintenant;
        var reveillees = 0;
        foreach (var echue in await store.ListerEchues(maintenant, ct))
        {
            // Rechargée : elle a pu changer depuis la liste (échéance modifiée, déjà réveillée…).
            var instance = await store.Charger(echue.Id, ct);
            if (instance is not { Statut: StatutProcessus.EnAttente, Echeance: { } echeance } || echeance > maintenant)
                continue;
            await Reveiller(instance, new DelaiExpire(instance.Etape!), ct);
            reveillees++;
        }
        if (reveillees > 0)
            await uniteDeTravail.Commit(ct);
        return reveillees;
    }

    private async Task<bool> ReveillerEtCommit(InstanceProcessus? instance, Func<InstanceProcessus, object> message, CancellationToken ct)
    {
        if (instance is not { Statut: StatutProcessus.EnAttente })
            return false;
        await Reveiller(instance, message(instance), ct);
        await uniteDeTravail.Commit(ct);
        return true;
    }

    private async Task<Guid> DemarrerInstance(Type type, Func<ProcessusBase, Task> demarrer, Guid? parentId, CancellationToken ct)
    {
        var instance = new InstanceProcessus { Id = Guid.CreateVersion7(), TypeProcessus = NomType(type), ParentId = parentId };
        await Executer(instance, Creer(type, instance), demarrer, ct);
        return instance.Id;
    }

    private async Task Reveiller(InstanceProcessus instance, object message, CancellationToken ct)
    {
        var processus = Creer(Type.GetType(instance.TypeProcessus, throwOnError: true)!, instance);
        processus.RestaurerEtat(instance.Etat);
        await Executer(instance, processus, p => Handlers.Invoquer(p, message), ct);
    }

    private ProcessusBase Creer(Type type, InstanceProcessus instance)
    {
        var processus = fabrique.Creer(type);
        if (processus.GetType() != type)
            throw new ProcessusException($"La fabrique a créé '{processus.GetType().Name}' au lieu de '{type.Name}'.");
        processus.Attacher(instance);
        return processus;
    }

    private async Task Executer(InstanceProcessus instance, ProcessusBase processus, Func<ProcessusBase, Task> premierAppel, CancellationToken ct)
    {
        instance.Statut = StatutProcessus.EnCours;
        instance.Echeance = null;

        await premierAppel(processus);
        while (processus.Defiler(out var evenement))
            await Handlers.Invoquer(processus, evenement);

        instance.Etat = processus.SerialiserEtat();

        switch (processus.Suite)
        {
            case Suspension suspension:
                instance.Etape = suspension.Etape;
                instance.Echeance = suspension.Echeance ?? (horloge.Maintenant + suspension.Delai);
                instance.Statut = StatutProcessus.EnAttente;
                await store.Sauvegarder(instance, ct);
                break;

            case Appel appel:
                instance.Etape = appel.Etape;
                instance.Statut = StatutProcessus.AttenteEnfant;
                await store.Sauvegarder(instance, ct);
                await DemarrerInstance(appel.Type, appel.Demarrer, instance.Id, ct);
                break;

            case Fin fin:
                instance.Statut = StatutProcessus.Termine;
                await store.Sauvegarder(instance, ct);
                if (instance.ParentId is { } parentId && await store.Charger(parentId, ct) is { Statut: StatutProcessus.AttenteEnfant } parent)
                    await Reveiller(parent, new SousProcessusTermine(parent.Etape!, instance.Id, fin.Resultat), ct);
                break;

            default:
                throw new ProcessusException(
                    $"Le processus '{processus.GetType().Name}' (instance {instance.Id}) s'est arrêté sans suspension ni fin (processus sans suite).");
        }
    }

    internal static string NomType(Type type) => $"{type.FullName}, {type.Assembly.GetName().Name}";
}
