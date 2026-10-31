using Sentier.Commandes;

namespace Exemples.DeclarationSousProcessus;

public sealed class EtablirCriteresSelection(IServiceVerification verification) : ICommandeSentier
{
    public Task ExecuterAsync(ContexteCommandeSentier contexte, CancellationToken ct)
        => verification.EtablirCriteresSelectionAsync(contexte.CleAffaire, ct);
}
