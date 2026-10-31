using Sentier.Definitions;

namespace Sentier.Execution.Executeurs;

/// <summary>Interactif : commandes d'entrée, création de la tâche ; à la complétion, fusion des données et commandes de sortie.</summary>
internal sealed class ExecuteurInteractif(ExecuteurAppels appels, IHorloge horloge) : IExecuteurNoeud
{
    public TypeNoeud Type => TypeNoeud.Interactif;

    public async Task<ResultatNoeud> EntrerAsync(ContexteNoeud contexte, CancellationToken ct)
    {
        var noeud = (NoeudInteractif)contexte.Noeud;
        var instance = contexte.Instance;
        await appels.ExecuterCommandesAsync(noeud.CommandesEntree, contexte, MomentCommande.Entree, null, ct);

        var maintenant = horloge.MaintenantUtc;
        var libelle = $"Nœud interactif '{noeud.Code}'";
        var assigne = noeud.Assigne is null
            ? null
            : ExecuteurAppels.ResoudreParametres([noeud.Assigne], instance, contexte.Variables, libelle).Values.FirstOrDefault() as string;
        var echeance = noeud.DureeEcheance is { } duree
            ? maintenant + duree
            : noeud.VariableEcheance is not null ? contexte.Variables.Lire(noeud.VariableEcheance) as DateTime? : null;

        var demande = new DemandeTacheSentier
        {
            IdInstance = instance.Id,
            CleProcessus = instance.CleProcessus,
            CleAffaire = instance.CleAffaire,
            CodeNoeud = noeud.Code,
            NomTache = noeud.NomTache,
            Role = noeud.Role,
            Assigne = assigne,
            DateEcheance = echeance,
            Donnees = ExecuteurAppels.ResoudreParametres(noeud.Donnees, instance, contexte.Variables, libelle),
        };

        if (instance.IdTacheCourante is not null)
        {
            // Nouvelle entrée dans le nœud (relance) : la tâche précédente ne doit plus reprendre l'instance.
            await contexte.Moteur.AnnulerTachesAsync(instance, $"Nouvelle entrée dans le nœud '{noeud.Code}'.", ct);
        }

        instance.IdTacheCourante = await contexte.Moteur.Taches.CreerTacheAsync(demande, ct);
        return ResultatNoeud.Suspendre();
    }

    public async Task<ResultatNoeud> ReprendreAsync(ContexteNoeud contexte, EvenementReprise evenement, CancellationToken ct)
    {
        if (evenement is not RepriseTache reprise)
        {
            throw new SentierException($"Le nœud interactif '{contexte.Noeud.Code}' ne peut être repris que par la complétion de sa tâche.");
        }

        var noeud = (NoeudInteractif)contexte.Noeud;
        contexte.Variables.Fusionner(reprise.Donnees);
        await appels.ExecuterCommandesAsync(noeud.CommandesSortie, contexte, MomentCommande.Sortie, reprise.Donnees ?? new Dictionary<string, object?>(), ct);

        var utilisateur = reprise.Utilisateur ?? contexte.Utilisateur;
        await contexte.Moteur.Taches.CompleterTacheAsync(reprise.Tache.Id, reprise.Donnees, utilisateur, ct);
        await contexte.Moteur.AnnulerTachesOrphelinesAsync(contexte.Instance, reprise.Tache.Id, ct);
        contexte.Instance.IdTacheCourante = null;

        return ResultatNoeud.Continuer(resultat: utilisateur is null ? "tâche complétée" : $"tâche complétée par {utilisateur}");
    }
}
