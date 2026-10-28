using ProcessManager;

namespace ProcessManager.Tests.Exemple;

public sealed class EtatInscription
{
    public Guid DossierId { get; set; }
    public int Refus { get; set; }
    public string? Resultat { get; set; }
}

// Un signal par tâche utilisateur.
public sealed record DossierSoumis;
public sealed record DemandeApprouvee;
public sealed record DemandeRefusee;

/// <summary>
/// Plusieurs tâches utilisateur et attentes dans le même processus :
/// Saisie (tâche user) → Carence (attente pure) → Approbation (tâche user), avec retour en Saisie sur refus.
/// DelaiExpire et AttenteAbandonnee sont aiguillés selon l'étape qu'ils portent.
/// </summary>
public sealed class Inscription(IBusCommandes commandes) : Processus<EtatInscription>
{
    public const string EtapeSaisie = "Saisie";
    public const string EtapeCarence = "Carence";
    public const string EtapeApprobation = "Approbation";

    public Task Demarrer(Guid dossierId)
    {
        Etat.DossierId = dossierId;
        Correler(dossierId.ToString());
        TacheUser(EtapeSaisie, TimeSpan.FromDays(5));
        return Task.CompletedTask;
    }

    private void Handle(DossierSoumis _) => Attendre(EtapeCarence, TimeSpan.FromDays(2));

    private void Handle(DemandeApprouvee _) => Finir("approuvée");

    private void Handle(DemandeRefusee _)
    {
        Etat.Refus++;
        TacheUser(EtapeSaisie, TimeSpan.FromDays(5));
    }

    private async Task Handle(DelaiExpire e)
    {
        switch (e.Etape)
        {
            case EtapeSaisie:
                Finir("saisie expirée");
                break;
            case EtapeCarence:
                await commandes.Envoyer(new NotifierApprobateur(Etat.DossierId));
                TacheUser(EtapeApprobation, TimeSpan.FromDays(3));
                break;
            case EtapeApprobation:
                Finir("approbation expirée");
                break;
        }
    }

    private void Handle(AttenteAbandonnee e) => Finir($"abandonnée ({e.Etape})");

    private void Finir(string resultat)
    {
        Etat.Resultat = resultat;
        Terminer(resultat);
    }
}
