using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Sentier.Definitions;
using Sentier.Persistance;
using Sentier.Variables;

namespace Sentier.Execution;

/// <summary>
/// Moteur d'exécution synchrone : enchaîne les nœuds jusqu'à un nœud suspensif ou une fin,
/// dans la transaction du client. Enregistré en Scoped.
/// </summary>
internal sealed class MoteurSentier
{
    private const int LongueurMaxResultat = 400;

    private readonly IServiceProvider _services;
    private readonly IDepotInstances _instances;
    private readonly IDepotHistorique _historique;
    private readonly IDepotAttentes _attentes;
    private readonly ISerialiseurVariables _serialiseur;
    private readonly OptionsSentier _options;
    private readonly ILogger _logger;
    private readonly Dictionary<TypeNoeud, IExecuteurNoeud> _executeurs;

    /// <summary>Instances en cours d'exécution dans cette portée : un enfant ne les reprend pas lui-même.</summary>
    private readonly HashSet<IdInstance> _enExecution = [];

    public MoteurSentier(
        IServiceProvider services,
        FournisseurDefinitions definitions,
        IDepotInstances instances,
        IDepotHistorique historique,
        IDepotAttentes attentes,
        IEnumerable<IExecuteurNoeud> executeurs,
        ISerialiseurVariables serialiseur,
        IHorloge horloge,
        OptionsSentier options)
    {
        _services = services;
        Definitions = definitions;
        _instances = instances;
        _historique = historique;
        _attentes = attentes;
        _serialiseur = serialiseur;
        Horloge = horloge;
        _options = options;
        _logger = services.GetService<ILogger<MoteurSentier>>() ?? (ILogger)NullLogger.Instance;
        _executeurs = new Dictionary<TypeNoeud, IExecuteurNoeud>();
        foreach (var executeur in executeurs)
        {
            _executeurs[executeur.Type] = executeur;
        }
    }

    internal FournisseurDefinitions Definitions { get; }

    internal IHorloge Horloge { get; }

    internal string? UtilisateurCourant => _services.GetService<IContexteUtilisateur>()?.Utilisateur;

    /// <summary>Gestionnaire de tâches du client ; lève une erreur s'il n'est pas enregistré.</summary>
    internal IGestionnaireTaches Taches => TachesOptionnelles
        ?? throw new SentierException("Aucun IGestionnaireTaches n'est enregistré : un nœud interactif ne peut pas gérer de tâche.");

    /// <summary>Gestionnaire de tâches du client, ou <c>null</c> : sans gestionnaire, aucune tâche n'a pu être créée.</summary>
    internal IGestionnaireTaches? TachesOptionnelles => _services.GetService<IGestionnaireTaches>();

    internal VariablesProcessus ChargerVariables(InstanceProcessus instance, DefinitionProcessus definition)
        => VariablesProcessus.Charger(definition.Declarations, _serialiseur, instance.VariablesJson);

    internal string SerialiserVariables(VariablesProcessus variables) => _serialiseur.Serialiser(variables.Valeurs);

    /// <summary>Crée une instance (enfant si <paramref name="parent"/> est fourni) et l'exécute depuis son début.</summary>
    internal async Task<ExecutionInstance> DemarrerAsync(
        DefinitionProcessus definition,
        long cleAffaire,
        VariablesProcessus variables,
        InstanceProcessus? parent,
        string? codeNoeudParent,
        string? utilisateur,
        CancellationToken ct)
    {
        var maintenant = Horloge.MaintenantUtc;
        var instance = new InstanceProcessus
        {
            IdDefinition = definition.Id ?? throw new SentierException($"La définition '{definition.CleProcessus}' n'est pas déployée."),
            CleProcessus = definition.CleProcessus,
            IdInstanceParent = parent?.Id,
            CodeNoeudParent = codeNoeudParent,
            CleAffaire = cleAffaire,
            InstanceUnique = definition.InstanceUnique,
            Statut = StatutInstance.EnCours,
            CodeNoeudCourant = definition.NoeudDebut.Code,
            VariablesJson = SerialiserVariables(variables),
            Version = 1,
            DateCreation = maintenant,
            DateMaj = maintenant,
        };

        await _instances.InsererAsync(instance, ct);
        _logger.LogInformation(
            "Sentier : démarrage de l'instance {IdInstance} du processus {CleProcessus} v{Version} (clé d'affaire {CleAffaire}).",
            instance.Id, definition.CleProcessus, definition.Version, cleAffaire);

        var execution = new ExecutionInstance(instance, definition, variables, utilisateur);
        await DeroulerAsync(execution, null, ct);
        return execution;
    }

