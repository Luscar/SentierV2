using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Sentier.Commandes;
using Sentier.Definitions;
using Sentier.Execution;
using Sentier.Persistance;

namespace Sentier.Services;

/// <summary>
/// Déploiement des définitions découvertes : validation complète, nouvelle version si l'empreinte change,
/// avertissements et migration automatique des changements compatibles.
/// </summary>
internal sealed class DeployeurDefinitions(
    IServiceProvider services,
    ISessionSentier session,
    RegistreSentier registre,
    IDepotDefinitions depot,
    IDepotInstances instances,
    FournisseurDefinitions fournisseur,
    ServiceMigration migration,
    IHorlogeSentier horloge)
{
    private readonly ILogger _logger = services.GetService<ILogger<DeployeurDefinitions>>() ?? (ILogger)NullLogger.Instance;

    public async Task<RapportDeploiement> DeployerAsync(CancellationToken ct)
    {
        session.ExigerTransaction();
        var erreurs = new List<string>();
        var definitions = Construire(erreurs);

        var parCle = new Dictionary<string, DefinitionProcessus>(StringComparer.Ordinal);
        foreach (var definition in definitions)
        {
            if (!parCle.TryAdd(definition.CleProcessus, definition))
            {
                erreurs.Add($"Le processus '{definition.CleProcessus}' est défini par plusieurs classes IDefinitionSentier.");
            }
        }

        foreach (var definition in parCle.Values)
        {
            foreach (var erreur in await ValiderAsync(definition, parCle, ct))
            {
                erreurs.Add($"[{definition.CleProcessus}] {erreur}");
            }
        }

        erreurs.AddRange(await VerifierVersionsEnCoursAsync(parCle, ct));

        if (erreurs.Count > 0)
        {
            throw new DefinitionInvalideException("déploiement", erreurs);
        }

        var resultats = new List<ResultatDeploiement>();
        foreach (var definition in parCle.Values)
        {
            resultats.Add(await DeployerAsync(definition, ct));
        }

        return new RapportDeploiement(resultats);
    }

    private List<DefinitionProcessus> Construire(List<string> erreurs)
    {
        var definitions = new List<DefinitionProcessus>();
        foreach (var type in registre.Definitions)
        {
            try
            {
                var source = (IDefinitionSentier)ActivatorUtilities.CreateInstance(services, type);
                definitions.Add(source.Definir());
            }
            catch (DefinitionInvalideException ex)
            {
                erreurs.AddRange(ex.Erreurs.Select(e => $"[{ex.CleProcessus}] {e}"));
            }
        }

        return definitions;
    }

    private async Task<List<string>> ValiderAsync(DefinitionProcessus definition, IReadOnlyDictionary<string, DefinitionProcessus> deployees, CancellationToken ct)
    {
        var erreurs = new List<string>(ValidateurDefinition.Valider(definition));

        foreach (var (noeud, commande) in Commandes(definition))
        {
            if (registre.TypeCommande(commande.Nom) is null)
            {
                erreurs.Add($"La commande '{commande.Nom}' (nœud '{noeud.Code}') n'est pas enregistrée.");
            }
        }

        foreach (var transition in definition.Transitions.Where(t => t.Requete is not null))
        {
            if (registre.TypeRequete(transition.Requete!.Nom) is null)
            {
                erreurs.Add($"La requête '{transition.Requete.Nom}' (nœud '{transition.Source}') n'est pas enregistrée.");
            }
        }

        if (definition.Noeuds.OfType<NoeudInteractif>().FirstOrDefault() is { } interactif && !EstEnregistre<IGestionnaireTachesSentier>())
        {
            erreurs.Add($"Le nœud interactif '{interactif.Code}' exige un IGestionnaireTachesSentier, et aucun n'est enregistré.");
        }

        foreach (var sousProcessus in definition.Noeuds.OfType<NoeudSousProcessus>())
        {
            var enfant = deployees.GetValueOrDefault(sousProcessus.CleProcessus)
                ?? await fournisseur.ObtenirActiveAsync(sousProcessus.CleProcessus, ct);
            if (enfant is null)
            {
                erreurs.Add($"Le processus enfant '{sousProcessus.CleProcessus}' du sous-processus '{sousProcessus.Code}' n'existe pas.");
                continue;
            }

            erreurs.AddRange(ValidateurSousProcessus.Valider(definition, sousProcessus, enfant));
        }

        return erreurs;
    }

    /// <summary>
    /// Vérifie les versions déjà déployées qui ont encore des instances actives : leurs commandes et requêtes,
    /// référencées par nom dans le contenu stocké, doivent toujours être enregistrées. Détecte au déploiement
    /// une classe renommée ou supprimée, qui ferait échouer ces instances à l'exécution.
    /// Les versions identiques à la définition déployée sont déjà vérifiées par <see cref="ValiderAsync"/>.
    /// </summary>
    private async Task<List<string>> VerifierVersionsEnCoursAsync(IReadOnlyDictionary<string, DefinitionProcessus> deployees, CancellationToken ct)
    {
        var erreurs = new List<string>();
        var empreintes = deployees.ToDictionary(d => d.Key, d => SerialisationDefinition.CalculerEmpreinte(SerialisationDefinition.Serialiser(d.Value)), StringComparer.Ordinal);
        foreach (var (idDefinition, nombre) in await instances.CompterActivesParVersionAsync(ct))
        {
            if (await depot.ObtenirAsync(idDefinition, ct) is not { } stockee
                || (empreintes.TryGetValue(stockee.CleProcessus, out var empreinte) && empreinte == stockee.Empreinte))
            {
                continue;
            }

            var definition = fournisseur.Charger(stockee);
            var prefixe = $"[{stockee.CleProcessus} v{stockee.Version}, {nombre} instance(s) active(s)]";
            foreach (var nom in Commandes(definition).Select(c => c.Commande.Nom).Distinct().Where(n => registre.TypeCommande(n) is null))
            {
                erreurs.Add($"{prefixe} La commande '{nom}' n'est plus enregistrée : si la classe a été renommée, ajoutez-lui [CommandeSentier(\"{nom}\")] ; sinon, gardez-la jusqu'à la migration de ces instances.");
            }

            var requetes = definition.Transitions.Where(t => t.Requete is not null).Select(t => t.Requete!.Nom).Distinct();
            foreach (var nom in requetes.Where(n => registre.TypeRequete(n) is null))
            {
                erreurs.Add($"{prefixe} La requête '{nom}' n'est plus enregistrée : si la classe a été renommée, ajoutez-lui [RequeteSentier(\"{nom}\")] ; sinon, gardez-la jusqu'à la migration de ces instances.");
            }

            if (definition.Noeuds.OfType<NoeudInteractif>().Any() && !EstEnregistre<IGestionnaireTachesSentier>())
            {
                erreurs.Add($"{prefixe} Cette version a des nœuds interactifs, et aucun IGestionnaireTachesSentier n'est enregistré.");
            }
        }

        return erreurs;
    }

    /// <summary>Vérifie l'enregistrement d'un service sans l'instancier (ses dépendances peuvent manquer au déploiement).</summary>
    private bool EstEnregistre<T>()
        => services.GetService<IServiceProviderIsService>() is { } verificateur
            ? verificateur.IsService(typeof(T))
            : services.GetService<T>() is not null;

    private async Task<ResultatDeploiement> DeployerAsync(DefinitionProcessus definition, CancellationToken ct)
    {
        var cle = definition.CleProcessus;
        var contenu = SerialisationDefinition.Serialiser(definition);
        var empreinte = SerialisationDefinition.CalculerEmpreinte(contenu);
        var derniere = await depot.ObtenirDerniereAsync(cle, ct);

        if (derniere is not null && derniere.Empreinte == empreinte)
        {
            var restantes = (await instances.CompterActivesAsync(cle, ct)).Where(c => c.IdDefinition != derniere.Id).Sum(c => c.Nombre);
            return new ResultatDeploiement { CleProcessus = cle, Etat = EtatDeploiement.Inchangee, Version = derniere.Version, InstancesRestees = restantes };
        }

        var stockee = new DefinitionStockee
        {
            CleProcessus = cle,
            Version = (derniere?.Version ?? 0) + 1,
            Nom = definition.Nom,
            InstanceUnique = definition.InstanceUnique,
            MigrationAuto = definition.MigrationAutomatique,
            Contenu = contenu,
            Empreinte = empreinte,
            EstActive = true,
            DateCreation = horloge.MaintenantUtc,
        };
        await depot.InsererAsync(stockee, ct);
        var nouvelle = fournisseur.Charger(stockee);

        var avertissements = new List<string>();
        foreach (var compte in await instances.CompterActivesAsync(cle, ct))
        {
            if (compte.IdDefinition != stockee.Id && nouvelle.TrouverNoeud(compte.CodeNoeud) is null)
            {
                avertissements.Add($"Le nœud '{compte.CodeNoeud}' disparaît dans la v{stockee.Version} alors que {compte.Nombre} instance(s) active(s) de la v{compte.Version} s'y trouvent.");
            }
        }

        var migrees = definition.MigrationAutomatique ? await migration.MigrerAutomatiquementAsync(nouvelle, ct) : 0;
        var restees = (await instances.CompterActivesAsync(cle, ct)).Where(c => c.IdDefinition != stockee.Id).Sum(c => c.Nombre);

        foreach (var avertissement in avertissements)
        {
            _logger.LogWarning("Sentier : {Avertissement}", avertissement);
        }

        _logger.LogInformation(
            "Sentier : processus {CleProcessus} déployé en v{Version} ({Migrees} instance(s) migrée(s), {Restees} restée(s) sur une version antérieure).",
            cle, stockee.Version, migrees, restees);

        return new ResultatDeploiement
        {
            CleProcessus = cle,
            Etat = definition.MigrationAutomatique && restees > 0 ? EtatDeploiement.NouvelleVersionIncompatible : EtatDeploiement.NouvelleVersion,
            Version = stockee.Version,
            InstancesMigrees = migrees,
            InstancesRestees = restees,
            Avertissements = avertissements,
        };
    }

    /// <summary>Toutes les commandes référencées par une définition, avec leur nœud.</summary>
    internal static IEnumerable<(Noeud Noeud, AppelCommande Commande)> Commandes(DefinitionProcessus definition)
    {
        foreach (var noeud in definition.Noeuds)
        {
            var appels = noeud switch
            {
                NoeudSystemique s => [s.Commande],
                NoeudAttente a => a.CommandesEntree,
                NoeudInteractif i => i.CommandesEntree.Concat(i.CommandesSortie),
                _ => [],
            };

            foreach (var appel in appels)
            {
                yield return (noeud, appel);
            }
        }
    }
}
