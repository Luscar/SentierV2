using Sentier.Commandes;

namespace Exemples.DeclarationSousProcessus;

public sealed class Comptabiliser(IServiceComptable comptable) : ICommandeSentier
{
    public Task ExecuterAsync(ContexteCommande contexte, CancellationToken ct)
        => comptable.ComptabiliserAsync(contexte.CleAffaire, ct);
}