    /// <summary>Reprend une instance au repos sur son nœud courant, à la suite d'un événement.</summary>
    internal async Task<ExecutionInstance> ReprendreAsync(InstanceProcessus instance, EvenementReprise evenement, string? utilisateur, CancellationToken ct)
    {
        var execution = await PreparerAsync(instance, utilisateur, ct);
        _logger.LogInformation(
            "Sentier : reprise de l'instance {IdInstance} ({CleProcessus}) sur le nœud {CodeNoeud} ({Evenement}).",
            instance.Id, instance.CleProcessus, instance.CodeNoeudCourant, evenement.GetType().Name);

        _enExecution.Add(instance.Id);
        ResultatNoeud resultat;
        try
        {
            var noeud = execution.Definition.Noeud(instance.CodeNoeudCourant);
            resultat = await Executeur(noeud).ReprendreAsync(Contexte(execution, noeud), evenement, ct);
        }
        finally
        {
            _enExecution.Remove(instance.Id);
        }

        await DeroulerAsync(execution, resultat, ct);
        return execution;
    }

    /// <summary>Exécute l'instance en entrant de nouveau dans son nœud courant (relance, suite de migration).</summary>
    internal async Task<ExecutionInstance> ExecuterNoeudCourantAsync(InstanceProcessus instance, string? utilisateur, CancellationToken ct)
    {
        var execution = await PreparerAsync(instance, utilisateur, ct);
        _logger.LogInformation(
            "Sentier : relance de l'instance {IdInstance} ({CleProcessus}) depuis le nœud {CodeNoeud}.",
            instance.Id, instance.CleProcessus, instance.CodeNoeudCourant);
        await DeroulerAsync(execution, null, ct);
        return execution;
    }

    /// <summary>
    /// Annule, par le gestionnaire du client, la tâche courante et toute autre tâche ouverte de l'instance
    /// (tâche orpheline d'une transaction annulée), puis oublie la tâche courante. L'instance n'est pas persistée ici.
    /// </summary>
    internal async Task AnnulerTachesAsync(InstanceProcessus instance, string motif, CancellationToken ct)
    {
        if (TachesOptionnelles is not { } taches)
        {
            instance.IdTacheCourante = null;
            return;
        }

        var aAnnuler = (await taches.ObtenirTachesOuvertesAsync(instance.Id, ct)).Select(t => t.Id).ToList();
        if (instance.IdTacheCourante is { } courante && !aAnnuler.Contains(courante))
        {
            aAnnuler.Insert(0, courante);
        }

        foreach (var idTache in aAnnuler)
        {
            await taches.AnnulerTacheAsync(idTache, motif, ct);
        }

        instance.IdTacheCourante = null;
    }

    /// <summary>Annule les tâches encore ouvertes d'une instance autres que <paramref name="idTacheConservee"/> (tâches orphelines).</summary>
    internal async Task AnnulerTachesOrphelinesAsync(InstanceProcessus instance, long idTacheConservee, CancellationToken ct)
    {
        var taches = Taches;
        foreach (var tache in await taches.ObtenirTachesOuvertesAsync(instance.Id, ct))
        {
            if (tache.Id != idTacheConservee)
            {
                await taches.AnnulerTacheAsync(tache.Id, "Tâche orpheline : l'instance attendait une autre tâche.", ct);
            }
        }
    }

    /// <summary>
    /// Annule une instance : tâches ouvertes annulées par le gestionnaire du client, attentes supprimées,
    /// sous-processus actifs annulés en cascade.
    /// </summary>
    internal async Task AnnulerAsync(InstanceProcessus instance, string motif, bool notifierParent, CancellationToken ct)
    {
        var maintenant = Horloge.MaintenantUtc;
        await AnnulerTachesAsync(instance, motif, ct);

        await _attentes.SupprimerParInstanceAsync(instance.Id, ct);

        foreach (var enfant in await _instances.ObtenirEnfantsActifsAsync(instance.Id, ct))
        {
            await AnnulerAsync(enfant, $"Annulation du parent {instance.Id} : {motif}", notifierParent: false, ct);
        }

        await _historique.FermerPassagesOuvertsAsync(instance.Id, maintenant, Tronquer($"ANNULEE : {motif}"), ct);
        instance.Statut = StatutInstance.Annulee;
        instance.DateFin = maintenant;
        instance.DateMaj = maintenant;
        await _instances.MettreAJourAsync(instance, ct);
        _logger.LogInformation("Sentier : instance {IdInstance} ({CleProcessus}) annulée.", instance.Id, instance.CleProcessus);

        if (notifierParent)
        {
            await NotifierParentAsync(instance, ct);
        }
    }

