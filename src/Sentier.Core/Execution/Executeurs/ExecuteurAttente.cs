using Sentier.Definitions;
using Sentier.Variables;

namespace Sentier.Execution.Executeurs;

/// <summary>Attente : commandes d'entrée, puis une ligne d'attente par déclencheur ; reprise sur le premier survenu.</summary>
internal sealed class ExecuteurAttente(ExecuteurAppels appels, PlanificateurAttentes planificateur) : IExecuteurNoeud
{
    public TypeNoeud Type => TypeNoeud.Attente;

    public async Task<ResultatNoeud> EntrerAsync(ContexteNoeud contexte, CancellationToken ct)
    {
        var noeud = (NoeudAttente)contexte.Noeud;
        await appels.ExecuterCommandesAsync(noeud.CommandesEntree, contexte, MomentCommande.Entree, null, ct);
        await planificateur.CreerAsync(contexte.Instance, noeud, contexte.Variables, noeud.Declencheurs, ct);
        return ResultatNoeud.Suspendre();
    }

    public Task<ResultatNoeud> ReprendreAsync(ContexteNoeud contexte, EvenementReprise evenement, CancellationToken ct)
    {
        if (evenement is not RepriseDeclencheur reprise)
        {
            throw new SentierException($"L'attente '{contexte.Noeud.Code}' ne peut être reprise que par une échéance ou un signal.");
        }

        var noeud = (NoeudAttente)contexte.Noeud;
        var nomSortie = reprise.Attente.NomSortie;
        contexte.Variables.EcrireSysteme(VariablesProcessus.VariableDeclencheur, nomSortie);

        var declencheur = noeud.Declencheurs.FirstOrDefault(d => d.NomSortie == nomSortie);
        if (declencheur is DeclencheurSignal { FusionnerChargeUtile: true })
        {
            contexte.Variables.Fusionner(reprise.ChargeUtile);
        }

        return Task.FromResult(ResultatNoeud.Continuer(nomSortie));
    }
}
