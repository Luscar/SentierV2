using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Sentier.Commandes;
using Sentier.Definitions;
using Sentier.Execution;
using Sentier.Migration;
using Sentier.Persistance;
using Sentier.Variables;

namespace Sentier.Services;

/// <summary>
/// Migration des instances au repos vers une version plus récente : automatique pour les changements
/// compatibles, ou selon un plan écrit par le client. Aucune commande d'entrée n'est réexécutée.
/// </summary>
internal sealed class ServiceMigration(
    IServiceProvider services,
    ISessionSentier session,
    RegistreSentier registre,
    FournisseurDefinitions definitions,
    IDepotInstances instances,
    IDepotAttentes attentes,
    IDepotHistorique historique,
    IDepotMigrations migrations,
    MoteurSentier moteur,
    PlanificateurAttentes planificateur,
    ExecuteurAppels appels,
    IHorlogeSentier horloge)
{
    private readonly ILogger _logger = services.GetService<ILogger<ServiceMigration>>() ?? (ILogger)NullLogger.Instance;

    public Task<RapportMigration> SimulerAsync(DemandeMigration demande, CancellationToken ct) => ExecuterAsync(demande, simulation: true, ct);

    public Task<RapportMigration> MigrerAsync(DemandeMigration demande, CancellationToken ct)
    {
        session.ExigerTransaction();
        return ExecuterAsync(demande, simulation: false, ct);
    }

    /// <summary>Migre automatiquement les instances au repos des versions antérieures, lorsque le changement est compatible.</summary>
    public async Task<int> MigrerAutomatiquementAsync(DefinitionProcessus nouvelle, CancellationToken ct)
    {
        var migrees = 0;
        var versions = (await instances.CompterActivesAsync(nouvelle.CleProcessus, ct))
            .Where(c => c.IdDefinition != nouvelle.Id && c.Version < nouvelle.Version)
            .Select(c => c.IdDefinition)
            .Distinct()
            .ToList();

        foreach (var idSource in versions)
        {
            var source = await definitions.ObtenirAsync(idSource, ct);
            foreach (var instance in await instances.ObtenirAuReposAsync(idSource, null, int.MaxValue, ct))
            {
                var analyse = await AnalyserAsync(instance, source, nouvelle, plan: null, ct);
                if (analyse.Migrable)
                {
                    await AppliquerAsync(instance, source, nouvelle, analyse, plan: null, moteur.UtilisateurCourant, ct);
                    migrees++;
                }
            }
        }

        return migrees;
    }

    private async Task<RapportMigration> ExecuterAsync(DemandeMigration demande, bool simulation, CancellationToken ct)
    {
        var cible = demande.VersionCible is { } version
            ? await definitions.ObtenirParVersionAsync(demande.CleProcessus, version, ct) ?? throw new ProcessusIntrouvableException(demande.CleProcessus, version)
            : await definitions.ObtenirActiveAsync(demande.CleProcessus, ct) ?? throw new ProcessusIntrouvableException(demande.CleProcessus);
        var utilisateur = demande.Utilisateur ?? moteur.UtilisateurCourant;
        var plans = new Dictionary<int, PlanMigration?>();
        var resultats = new List<ResultatMigrationInstance>();

        foreach (var (id, instance, raisonSelection) in await SelectionnerAsync(demande, cible, ct))
        {
            var raison = raisonSelection;
            if (instance is null)
            {
                resultats.Add(new ResultatMigrationInstance { IdInstance = id, VersionSource = 0, VersionCible = cible.Version, Migrable = false, Raison = raison });
                continue;
            }

            var source = await definitions.ObtenirAsync(instance.IdDefinition, ct);
            if (raison is null && source.Version >= cible.Version)
            {
                raison = source.Version == cible.Version
                    ? "L'instance est déjà sur la version cible."
                    : $"La version cible v{cible.Version} est antérieure à la version de l'instance (v{source.Version}).";
            }

            if (raison is not null)
            {
                resultats.Add(new ResultatMigrationInstance { IdInstance = id, VersionSource = source.Version, VersionCible = cible.Version, Migrable = false, Raison = raison });
                continue;
            }

            if (!plans.TryGetValue(source.Version, out var plan))
            {
                plan = TrouverPlan(demande.CleProcessus, source.Version);
                plans[source.Version] = plan;
            }

            var analyse = await AnalyserAsync(instance, source, cible, plan, ct);
            if (analyse.Migrable && !simulation)
            {
                await AppliquerAsync(instance, source, cible, analyse, plan, utilisateur, ct);
            }

            resultats.Add(new ResultatMigrationInstance
            {
                IdInstance = id,
                VersionSource = source.Version,
                VersionCible = cible.Version,
                Migrable = analyse.Migrable,
                Raison = analyse.Raison,
                Actions = analyse.Actions,
            });
        }

        return new RapportMigration { CleProcessus = demande.CleProcessus, VersionCible = cible.Version, Simulation = simulation, Instances = resultats };
    }

    private async Task<List<(IdInstance Id, InstanceProcessus? Instance, string? Raison)>> SelectionnerAsync(DemandeMigration demande, DefinitionProcessus cible, CancellationToken ct)
    {
        var selection = new List<(IdInstance, InstanceProcessus?, string?)>();
        if (demande.Instances is not null)
        {
            foreach (var id in demande.Instances.Take(demande.TailleMax))
            {
                var instance = await instances.ObtenirAsync(id, ct);
                selection.Add(instance switch
                {
                    null => (id, null, "Instance introuvable."),
                    _ when instance.CleProcessus != demande.CleProcessus => (id, null, $"L'instance appartient au processus '{instance.CleProcessus}'."),
                    _ when !instance.EstAuRepos => (id, instance, $"L'instance n'est pas au repos (statut {instance.Statut})."),
                    _ => (id, instance, null),
                });
            }

            return selection;
        }

        var versions = (await instances.CompterActivesAsync(demande.CleProcessus, ct))
            .Where(c => c.IdDefinition != cible.Id && (demande.VersionSource is null || c.Version == demande.VersionSource))
            .Select(c => c.IdDefinition)
            .Distinct();

        foreach (var idDefinition in versions)
        {
            var restant = demande.TailleMax - selection.Count;
            if (restant <= 0)
            {
                break;
            }

            foreach (var instance in await instances.ObtenirAuReposAsync(idDefinition, demande.CodeNoeud, restant, ct))
            {
                selection.Add((instance.Id, instance, null));
            }
        }

        return selection;
    }

    private PlanMigration? TrouverPlan(string cleProcessus, int versionSource)
    {
        foreach (var type in registre.PlansMigration)
        {
            var source = (IPlanMigration)ActivatorUtilities.CreateInstance(services, type);
            if (source.CleProcessus != cleProcessus || versionSource < source.VersionSourceMin || versionSource > source.VersionSourceMax)
            {
                continue;
            }

            var constructeur = new ConstructeurPlanMigration();
            source.Definir(constructeur);
            var plan = constructeur.Construire(type.Name);
            foreach (var commande in plan.CommandesVariables.Where(c => registre.TypeCommande(c.Nom) is null))
            {
                throw new MigrationInvalideException($"Plan de migration '{type.Name}' : la commande '{commande.Nom}' n'est pas enregistrée.");
            }

            return plan;
        }

        return null;
    }

    /// <summary>Évalue la migration d'une instance, sans rien écrire.</summary>
    private async Task<AnalyseMigration> AnalyserAsync(InstanceProcessus instance, DefinitionProcessus source, DefinitionProcessus cible, PlanMigration? plan, CancellationToken ct)
    {
        var analyse = new AnalyseMigration();
        var code = instance.CodeNoeudCourant;
        var regle = plan?.Noeuds.GetValueOrDefault(code);
        var noeudSource = source.TrouverNoeud(code);

        if (regle?.CodeSuite is { } suite)
        {
            if (cible.TrouverNoeud(suite) is null)
            {
                throw new MigrationInvalideException($"Le nœud de suite '{suite}' du plan n'existe pas dans la v{cible.Version} de '{cible.CleProcessus}'.");
            }

            analyse.CodeCible = suite;
            analyse.CodeSuite = suite;
            analyse.Actions.Add($"annulation des attentes, tâches et sous-processus du nœud '{code}', puis reprise au nœud '{suite}'");
        }
        else if (!await AnalyserNoeudAsync(instance, noeudSource, cible, plan, regle, analyse, ct))
        {
            return analyse;
        }

        AnalyserVariables(instance, source, cible, plan, analyse);
        return analyse;
    }

    private async Task<bool> AnalyserNoeudAsync(InstanceProcessus instance, Noeud? noeudSource, DefinitionProcessus cible, PlanMigration? plan, RegleNoeudMigration? regle, AnalyseMigration analyse, CancellationToken ct)
    {
        var code = instance.CodeNoeudCourant;
        var codeCible = regle?.CodeCible ?? code;
        var noeudCible = cible.TrouverNoeud(codeCible);
        analyse.CodeCible = codeCible;

        if (noeudCible is null)
        {
            if (regle?.CodeCible is not null)
            {
                throw new MigrationInvalideException($"Le nœud '{codeCible}' désigné par le plan n'existe pas dans la v{cible.Version} de '{cible.CleProcessus}'.");
            }

            return analyse.Bloquer($"Le nœud '{code}' n'existe pas dans la v{cible.Version}{(plan is null ? "" : " et le plan ne lui donne aucune correspondance")}.");
        }

        if (noeudSource is not null && noeudSource.Type != noeudCible.Type)
        {
            return analyse.Bloquer($"Le nœud '{code}' change de type ({noeudSource.Type} → {noeudCible.Type}) : un plan avec un nœud de suite est requis.");
        }

        if (codeCible != code)
        {
            analyse.Actions.Add($"nœud '{code}' → '{codeCible}'");
        }

        switch (noeudCible)
        {
            case NoeudAttente attenteCible:
            {
                var existantes = (await attentes.ObtenirParInstanceAsync(instance.Id, ct)).Where(a => a.CodeNoeud == code).ToList();
                var compatibles = DeclencheursCompatibles(noeudSource as NoeudAttente, attenteCible, existantes, out var manquants);
                var action = regle?.Attentes ?? (compatibles || plan is null ? ActionAttentes.Conserver : ActionAttentes.Recreer);
                if (action == ActionAttentes.Recreer)
                {
                    analyse.Attentes = ActionAttentes.Recreer;
                    analyse.Actions.Add($"attentes du nœud '{codeCible}' recréées ({attenteCible.Declencheurs.Count} déclencheur(s))");
                }
                else if (!compatibles)
                {
                    return analyse.Bloquer($"Les déclencheurs de l'attente '{code}' sont modifiés ou retirés alors que l'instance attend.");
                }
                else if (manquants.Count > 0)
                {
                    analyse.AttentesACreer.AddRange(manquants);
                    analyse.Actions.Add($"nouveaux déclencheurs créés : {string.Join(", ", manquants.Select(m => m.NomSortie))}");
                }

                break;
            }

            case NoeudInteractif interactifCible when noeudSource is NoeudInteractif interactifSource:
            {
                if (instance.IdTacheCourante is not null && regle?.Taches != ActionTaches.Conserver && !InteractifsEquivalents(interactifSource, interactifCible))
                {
                    return analyse.Bloquer($"Le nœud interactif '{code}' est modifié alors que des tâches sont ouvertes.");
                }

                break;
            }

            case NoeudSousProcessus sousProcessusCible when noeudSource is NoeudSousProcessus sousProcessusSource:
            {
                if (plan is null && SerialisationDefinition.Serialiser(sousProcessusSource) != SerialisationDefinition.Serialiser(sousProcessusCible))
                {
                    return analyse.Bloquer($"Le sous-processus '{code}' est modifié alors qu'un enfant est actif.");
                }

                break;
            }
        }

        return true;
    }

    private void AnalyserVariables(InstanceProcessus instance, DefinitionProcessus source, DefinitionProcessus cible, PlanMigration? plan, AnalyseMigration analyse)
    {
        if (plan is null)
        {
            foreach (var declaration in cible.Variables)
            {
                if (source.Declarations.TryGetValue(declaration.Nom, out var ancienne) && ancienne.Type != declaration.Type)
                {
                    analyse.Bloquer($"Le type de la variable '{declaration.Nom}' change ({ancienne.Type} → {declaration.Type}).");
                    return;
                }
            }
        }

        var valeurs = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var (nom, valeur) in moteur.ChargerVariables(instance, source).Valeurs)
        {
            if (VariablesProcessus.EstReserve(nom))
            {
                valeurs[nom] = valeur;
                continue;
            }

            var nouveauNom = plan?.RenommagesVariables.GetValueOrDefault(nom) ?? nom;
            if (nouveauNom != nom)
            {
                analyse.Actions.Add($"variable '{nom}' renommée en '{nouveauNom}'");
            }

            if (!cible.Declarations.TryGetValue(nouveauNom, out var declaration))
            {
                if (plan?.RenommagesVariables.ContainsKey(nom) == true)
                {
                    throw new MigrationInvalideException($"Le plan renomme '{nom}' en '{nouveauNom}', qui n'est pas déclarée dans la v{cible.Version}.");
                }

                analyse.Actions.Add($"variable '{nom}' abandonnée (non déclarée dans la v{cible.Version})");
                continue;
            }

            try
            {
                valeurs[nouveauNom] = ConvertisseurValeurs.Normaliser(declaration.Type, valeur, nouveauNom);
            }
            catch (VariableInvalideException ex)
            {
                analyse.Bloquer(ex.Message);
                return;
            }
        }

        if (plan is not null)
        {
            foreach (var (nom, valeur) in plan.ValeursVariables)
            {
                if (!cible.Declarations.TryGetValue(nom, out var declaration))
                {
                    throw new MigrationInvalideException($"Le plan donne une valeur à '{nom}', qui n'est pas déclarée dans la v{cible.Version}.");
                }

                if (valeurs.GetValueOrDefault(nom) is null)
                {
                    valeurs[nom] = ConvertisseurValeurs.Normaliser(declaration.Type, valeur.VersObjet(nom), nom);
                    analyse.Actions.Add($"variable '{nom}' initialisée par le plan");
                }
            }

            if (plan.CommandesVariables.Count > 0)
            {
                analyse.Actions.Add($"variables calculées par : {string.Join(", ", plan.CommandesVariables.Select(c => c.Nom))}");
            }
        }

        foreach (var declaration in cible.Variables.Where(d => valeurs.GetValueOrDefault(d.Nom) is null))
        {
            if (declaration.Defaut is not null)
            {
                valeurs[declaration.Nom] = declaration.Defaut.VersObjet(declaration.Nom);
                analyse.Actions.Add($"variable '{declaration.Nom}' initialisée à sa valeur par défaut");
            }
            else if (declaration.Obligatoire && plan?.CommandesVariables.Count is null or 0)
            {
                analyse.Bloquer($"La variable obligatoire '{declaration.Nom}' de la v{cible.Version} n'a pas de valeur.");
                return;
            }
        }

        analyse.Variables = valeurs;
    }

    /// <summary>Applique une migration analysée et migrable, dans la transaction du client.</summary>
    private async Task AppliquerAsync(InstanceProcessus instance, DefinitionProcessus source, DefinitionProcessus cible, AnalyseMigration analyse, PlanMigration? plan, string? utilisateur, CancellationToken ct)
    {
        var codeSource = instance.CodeNoeudCourant;
        var maintenant = horloge.MaintenantUtc;
        var mode = plan is null ? ModeMigration.Auto : ModeMigration.Plan;

        var variables = new VariablesProcessus(cible.Declarations, analyse.Variables);
        foreach (var commande in plan?.CommandesVariables ?? [])
        {
            await appels.ExecuterCommandeAsync(commande, instance, codeSource, variables, MomentCommande.Migration, null, null, utilisateur, ct);
        }

        var manquantes = cible.Variables.Where(d => d.Obligatoire && !variables.Contient(d.Nom)).Select(d => d.Nom).ToList();
        if (manquantes.Count > 0)
        {
            throw new MigrationInvalideException($"Migration de l'instance {instance.Id} : variables obligatoires sans valeur après le plan : {string.Join(", ", manquantes)}.");
        }

        if (analyse.CodeSuite is not null)
        {
            await moteur.AnnulerTachesAsync(instance, $"Migration vers la v{cible.Version}", ct);

            await attentes.SupprimerParInstanceAsync(instance.Id, ct);
            foreach (var enfant in await instances.ObtenirEnfantsActifsAsync(instance.Id, ct))
            {
                await moteur.AnnulerAsync(enfant, $"Migration du parent {instance.Id} vers la v{cible.Version}", notifierParent: false, ct);
            }

            await historique.FermerPassagesOuvertsAsync(instance.Id, maintenant, $"MIGRATION v{source.Version} → v{cible.Version}", ct);
        }
        else
        {
            if (analyse.CodeCible != codeSource)
            {
                await attentes.RenommerNoeudAsync(instance.Id, codeSource, analyse.CodeCible, ct);
                if (moteur.TachesOptionnelles is { } taches)
                {
                    await taches.RenommerNoeudAsync(instance.Id, codeSource, analyse.CodeCible, ct);
                }
                await instances.RenommerNoeudParentAsync(instance.Id, codeSource, analyse.CodeCible, ct);
            }

            if (cible.Noeud(analyse.CodeCible) is NoeudAttente attenteCible)
            {
                if (analyse.Attentes == ActionAttentes.Recreer)
                {
                    await attentes.SupprimerParNoeudAsync(instance.Id, analyse.CodeCible, ct);
                    await planificateur.CreerAsync(instance, attenteCible, variables, attenteCible.Declencheurs, ct);
                }
                else if (analyse.AttentesACreer.Count > 0)
                {
                    await planificateur.CreerAsync(instance, attenteCible, variables, analyse.AttentesACreer, ct);
                }
            }
        }

        instance.IdDefinition = cible.Id!.Value;
        instance.CodeNoeudCourant = analyse.CodeCible;
        instance.VariablesJson = moteur.SerialiserVariables(variables);
        instance.DateMaj = maintenant;
        await instances.MettreAJourAsync(instance, ct);

        await migrations.InsererAsync(new TraceMigration
        {
            IdInstance = instance.Id,
            IdDefinitionSource = source.Id!.Value,
            IdDefinitionCible = cible.Id.Value,
            Mode = mode,
            Detail = JsonSerializer.Serialize(new { noeudSource = codeSource, noeudCible = analyse.CodeCible, actions = analyse.Actions }),
            Utilisateur = utilisateur,
            DateMigration = maintenant,
        }, ct);

        _logger.LogInformation(
            "Sentier : instance {IdInstance} ({CleProcessus}) migrée de la v{Source} vers la v{Cible} ({Mode}).",
            instance.Id, instance.CleProcessus, source.Version, cible.Version, mode);

        if (analyse.CodeSuite is not null)
        {
            await moteur.ExecuterNoeudCourantAsync(instance, utilisateur, ct);
        }
    }

    /// <summary>
    /// Les déclencheurs en cours sont compatibles si chacun existe, inchangé, dans la cible et si aucun
    /// déclencheur de la source n'est retiré. Les nouveaux déclencheurs de la cible sont retournés.
    /// </summary>
    private static bool DeclencheursCompatibles(NoeudAttente? source, NoeudAttente cible, IReadOnlyList<Attente> existantes, out List<Declencheur> manquants)
    {
        manquants = [];
        var parSortie = cible.Declencheurs.ToDictionary(d => d.NomSortie);
        var sourceParSortie = source?.Declencheurs.ToDictionary(d => d.NomSortie) ?? [];

        foreach (var ancien in sourceParSortie.Values)
        {
            if (!parSortie.TryGetValue(ancien.NomSortie, out var nouveau)
                || SerialisationDefinition.Serialiser(ancien) != SerialisationDefinition.Serialiser(nouveau))
            {
                return false;
            }
        }

        foreach (var attente in existantes)
        {
            if (!parSortie.TryGetValue(attente.NomSortie, out var nouveau) || nouveau.Type != attente.TypeDeclencheur)
            {
                return false;
            }
        }

        manquants.AddRange(cible.Declencheurs.Where(d => !sourceParSortie.ContainsKey(d.NomSortie) && existantes.All(a => a.NomSortie != d.NomSortie)));
        return true;
    }

    private static bool InteractifsEquivalents(NoeudInteractif source, NoeudInteractif cible)
    {
        static string Signature(NoeudInteractif n) => JsonSerializer.Serialize(
            new { n.NomTache, n.Role, n.Assigne, n.DureeEcheance, n.VariableEcheance, n.Donnees },
            SerialisationDefinition.Options);
        return Signature(source) == Signature(cible);
    }

    private sealed class AnalyseMigration
    {
        public bool Migrable { get; private set; } = true;

        public string? Raison { get; private set; }

        public List<string> Actions { get; } = [];

        public string CodeCible { get; set; } = "";

        public string? CodeSuite { get; set; }

        public ActionAttentes Attentes { get; set; } = ActionAttentes.Conserver;

        public List<Declencheur> AttentesACreer { get; } = [];

        public Dictionary<string, object?> Variables { get; set; } = [];

        public bool Bloquer(string raison)
        {
            Migrable = false;
            Raison = raison;
            return false;
        }
    }
}
