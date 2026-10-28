using System.Text.Json;
using ProcessManager.Tests.Exemple;
using ProcessManager.Tests.Infrastructure;

namespace ProcessManager.Tests;

public sealed class Contexte
{
    public HorlogeTest Horloge { get; } = new();
    public StoreMemoire Store { get; } = new();
    public BusCommandesTest Commandes { get; } = new();
    public BusQueriesTest Queries { get; } = new();
    public FabriqueTest Fabrique { get; } = new();
    public MoteurProcessus Moteur { get; }

    public Contexte()
    {
        Fabrique
            .Enregistrer(() => new DeclarationOriginale(Commandes, Queries))
            .Enregistrer(() => new Signature(Commandes));
        Moteur = new MoteurProcessus(Store, Store, Fabrique, Horloge);
        IdentificationReussie(false);
    }

    public void IdentificationReussie(bool reussie) => Queries.Repondre = _ => new StatutDeclaration(reussie);

    public InstanceProcessus Instance(Guid id) => Store.Lire(id) ?? throw new InvalidOperationException($"Instance {id} absente.");

    public InstanceProcessus Enfant(Guid parentId) => Store.Commitees.Single(i => i.ParentId == parentId);

    public EtatDeclaration EtatDeclaration(Guid id) => JsonSerializer.Deserialize<EtatDeclaration>(Instance(id).Etat!)!;
}
