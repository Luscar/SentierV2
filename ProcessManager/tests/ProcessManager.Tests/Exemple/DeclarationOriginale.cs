using ProcessManager;

namespace ProcessManager.Tests.Exemple;

public sealed class EtatDeclaration
{
    public Guid AggregateId { get; set; }
    public int Relances { get; set; }
    public string? Resultat { get; set; }
}

// Événements internes (décisions).
public sealed record Identifiee;
public sealed record RejetIdentification;
public sealed record Validee;
public sealed record RejetValidation;
public sealed record Comptabilisee;
public sealed record RejetComptabilisation;

// Signaux des tâches utilisateur.
public sealed record CorrectionSoumise;
public sealed record RejetAutorise;
public sealed record RejetRefuse;

/// <summary>
/// Identification (rejet → CorrectionIdentification) → Validation (rejet → CorrectionValidation)
/// → AttenteComptabilisation (date fixe) → Comptabilisation (rejet → AutorisationRejet).
/// </summary>
public sealed class DeclarationOriginale(IBusCommandes commandes, IBusQueries queries) : Processus<EtatDeclaration>
{
    public const string EtapeCorrectionIdentification = "CorrectionIdentification";
    public const string EtapeCorrectionValidation = "CorrectionValidation";
    public const string EtapeAttenteComptabilisation = "AttenteComptabilisation";
    public const string EtapeAutorisationRejet = "AutorisationRejet";
    public static readonly TimeSpan DelaiTache = TimeSpan.FromDays(5);
    public const int RelancesMax = 2;

    public Task Demarrer(Guid aggregateId)
    {
        Etat.AggregateId = aggregateId;
        Correler(aggregateId.ToString());
        return Identifier();
    }

    // Identification.
    private async Task Identifier()
    {
        await commandes.Envoyer(new IdentifierDeclaration(Etat.AggregateId));
        var verdict = await queries.Demander<Verdict>(new VerifierIdentification(Etat.AggregateId));
        Raise(verdict.Accepte ? new Identifiee() : new RejetIdentification());
    }

    private void Handle(RejetIdentification _) => OuvrirTache(EtapeCorrectionIdentification);

    private Task Handle(Identifiee _) => Valider();

    // Validation.
    private async Task Valider()
    {
        await commandes.Envoyer(new ValiderDeclaration(Etat.AggregateId));
        var verdict = await queries.Demander<Verdict>(new VerifierValidation(Etat.AggregateId));
        Raise(verdict.Accepte ? new Validee() : new RejetValidation());
    }

    private void Handle(RejetValidation _) => OuvrirTache(EtapeCorrectionValidation);

    // Attente de comptabilisation, jusqu'à une date fixe.
    private async Task Handle(Validee _)
    {
        var date = await queries.Demander<DateTimeOffset>(new ObtenirDateComptabilisation(Etat.AggregateId));
        Attendre(EtapeAttenteComptabilisation, date);
    }

    // Comptabilisation.
    private async Task Comptabiliser()
    {
        await commandes.Envoyer(new ComptabiliserDeclaration(Etat.AggregateId));
        var verdict = await queries.Demander<Verdict>(new VerifierComptabilisation(Etat.AggregateId));
        Raise(verdict.Accepte ? new Comptabilisee() : new RejetComptabilisation());
    }

    private void Handle(Comptabilisee _) => Finir("comptabilisée");

    private async Task Handle(RejetComptabilisation _)
    {
        await commandes.Envoyer(new DemanderAutorisationRejet(Etat.AggregateId));
        OuvrirTache(EtapeAutorisationRejet);
    }

    // Signaux : le même signal de correction reprend l'étape rejetée.
    private Task Handle(CorrectionSoumise _) => Etape switch
    {
        EtapeCorrectionIdentification => Identifier(),
        EtapeCorrectionValidation => Valider(),
        _ => throw new InvalidOperationException($"Correction soumise à l'étape '{Etape}'."),
    };

    private void Handle(RejetAutorise _) => Finir("rejetée");

    private Task Handle(RejetRefuse _) => Comptabiliser();

    // Délais : la fin de l'attente de comptabilisation lance la comptabilisation, sinon relance de la tâche.
    private async Task Handle(DelaiExpire e)
    {
        if (e.Etape == EtapeAttenteComptabilisation)
        {
            await Comptabiliser();
            return;
        }

        Etat.Relances++;
        if (Etat.Relances > RelancesMax)
        {
            Finir("expirée");
            return;
        }
        await commandes.Envoyer(new Relancer(Etat.AggregateId, e.Etape, Etat.Relances));
        TacheUser(e.Etape, DelaiTache);
    }

    private void Handle(AttenteAbandonnee _) => Finir("abandonnée");

    // Les relances repartent de zéro à chaque nouvelle tâche.
    private void OuvrirTache(string etape)
    {
        if (Etape != etape)
            Etat.Relances = 0;
        TacheUser(etape, DelaiTache);
    }

    // Le résultat est aussi gardé dans l'état pour être observable.
    private void Finir(string resultat)
    {
        Etat.Resultat = resultat;
        Terminer(resultat);
    }
}
