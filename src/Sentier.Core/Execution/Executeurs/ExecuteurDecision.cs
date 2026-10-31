using Sentier.Definitions;

namespace Sentier.Execution.Executeurs;

/// <summary>Décision : première branche dont la requête ou la condition sur variable est vraie, sinon la branche « sinon ».</summary>
internal sealed class ExecuteurDecision(ExecuteurAppels appels) : ExecuteurNonSuspensif
{
    public override TypeNoeud Type => TypeNoeud.Decision;

    public override async Task<ResultatNoeud> EntrerAsync(ContexteNoeud contexte, CancellationToken ct)
    {
        var sorties = contexte.Definition.Sorties(contexte.Noeud.Code);
        foreach (var transition in sorties.Where(t => t.Requete is not null || t.Condition is not null))
        {
            var vraie = transition.Condition is { } condition
                ? condition.Evaluer(contexte.Variables)
                : await appels.EvaluerAsync(transition.Requete!, contexte.Instance, contexte.Noeud.Code, contexte.Variables, ct);
            if (vraie)
            {
                return ResultatNoeud.Continuer(transition.NomSortie);
            }
        }

        var sinon = sorties.FirstOrDefault(t => t.EstSinon)
            ?? throw new AucuneTransitionException(contexte.Definition.CleProcessus, contexte.Noeud.Code, ConstructeurDecision.SortieSinon);
        return ResultatNoeud.Continuer(sinon.NomSortie);
    }
}
