using Sentier.Commandes;

namespace Exemples.DeclarationSousProcessus;

// Requêtes : lecture seule de l'état de la déclaration.

public abstract class RequeteEtatDeclaration(IDossiersDeclarations dossiers) : IRequeteSentier
{
    public async Task<bool> EvaluerAsync(ContexteRequeteSentier contexte, CancellationToken ct)
        => Evaluer(await dossiers.ObtenirEtatAsync(contexte.CleAffaire, ct));

    protected abstract bool Evaluer(EtatDeclaration etat);
}
