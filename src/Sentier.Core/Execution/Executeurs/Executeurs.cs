using Sentier.Definitions;
using Sentier.Persistance;
using Sentier.Variables;

namespace Sentier.Execution.Executeurs;

/// <summary>Classe de base des exécuteurs de nœuds non suspensifs.</summary>
internal abstract class ExecuteurNonSuspensif : IExecuteurNoeud
{
    public abstract TypeNoeud Type { get; }

    public abstract Task<ResultatNoeud> EntrerAsync(ContexteNoeud contexte, CancellationToken ct);

    public Task<ResultatNoeud> ReprendreAsync(ContexteNoeud contexte, EvenementReprise evenement, CancellationToken ct)
        => throw new SentierException($"Le nœud '{contexte.Noeud.Code}' ({Type}) ne peut pas être repris : il n'est pas suspensif.");
}

/// <summary>Début : continue immédiatement.</summary>
internal sealed class ExecuteurDebut : ExecuteurNonSuspensif
{
    public override TypeNoeud Type => TypeNoeud.Debut;

    public override Task<ResultatNoeud> EntrerAsync(ContexteNoeud contexte, CancellationToken ct)
        => Task.FromResult(ResultatNoeud.Continuer());
}

/// <summary>Fin : termine l'instance avec le code du nœud.</summary>
internal sealed class ExecuteurFin : ExecuteurNonSuspensif
{
    public override TypeNoeud Type => TypeNoeud.Fin;

    public override Task<ResultatNoeud> EntrerAsync(ContexteNoeud contexte, CancellationToken ct)
        => Task.FromResult(ResultatNoeud.Terminer(contexte.Noeud.Code));
}

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

/// <summary>Décision : première branche dont la requête est vraie, sinon la branche « sinon ».</summary>
internal sealed class ExecuteurDecision(ExecuteurAppels appels) : ExecuteurNonSuspensif
{
    public override TypeNoeud Type => TypeNoeud.Decision;

    public override async Task<ResultatNoeud> EntrerAsync(ContexteNoeud contexte, CancellationToken ct)
    {
        var sorties = contexte.Definition.Sorties(contexte.Noeud.Code);
        foreach (var transition in sorties.Where(t => t.Requete is not null))
        {
            if (await appels.EvaluerAsync(transition.Requete!, contexte.Instance, contexte.Noeud.Code, contexte.Variables, ct))
            {
                return ResultatNoeud.Continuer(transition.NomSortie);
            }
        }

        var sinon = sorties.FirstOrDefault(t => t.EstSinon)
            ?? throw new AucuneTransitionException(contexte.Definition.CleProcessus, contexte.Noeud.Code, ConstructeurDecision.SortieSinon);
        return ResultatNoeud.Continuer(sinon.NomSortie);
    }
}

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

        var demande = new DemandeTacheUtilisateur
        {
            IdInstance = instance.Id,
            CleProcessus = instance.CleProcessus,
            CleAffaire = instance.CleAffaire,
            CodeNoeud = noeud.Code,
            Nom = noeud.NomTache,
            Role = noeud.Role,
            Utilisateur = assigne,
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
        await contexte.Moteur.Taches.CompleterTacheAsync(reprise.Tache.Id, utilisateur, ct);
        await contexte.Moteur.AnnulerTachesOrphelinesAsync(contexte.Instance, reprise.Tache.Id, ct);
        contexte.Instance.IdTacheCourante = null;

        return ResultatNoeud.Continuer(resultat: utilisateur is null ? "tâche complétée" : $"tâche complétée par {utilisateur}");
    }
}

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
