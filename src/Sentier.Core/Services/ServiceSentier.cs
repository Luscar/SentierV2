using Sentier.Definitions;
using Sentier.Execution;
using Sentier.Migration;
using Sentier.Persistance;
using Sentier.Variables;

namespace Sentier.Services;

/// <summary>Implémentation de <see cref="IServiceSentier"/>.</summary>
internal sealed class ServiceSentier(
    ISessionSentier session,
    MoteurSentier moteur,
    FournisseurDefinitions definitions,
    IDepotInstances instances,
    IDepotAttentes attentes,
    DeployeurDefinitions deployeur,
    ServiceMigration migration,
    IHorloge horloge) : IServiceSentier
{
    public async Task<ResultatExecution> DemarrerAsync(string cleProcessus, long cleAffaire, IReadOnlyDictionary<string, object?>? variables = null, CancellationToken ct = default)
    {
        session.ExigerTransaction();
        var definition = await definitions.ObtenirActiveAsync(cleProcessus, ct)
            ?? throw new ProcessusIntrouvableException(cleProcessus);

        if (definition.InstanceUnique)
        {
            var existantes = await instances.ObtenirActivesAsync(cleProcessus, cleAffaire, ct);
            if (existantes.Count > 0)
            {
                return ResultatExecution.Aucun($"Une instance active du processus '{cleProcessus}' existe déjà pour la clé d'affaire {cleAffaire}.", existantes[0]);
            }
        }

        var valeurs = VariablesProcessus.Initialiser(definition.Declarations, variables);
        var execution = await moteur.DemarrerAsync(definition, cleAffaire, valeurs, null, null, moteur.UtilisateurCourant, ct);
        return ResultatExecution.Depuis(execution.Instance);
    }

    public async Task<ResultatExecution> CompleterTacheAsync(long idTache, IReadOnlyDictionary<string, object?>? donnees = null, string? utilisateur = null, CancellationToken ct = default)
    {
        session.ExigerTransaction();
        var tache = await moteur.Taches.ObtenirTacheAsync(idTache, ct);
        if (tache is null)
        {
            return ResultatExecution.Aucun($"Aucune tâche ne correspond à l'identifiant {idTache}.");
        }

        // La référence est la tâche courante de l'instance, pas le statut tenu par le client : une tâche orpheline
        // ou périmée ne reprend jamais l'instance, et une tâche marquée complétée chez le client alors que la
        // transaction de Sentier a été annulée peut être complétée de nouveau.
        var instance = await instances.ObtenirAsync(tache.IdInstance, ct);
        if (instance is null || !instance.EstAuRepos || instance.IdTacheCourante != idTache)
        {
            return ResultatExecution.Aucun(
                tache.Statut == StatutTache.Ouverte
                    ? $"L'instance n'attend pas la tâche {idTache}."
                    : $"La tâche {idTache} est déjà {(tache.Statut == StatutTache.Completee ? "complétée" : "annulée")}.",
                instance);
        }

        if (tache.Statut == StatutTache.Annulee)
        {
            return ResultatExecution.Aucun($"La tâche {idTache} est annulée chez le client ; l'instance l'attend toujours.", instance);
        }

        var auteur = utilisateur ?? moteur.UtilisateurCourant;
        var execution = await moteur.ReprendreAsync(instance, new RepriseTache(tache, donnees, auteur), auteur, ct);
        return ResultatExecution.Depuis(execution.Instance);
    }

    public Task<ResultatSignal> EnvoyerSignalAsync(string nomSignal, string cleProcessus, long cleAffaire, IReadOnlyDictionary<string, object?>? chargeUtile = null, CancellationToken ct = default)
        => EnvoyerSignalAsync(new Signal { Nom = nomSignal, CleProcessus = cleProcessus, CleAffaire = cleAffaire, ChargeUtile = chargeUtile }, ct);

    public Task<ResultatSignal> EnvoyerSignalCorrelationAsync(string nomSignal, string cleCorrelation, IReadOnlyDictionary<string, object?>? chargeUtile = null, CancellationToken ct = default)
        => EnvoyerSignalAsync(new Signal { Nom = nomSignal, CleCorrelation = cleCorrelation, ChargeUtile = chargeUtile }, ct);

    public async Task<ResultatSignal> EnvoyerSignalAsync(Signal signal, CancellationToken ct = default)
    {
        session.ExigerTransaction();
        if (string.IsNullOrWhiteSpace(signal.Nom))
        {
            throw new ArgumentException("Le nom du signal est obligatoire.", nameof(signal));
        }

        if ((signal.CleAffaire is null) != (signal.CleProcessus is null))
        {
            throw new ArgumentException("Un signal qui cible une clé d'affaire doit aussi indiquer la clé du processus (et inversement).", nameof(signal));
        }

        if (signal.CleAffaire is null && signal.CleCorrelation is null)
        {
            throw new ArgumentException("Un signal doit cibler un processus et une clé d'affaire, une clé de corrélation, ou les deux.", nameof(signal));
        }

        var candidates = await attentes.RechercherSignalAsync(signal, ct);
        if (candidates.Count == 0)
        {
            return new ResultatSignal { Raison = $"Aucune instance n'attend le signal '{signal.Nom}'." };
        }

        var resultats = new List<ResultatExecution>();
        foreach (var groupe in candidates.GroupBy(a => a.IdInstance))
        {
            var attente = await attentes.VerrouillerAsync(groupe.First().Id, ct);
            resultats.Add(attente is null
                ? ResultatExecution.Aucun($"L'attente du signal '{signal.Nom}' a déjà été traitée.")
                : await DeclencherAsync(attente, signal.ChargeUtile, ct));
        }

        return new ResultatSignal
        {
            Resultats = resultats,
            Raison = resultats.All(r => r.SansEffet) ? $"Aucune instance n'a été reprise par le signal '{signal.Nom}'." : null,
        };
    }

    public async Task<ResultatExecution> AnnulerAsync(IdInstance idInstance, string motif, CancellationToken ct = default)
    {
        session.ExigerTransaction();
        var instance = await instances.ObtenirAsync(idInstance, ct);
        if (instance is null)
        {
            return ResultatExecution.Aucun($"L'instance {idInstance} est introuvable.");
        }

        if (!instance.EstActive)
        {
            return ResultatExecution.Aucun($"L'instance {idInstance} est déjà {(instance.Statut == StatutInstance.Terminee ? "terminée" : "annulée")}.", instance);
        }

        await moteur.AnnulerAsync(instance, motif, notifierParent: true, ct);
        return ResultatExecution.Depuis(instance);
    }

    public Task<RapportDeploiement> DeployerDefinitionsAsync(CancellationToken ct = default) => deployeur.DeployerAsync(ct);

    public Task<RapportMigration> SimulerMigrationAsync(DemandeMigration demande, CancellationToken ct = default) => migration.SimulerAsync(demande, ct);

    public Task<RapportMigration> MigrerAsync(DemandeMigration demande, CancellationToken ct = default) => migration.MigrerAsync(demande, ct);

    public Task<IReadOnlyList<EcheanceDue>> ObtenirEcheancesDuesAsync(int tailleMax = 100, CancellationToken ct = default)
        => attentes.ObtenirEcheancesDuesAsync(horloge.MaintenantUtc, tailleMax, ct);

    public async Task<ResultatExecution> TraiterEcheanceAsync(IdAttente idAttente, CancellationToken ct = default)
    {
        session.ExigerTransaction();
        var attente = await attentes.VerrouillerAsync(idAttente, ct);
        if (attente is null)
        {
            return ResultatExecution.Aucun($"L'échéance {idAttente} a déjà été traitée, gagnée par un autre déclencheur, ou est en cours de traitement.");
        }

        if (attente.TypeDeclencheur != TypeDeclencheur.Echeance)
        {
            return ResultatExecution.Aucun($"L'attente {idAttente} n'est pas une échéance.");
        }

        if (attente.DateEcheance > horloge.MaintenantUtc)
        {
            return ResultatExecution.Aucun($"L'échéance {idAttente} n'est pas encore atteinte.");
        }

        return await DeclencherAsync(attente, null, ct);
    }

    public async Task<ResultatExecution> MarquerEnErreurAsync(IdInstance idInstance, string message, CancellationToken ct = default)
    {
        session.ExigerTransaction();
        var instance = await instances.ObtenirAsync(idInstance, ct);
        if (instance is null)
        {
            return ResultatExecution.Aucun($"L'instance {idInstance} est introuvable.");
        }

        if (!instance.EstActive)
        {
            return ResultatExecution.Aucun($"L'instance {idInstance} n'est plus active.", instance);
        }

        await moteur.MarquerEnErreurAsync(instance, message, ct);
        return ResultatExecution.Depuis(instance);
    }

    public async Task<ResultatExecution> ReprendreAsync(IdInstance idInstance, CancellationToken ct = default)
    {
        session.ExigerTransaction();
        var instance = await instances.ObtenirAsync(idInstance, ct);
        if (instance is null)
        {
            return ResultatExecution.Aucun($"L'instance {idInstance} est introuvable.");
        }

        if (instance.Statut != StatutInstance.EnErreur)
        {
            return ResultatExecution.Aucun($"L'instance {idInstance} n'est pas en erreur.", instance);
        }

        var definition = await definitions.ObtenirAsync(instance.IdDefinition, ct);
        var noeud = definition.Noeud(instance.CodeNoeudCourant);
        if (noeud.EstSuspensif)
        {
            var enAttente = await attentes.ObtenirParInstanceAsync(instance.Id, ct);
            var enTache = noeud is NoeudInteractif && instance.IdTacheCourante is not null;
            var enfants = await instances.ObtenirEnfantsActifsAsync(instance.Id, ct);
            if (enAttente.Count > 0 || enTache || enfants.Count > 0)
            {
                // L'instance attend toujours : on la remet au repos, et une échéance déjà atteinte est traitée tout de suite.
                await moteur.RemettreAuReposAsync(instance, ct);
                var due = enAttente.FirstOrDefault(a => a.TypeDeclencheur == TypeDeclencheur.Echeance && a.DateEcheance <= horloge.MaintenantUtc);
                if (due is not null && await attentes.VerrouillerAsync(due.Id, ct) is { } verrouillee)
                {
                    return await DeclencherAsync(verrouillee, null, ct);
                }

                return ResultatExecution.Depuis(instance);
            }
        }

        var execution = await moteur.ExecuterNoeudCourantAsync(instance, moteur.UtilisateurCourant, ct);
        return ResultatExecution.Depuis(execution.Instance);
    }

    /// <summary>
    /// Fait gagner un déclencheur : toutes les attentes du nœud sont supprimées dans la même transaction,
    /// puis l'instance reprend. Le perdant d'une course ne trouve plus d'attente et est ignoré.
    /// </summary>
    private async Task<ResultatExecution> DeclencherAsync(Attente attente, IReadOnlyDictionary<string, object?>? chargeUtile, CancellationToken ct)
    {
        var instance = await instances.ObtenirAsync(attente.IdInstance, ct);
        if (instance is null || !instance.EstAuRepos || instance.CodeNoeudCourant != attente.CodeNoeud)
        {
            return ResultatExecution.Aucun("L'instance n'attend plus sur ce nœud.", instance);
        }

        if (await attentes.SupprimerParNoeudAsync(instance.Id, attente.CodeNoeud, ct) == 0)
        {
            return ResultatExecution.Aucun("L'attente a déjà été traitée par un autre déclencheur.", instance);
        }

        var execution = await moteur.ReprendreAsync(instance, new RepriseDeclencheur(attente, chargeUtile), moteur.UtilisateurCourant, ct);
        return ResultatExecution.Depuis(execution.Instance);
    }
}
