using Sentier.Commandes;

namespace Exemples.DeclarationSousProcessus;

public sealed class Comptabiliser(IServiceComptable comptable) : ICommandeSentier
{
    public Task ExecuterAsync(ContexteCommandeSentier contexte, CancellationToken ct)
        => comptable.ComptabiliserAsync(contexte.CleAffaire, ct);
}
