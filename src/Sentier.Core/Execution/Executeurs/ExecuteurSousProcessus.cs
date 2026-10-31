using Sentier.Definitions;
using Sentier.Variables;

namespace Sentier.Execution.Executeurs;

/// <summary>
/// Sous-processus : démarre l'enfant dans la même transaction. Si l'enfant se termine immédiatement,
/// le parent continue ; sinon il se suspend jusqu'à la fin de l'enfant.
/// </summary>
internal sealed class ExecuteurSousProcessus : IExecuteurNoeud
{
    public TypeNoeud Type => TypeNoeud.SousProcessus;

    public async Task<ResultatNoeud> EntrerAsync(ContexteNoeud contexte, CancellationToken ct)
    {
        var noeud = (NoeudSousProcessus)contexte.Noeud;
        var parent = contexte.Instance;
        var moteur = contexte.Moteur;

        var definitionEnfant = await moteur.Definitions.ObtenirActiveAsync(noeud.CleProcessus, ct)
            ?? throw new ProcessusIntrouvableException(noeud.CleProcessus);
        var erreurs = ValidateurSousProcessus.Valider(contexte.Definition, noeud, definitionEnfant);
        if (erreurs.Count > 0)
        {
            throw new DefinitionInvalideException(contexte.Definition.CleProcessus, erreurs);
        }

        var cleAffaire = noeud.VariableCleAffaire is null
            ? parent.CleAffaire
            : contexte.Variables.Lire(noeud.VariableCleAffaire) as long?
              ?? throw new VariableInvalideException($"Le sous-processus '{noeud.Code}' lit sa clé d'affaire dans la variable '{noeud.VariableCleAffaire}', qui n'a pas de valeur.");

        if (definitionEnfant.InstanceUnique
            && (await moteur.InstancesActivesAsync(definitionEnfant.CleProcessus, cleAffaire, ct)).Count > 0)
        {
            throw new SentierException(
                $"Le sous-processus '{noeud.Code}' ne peut pas démarrer : une instance active du processus '{definitionEnfant.CleProcessus}' existe déjà pour la clé d'affaire {cleAffaire}.");
        }

        var entrees = ExecuteurAppels.ResoudreParametres(noeud.Entrees, parent, contexte.Variables, $"Mappage d'entrée du sous-processus '{noeud.Code}'")
            .Where(e => e.Value is not null)
            .ToDictionary(e => e.Key, e => e.Value);
        var variablesEnfant = VariablesProcessus.Initialiser(definitionEnfant.Declarations, entrees);

        var enfant = await moteur.DemarrerAsync(definitionEnfant, cleAffaire, variablesEnfant, parent, noeud.Code, contexte.Utilisateur, ct);
        return enfant.Instance.Statut switch
        {
            StatutInstance.Terminee or StatutInstance.Annulee => Conclure(contexte, enfant.Instance, enfant.Variables),
            _ => ResultatNoeud.Suspendre(),
        };
    }

    public Task<ResultatNoeud> ReprendreAsync(ContexteNoeud contexte, EvenementReprise evenement, CancellationToken ct)
    {
        if (evenement is not RepriseEnfant reprise)
        {
            throw new SentierException($"Le sous-processus '{contexte.Noeud.Code}' ne peut être repris que par la fin de son enfant.");
        }

        return Task.FromResult(Conclure(contexte, reprise.Enfant, reprise.VariablesEnfant));
    }

    private static ResultatNoeud Conclure(ContexteNoeud contexte, InstanceProcessus enfant, IVariablesLecture? variablesEnfant)
    {
        var noeud = (NoeudSousProcessus)contexte.Noeud;
        if (enfant.Statut == StatutInstance.Annulee)
        {
            var brancheAnnule = contexte.Definition.Sorties(noeud.Code).Any(t => t.NomSortie == NoeudSousProcessus.SortieAnnule);
            return brancheAnnule
                ? ResultatNoeud.Continuer(NoeudSousProcessus.SortieAnnule, $"enfant {enfant.Id} annulé")
                : ResultatNoeud.Annuler($"Le sous-processus {enfant.Id} ({enfant.CleProcessus}) a été annulé.");
        }

        if (variablesEnfant is not null)
        {
            foreach (var sortie in noeud.Sorties)
            {
                contexte.Variables.Ecrire(sortie.VariableParent, variablesEnfant.Lire(sortie.VariableEnfant));
            }
        }

        contexte.Variables.EcrireSysteme(VariablesProcessus.VariableFinEnfant, enfant.CodeFin);
        return ResultatNoeud.Continuer(enfant.CodeFin, $"fin de l'enfant {enfant.Id} : {enfant.CodeFin}");
    }
}
