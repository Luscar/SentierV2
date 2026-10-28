using ProcessManager;

namespace ProcessManager.Tests.Exemple;

public sealed class EtatDeclaration
{
    public Guid AggregateId { get; set; }
    public int Relances { get; set; }
    public string? Resultat { get; set; }
}

public sealed record Identifiee;
public sealed record RejetIdentification;
public sealed record CorrectionSoumise;

public sealed class DeclarationOriginale(IBusCommandes commandes, IBusQueries queries) : Processus<EtatDeclaration>
{
    public const string EtapeCorrection = "CorrectionIdentification";
    public const string EtapeSignature = "Signature";
    public static readonly TimeSpan DelaiCorrection = TimeSpan.FromDays(5);

    // Systémique + décision.
    public async Task Demarrer(Guid aggregateId)
    {
        Etat.AggregateId = aggregateId;
        Correler(aggregateId.ToString());
        await commandes.Envoyer(new IdentifierDeclaration(aggregateId));
        var statut = await queries.Demander<StatutDeclaration>(new ObtenirStatutDeclaration(aggregateId));
        Raise(statut.Identifiee ? new Identifiee() : new RejetIdentification());
    }

    // Systémique puis sous-processus.
    private async Task Handle(Identifiee _)
    {
        await commandes.Envoyer(new ValiderDeclaration(Etat.AggregateId));
        Appeler<Signature>(EtapeSignature, s => s.Demarrer(Etat.AggregateId));
    }

    // Tâche utilisateur.
    private void Handle(RejetIdentification _) => TacheUser(EtapeCorrection, DelaiCorrection);

    // Signal.
    private Task Handle(CorrectionSoumise _) => Demarrer(Etat.AggregateId);

    // Fin de délai.
    private async Task Handle(DelaiExpire _)
    {
        Etat.Relances++;
        if (Etat.Relances > 2)
        {
            Finir("expirée");
            return;
        }
        await commandes.Envoyer(new RelancerDeclarant(Etat.AggregateId, Etat.Relances));
        TacheUser(EtapeCorrection, DelaiCorrection);
    }

    // Abandon.
    private void Handle(AttenteAbandonnee _) => Finir("abandonnée");

    private void Handle(SousProcessusTermine termine) => Finir(termine.Resultat);

    // Le résultat est aussi gardé dans l'état pour être observable.
    private void Finir(string? resultat)
    {
        Etat.Resultat = resultat;
        Terminer(resultat);
    }
}
