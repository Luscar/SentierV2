using ProcessManager;

namespace ProcessManager.Tests.Exemple;

/// <summary>Sous-processus réutilisable seul : demande une signature et attend 10 jours.</summary>
public sealed class Signature(IBusCommandes commandes) : ProcessusBase
{
    public const string EtapeSignature = "Signature";

    public async Task Demarrer(Guid aggregateId)
    {
        Correler(aggregateId.ToString());
        await commandes.Envoyer(new DemanderSignature(aggregateId));
        TacheUser(EtapeSignature, TimeSpan.FromDays(10));
    }

    private void Handle(SignatureRecue _) => Terminer("signée");

    private void Handle(DelaiExpire _) => Terminer("non-signée");

    private void Handle(AttenteAbandonnee _) => Terminer("abandonnée");
}