    /// <summary>Marque une instance en erreur (appelé dans une transaction séparée).</summary>
    internal async Task MarquerEnErreurAsync(InstanceProcessus instance, string message, CancellationToken ct)
    {
        var maintenant = Horloge.MaintenantUtc;
        instance.Statut = StatutInstance.EnErreur;
        instance.MessageErreur = message.Length > 4000 ? message[..4000] : message;
        instance.DateErreur = maintenant;
        instance.DateMaj = maintenant;
        await _instances.MettreAJourAsync(instance, ct);
        _logger.LogWarning("Sentier : instance {IdInstance} ({CleProcessus}) marquée en erreur sur le nœud {CodeNoeud}.", instance.Id, instance.CleProcessus, instance.CodeNoeudCourant);
    }

    /// <summary>Remet au repos (suspendue) une instance en erreur qui attend encore un déclencheur, une tâche ou un enfant.</summary>
    internal async Task RemettreAuReposAsync(InstanceProcessus instance, CancellationToken ct)
    {
        instance.Statut = StatutInstance.Suspendue;
        instance.MessageErreur = null;
        instance.DateErreur = null;
        instance.DateMaj = Horloge.MaintenantUtc;
        await _instances.MettreAJourAsync(instance, ct);
        _logger.LogInformation("Sentier : instance {IdInstance} ({CleProcessus}) relancée, de nouveau suspendue sur le nœud {CodeNoeud}.", instance.Id, instance.CleProcessus, instance.CodeNoeudCourant);
    }

    internal Task<IReadOnlyList<InstanceProcessus>> InstancesActivesAsync(string cleProcessus, long cleAffaire, CancellationToken ct)
        => _instances.ObtenirActivesAsync(cleProcessus, cleAffaire, ct);

    internal ContexteNoeud Contexte(ExecutionInstance execution, Noeud noeud)
        => new(execution.Instance, execution.Definition, noeud, execution.Variables, execution.Utilisateur, this);

    private async Task<ExecutionInstance> PreparerAsync(InstanceProcessus instance, string? utilisateur, CancellationToken ct)
    {
        var definition = await Definitions.ObtenirAsync(instance.IdDefinition, ct);
        var variables = ChargerVariables(instance, definition);
        instance.Statut = StatutInstance.EnCours;
        instance.MessageErreur = null;
        instance.DateErreur = null;
        return new ExecutionInstance(instance, definition, variables, utilisateur);
    }

