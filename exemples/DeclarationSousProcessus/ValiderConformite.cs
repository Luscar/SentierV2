using Sentier.Commandes;

namespace Exemples.DeclarationSousProcessus;

public sealed class ValiderConformite(IServiceConformite conformite) : ICommandeSentier
{
    public Task ExecuterAsync(ContexteCommande contexte, CancellationToken ct)
        => conformite.ValiderAsync(contexte.CleAffaire, ct);
}
