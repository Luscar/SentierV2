using System.Text.Json;

namespace ProcessManager;

/// <summary>
/// Classe de base des processus. Un processus expose sa propre méthode Demarrer et une méthode Handle par type d'événement.
/// Les méthodes de suspension ne font que déclarer une intention ; le moteur l'applique en fin d'exécution.
/// </summary>
public abstract class ProcessusBase
{
    private readonly Queue<object> _file = new();
    private InstanceProcessus? _instance;

    public Guid Id => Instance.Id;
    public string? Etape => Instance.Etape;

    internal InstanceProcessus Instance =>
        _instance ?? throw new ProcessusException($"Le processus '{GetType().Name}' n'est attaché à aucune instance : il doit être exécuté par le moteur.");

    internal Suite? Suite { get; private set; }

    /// <summary>Met un événement interne en file ; il sera traité par le Handle correspondant de ce processus.</summary>
    protected void Raise(object evenement)
    {
        ArgumentNullException.ThrowIfNull(evenement);
        if (Suite is not null)
            throw new ProcessusException(
                $"Le processus '{GetType().Name}' a levé l'événement '{evenement.GetType().Name}' après un point de suspension ({Suite.Description}).");
        _file.Enqueue(evenement);
    }

    /// <summary>Associe une clé métier à l'instance, pour la retrouver lors d'un signal.</summary>
    protected void Correler(string cle) => Instance.CleCorrelation = cle;

    /// <summary>Déclare une tâche utilisateur (alias de <see cref="Attendre(string, TimeSpan?)"/>).</summary>
    protected void TacheUser(string etape, TimeSpan? delai = null) => Attendre(etape, delai);

    /// <summary>Déclare une attente, avec un délai relatif optionnel.</summary>
    protected void Attendre(string etape, TimeSpan? delai = null) => Declarer(new Suspension(etape, delai, null));

    /// <summary>Déclare une attente jusqu'à une date fixe.</summary>
    protected void Attendre(string etape, DateTimeOffset echeance) => Declarer(new Suspension(etape, null, echeance));

    /// <summary>Déclare l'appel d'un sous-processus ; le parent est suspendu jusqu'à <see cref="SousProcessusTermine"/>.</summary>
    protected void Appeler<TProcessus>(string etape, Func<TProcessus, Task> demarrer) where TProcessus : ProcessusBase =>
        Declarer(new Appel(etape, typeof(TProcessus), p => demarrer((TProcessus)p)));

    /// <summary>Termine le processus, avec un résultat optionnel.</summary>
    protected void Terminer(string? resultat = null) => Declarer(new Fin(resultat));

    private void Declarer(Suite suite)
    {
        if (Suite is not null)
            throw new ProcessusException(
                $"Le processus '{GetType().Name}' a déjà déclaré une suite ({Suite.Description}) et ne peut pas déclarer en plus : {suite.Description}.");
        Suite = suite;
    }

    internal void Attacher(InstanceProcessus instance)
    {
        _instance = instance;
        _file.Clear();
        Suite = null;
    }

    internal bool Defiler(out object evenement) => _file.TryDequeue(out evenement!);

    internal virtual string? SerialiserEtat() => null;

    internal virtual void RestaurerEtat(string? json) { }
}

/// <summary>Processus portant un état typé, sérialisé en JSON dans l'instance.</summary>
public abstract class Processus<TEtat> : ProcessusBase where TEtat : new()
{
    protected TEtat Etat { get; private set; } = new();

    internal override string? SerialiserEtat() => JsonSerializer.Serialize(Etat);

    internal override void RestaurerEtat(string? json) =>
        Etat = string.IsNullOrEmpty(json) ? new() : JsonSerializer.Deserialize<TEtat>(json) ?? new();
}

internal abstract record Suite(string Description);

internal sealed record Suspension(string Etape, TimeSpan? Delai, DateTimeOffset? Echeance) : Suite($"attente '{Etape}'");

internal sealed record Appel(string Etape, Type Type, Func<ProcessusBase, Task> Demarrer) : Suite($"appel de '{Type.Name}' à l'étape '{Etape}'");

internal sealed record Fin(string? Resultat) : Suite("fin");