    /// <summary>
    /// Boucle d'exécution : tant que le nœud courant n'est ni suspensif ni final, exécuter le nœud,
    /// choisir la transition sortante et enregistrer le passage ; puis persister l'état.
    /// </summary>
    /// <param name="execution">Instance en cours d'exécution.</param>
    /// <param name="resultatReprise">Résultat de la reprise du nœud courant, ou <c>null</c> pour y entrer.</param>
    /// <param name="ct">Jeton d'annulation.</param>
    private async Task DeroulerAsync(ExecutionInstance execution, ResultatNoeud? resultatReprise, CancellationToken ct)
    {
        var instance = execution.Instance;
        var definition = execution.Definition;
        var etapes = 0;
        var resultat = resultatReprise;

        _enExecution.Add(instance.Id);
        try
        {
            if (resultat is not null)
            {
                await _historique.FermerPassagesOuvertsAsync(instance.Id, Horloge.MaintenantUtc, Decrire(resultat), ct);
            }

            while (true)
            {
                if (resultat is null)
                {
                    if (++etapes > _options.MaxEtapesParExecution)
                    {
                        throw new BoucleInfinieException(instance.Id, _options.MaxEtapesParExecution);
                    }

                    var noeud = definition.Noeud(instance.CodeNoeudCourant);
                    var dateEntree = Horloge.MaintenantUtc;
                    resultat = await Executeur(noeud).EntrerAsync(Contexte(execution, noeud), ct);
                    var suspendu = resultat is ResultatSuspendre;
                    await _historique.InsererAsync(new PassageNoeud
                    {
                        IdInstance = instance.Id,
                        CodeNoeud = noeud.Code,
                        TypeNoeud = noeud.Type,
                        DateEntree = dateEntree,
                        DateSortie = suspendu ? null : Horloge.MaintenantUtc,
                        Resultat = suspendu ? null : Decrire(resultat),
                    }, ct);
                }

                if (resultat is ResultatContinuer continuer)
                {
                    var transition = definition.Sortie(instance.CodeNoeudCourant, continuer.NomSortie)
                        ?? throw new AucuneTransitionException(definition.CleProcessus, instance.CodeNoeudCourant, continuer.NomSortie);
                    instance.CodeNoeudCourant = transition.Destination;
                    resultat = null;
                    continue;
                }

                break;
            }

            switch (resultat)
            {
                case ResultatSuspendre:
                    instance.Statut = StatutInstance.Suspendue;
                    await PersisterAsync(execution, ct);
                    _logger.LogDebug("Sentier : instance {IdInstance} suspendue sur le nœud {CodeNoeud}.", instance.Id, instance.CodeNoeudCourant);
                    return;

                case ResultatTerminer terminer:
                    var maintenant = Horloge.MaintenantUtc;
                    instance.Statut = StatutInstance.Terminee;
                    instance.CodeFin = terminer.CodeFin;
                    instance.DateFin = maintenant;
                    await PersisterAsync(execution, ct);
                    _logger.LogInformation(
                        "Sentier : instance {IdInstance} ({CleProcessus}) terminée sur la fin {CodeFin}.",
                        instance.Id, instance.CleProcessus, terminer.CodeFin);
                    break;

                case ResultatAnnuler annuler:
                    instance.VariablesJson = SerialiserVariables(execution.Variables);
                    await AnnulerAsync(instance, annuler.Motif, notifierParent: false, ct);
                    break;
            }
        }
        finally
        {
            _enExecution.Remove(instance.Id);
        }

        await NotifierParentAsync(instance, ct);
    }

    /// <summary>Reprend le parent d'une instance terminée ou annulée, s'il l'attend sur son nœud sous-processus.</summary>
    private async Task NotifierParentAsync(InstanceProcessus enfant, CancellationToken ct)
    {
        if (enfant.IdInstanceParent is not { } idParent || _enExecution.Contains(idParent))
        {
            return;
        }

        var parent = await _instances.ObtenirAsync(idParent, ct);
        if (parent is null || !parent.EstAuRepos || parent.CodeNoeudCourant != enfant.CodeNoeudParent)
        {
            return;
        }

        IVariablesLecture? variablesEnfant = null;
        if (enfant.Statut == StatutInstance.Terminee)
        {
            var definitionEnfant = await Definitions.ObtenirAsync(enfant.IdDefinition, ct);
            variablesEnfant = ChargerVariables(enfant, definitionEnfant);
        }

        await ReprendreAsync(parent, new RepriseEnfant(enfant, variablesEnfant), utilisateur: null, ct);
    }

    private async Task PersisterAsync(ExecutionInstance execution, CancellationToken ct)
    {
        var instance = execution.Instance;
        instance.VariablesJson = SerialiserVariables(execution.Variables);
        instance.DateMaj = Horloge.MaintenantUtc;
        await _instances.MettreAJourAsync(instance, ct);
    }

    private IExecuteurNoeud Executeur(Noeud noeud)
        => _executeurs.TryGetValue(noeud.Type, out var executeur)
            ? executeur
            : throw new SentierException($"Aucun exécuteur n'est enregistré pour les nœuds de type {noeud.Type}.");

    private static string? Decrire(ResultatNoeud resultat) => resultat switch
    {
        ResultatContinuer c => Tronquer(c.Resultat ?? c.NomSortie),
        ResultatTerminer t => t.CodeFin,
        ResultatAnnuler a => Tronquer($"ANNULEE : {a.Motif}"),
        _ => null,
    };

    private static string? Tronquer(string? texte)
        => texte is { Length: > LongueurMaxResultat } ? texte[..LongueurMaxResultat] : texte;
}
