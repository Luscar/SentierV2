using Sentier.Commandes;

namespace Exemples.DeclarationSousProcessus;

// Commandes : elles n'ont besoin que de la clé d'affaire, le numéro de la déclaration.

public sealed class IdentifierDeclaration(IServiceIdentification identification) : ICommandeSentier
{
    public Task ExecuterAsync(ContexteCommandeSentier contexte, CancellationToken ct)
        => identification.IdentifierAsync(contexte.CleAffaire, ct);
}
