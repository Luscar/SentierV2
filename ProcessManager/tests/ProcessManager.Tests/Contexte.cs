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

    public bool IdentificationAcceptee { get; set; } = true;
    public bool ValidationAcceptee { get; set; } = true;
    public bool ComptabilisationAcceptee { get; set; } = true;
    public DateTimeOffset DateComptabilisation { get; set; }

    public Contexte()
    {
        DateComptabilisation = Horloge.Maintenant.AddDays(20);
        Fabrique
            .Enregistrer(() => new DeclarationOriginale(Commandes, Queries))
            .Enregistrer(() => new Signature(Commandes));
        Moteur = new MoteurProcessus(Store, Store, Fabrique, Horloge);
        Queries.Repondre = query => query switch
        {
            VerifierIdentification => new Verdict(IdentificationAcceptee),
            VerifierValidation => new Verdict(ValidationAcceptee),
            VerifierComptabilisation => new Verdict(ComptabilisationAcceptee),
            ObtenirDateComptabilisation => DateComptabilisation,
            _ => throw new InvalidOperationException($"Query inattendue : {query}"),
        };
    }

    public InstanceProcessus Instance(Guid id) => Store.Lire(id) ?? throw new InvalidOperationException($"Instance {id} absente.");

    public InstanceProcessus Enfant(Guid parentId) => Store.Commitees.Single(i => i.ParentId == parentId);

    public EtatDeclaration EtatDeclaration(Guid id) => JsonSerializer.Deserialize<EtatDeclaration>(Instance(id).Etat!)!;

    public int Envoyees<TCommande>() => Commandes.Envoyees.OfType<TCommande>().Count();
}
