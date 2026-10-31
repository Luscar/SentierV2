using Sentier.Definitions;

namespace Sentier.Execution.Executeurs;

/// <summary>Systémique : exécute sa commande.</summary>
internal sealed class ExecuteurSystemique(ExecuteurAppels appels) : ExecuteurNonSuspensif
{
    public override TypeNoeud Type => TypeNoeud.Systemique;

    public override async Task<ResultatNoeud> EntrerAsync(ContexteNoeud contexte, CancellationToken ct)
    {
        var noeud = (NoeudSystemique)contexte.Noeud;
        await appels.ExecuterCommandesAsync([noeud.Commande], contexte, MomentCommande.Execution, null, ct);
        return ResultatNoeud.Continuer();
    }
}
