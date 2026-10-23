using Dapper;
using Sentier.Tests.Integration.Processus;
using Sentier.Tests.Integration.Support;
using Sentier.Variables;

namespace Sentier.Tests.Integration;

/// <summary>
/// Processus de bout en bout sur SQLite : démarrage, décision, tâche du client, attente d'un signal avec relances
/// par le batch des échéances, commandes métier dans la transaction du client, sous-processus, historique.
/// </summary>
public sealed class ParcoursDemandeCongeTests : IAsyncLifetime
{
    private const long Employe = 1001;
    private const string Cle = "demande-conge";

    private HoteSqlite _hote = null!;

    public async Task InitializeAsync()
    {
        _hote = await HoteSqlite.DemarrerAsync();
        _hote.Connexion.Execute("INSERT INTO CLIENT_EMPLOYE (ID, NOM, SOLDE) VALUES (@Employe, 'Alice', 15)", new { Employe });
    }

    public Task DisposeAsync()
    {
        _hote.Dispose();
        return Task.CompletedTask;
    }

    private long SoldeEmploye() => _hote.Connexion.ExecuteScalar<long>("SELECT SOLDE FROM CLIENT_EMPLOYE WHERE ID = @Employe", new { Employe });

    private Task<ResultatExecution> Demarrer(long jours) => _hote.Sentier(s => s.DemarrerAsync(Cle, Employe, ValeursVariables.Avec(DemandeConge.NbJours, jours), "alice"));

    private Task<ResultatExecution> Decider(long idTache, string decision, string utilisateur = "gestionnaire-1")
        => _hote.Sentier(s => s.CompleterTacheAsync(idTache, new Dictionary<string, object?> { ["decision"] = decision }, utilisateur));

    [Fact]
    public async Task Parcours_complet_d_une_demande_approuvee()
    {
        // 1. Démarrage : le solde est lu chez le client, la décision passe, la tâche d'approbation est créée.
        var demarrage = await Demarrer(jours: 5);

        Assert.Equal((StatutInstance.Suspendue, "approbation"), (demarrage.Statut, demarrage.CodeNoeudCourant));
        var tache = GestionnaireTachesSql.Lire(_hote.Connexion, demarrage.IdTacheCourante!.Value);
        Assert.Equal(("Approuver la demande", "gestionnaire", "OUVERTE"), (tache.Nom, tache.Role, tache.Statut));
        Assert.Equal(5L, tache.Donnee<long>("nbJours"));
        Assert.Equal(15L, tache.Donnee<long>("solde"));
        var etat = await _hote.InstanceAsync(demarrage.IdInstance);
        Assert.Equal(tache.Id, etat.IdTacheCourante);
        Assert.Equal(15L, etat.Variables["solde"]);

        // 2. Le gestionnaire approuve : commande de sortie (journal), puis attente de la validation RH.
        _hote.Horloge.Avancer(TimeSpan.FromHours(3));
        var approbation = await Decider(tache.Id, "approuvee");

        Assert.Equal((StatutInstance.Suspendue, "attendre-rh"), (approbation.Statut, approbation.CodeNoeudCourant));
        Assert.Null(approbation.IdTacheCourante);
        Assert.Equal(("COMPLETEE", "gestionnaire-1"), (GestionnaireTachesSql.Lire(_hote.Connexion, tache.Id).Statut, GestionnaireTachesSql.Lire(_hote.Connexion, tache.Id).CompleteePar));
        var attentes = await _hote.Suivi(s => s.ObtenirAttentesAsync(demarrage.IdInstance!.Value));
        Assert.Equal(["VALIDATION_RH", "echeance"], attentes.Select(a => a.NomSortie));
        Assert.Equal(_hote.Horloge.MaintenantUtc.AddDays(2), attentes[1].DateEcheance);

        // 3. Les RH tardent : le batch relance deux fois, sans rien faire avant l'échéance.
        Assert.Equal(0, await _hote.ExecuterBatchEcheancesAsync());
        _hote.Horloge.Avancer(TimeSpan.FromDays(2));
        Assert.Equal(1, await _hote.ExecuterBatchEcheancesAsync());
        _hote.Horloge.Avancer(TimeSpan.FromDays(2));
        Assert.Equal(1, await _hote.ExecuterBatchEcheancesAsync());
        Assert.Equal(2L, (await _hote.InstanceAsync(demarrage.IdInstance)).Variables["relances"]);

        // 4. Les RH valident : débit du solde, sous-processus d'archivage (terminé dans la même transaction), fin.
        _hote.Horloge.Avancer(TimeSpan.FromHours(1));
        var signal = await _hote.Sentier(s => s.EnvoyerSignalAsync(DemandeConge.SignalRh, Cle, Employe, new Dictionary<string, object?> { ["validePar"] = "rh-7" }));

        var fin = Assert.Single(signal.Resultats);
        Assert.Equal((StatutInstance.Terminee, "approuvee"), (fin.Statut, fin.CodeFin));
        Assert.Equal(10, SoldeEmploye());

        etat = await _hote.InstanceAsync(demarrage.IdInstance);
        Assert.Equal("rh-7", etat.Variables["validePar"]);
        Assert.Equal("approuvee", etat.Variables["decision"]);
        Assert.Equal("archivee", etat.Variables[VariablesProcessus.VariableFinEnfant]);
        var numero = Assert.IsType<string>(etat.Variables["numeroArchive"]);
        Assert.Equal(5L, _hote.Connexion.ExecuteScalar<long>("SELECT JOURS FROM CLIENT_ARCHIVE WHERE NUMERO = @numero", new { numero }));
        Assert.Equal(_hote.Horloge.MaintenantUtc, etat.DateFin);
        Assert.Equal(HorlogeSimulee.Origine, etat.DateCreation);

        // 5. Traces : journal du client, historique de Sentier, plus aucune attente ni instance active.
        Assert.Equal(
            [("decision-gestionnaire", "approuvee", "gestionnaire-1"), ("relance-rh", "1", null), ("relance-rh", "2", null)],
            JournalClient.Lire(_hote.Connexion, Employe));

        var historique = await _hote.Suivi(s => s.ObtenirHistoriqueAsync(demarrage.IdInstance!.Value));
        Assert.Equal(
            ["debut", "calculer-solde", "solde-suffisant", "approbation", "est-approuvee", "attendre-rh", "relancer-rh", "attendre-rh", "relancer-rh", "attendre-rh", "debiter-solde", "archivage", "approuvee"],
            historique.Select(p => p.CodeNoeud));
        Assert.Equal("SoldeSuffisant", historique[2].Resultat);
        Assert.Equal("tâche complétée par gestionnaire-1", historique[3].Resultat);
        Assert.Equal(["echeance", "echeance", "VALIDATION_RH"], historique.Where(p => p.CodeNoeud == "attendre-rh").Select(p => p.Resultat));
        Assert.All(historique, p => Assert.NotNull(p.DateSortie));
        Assert.Equal(HorlogeSimulee.Origine.AddHours(3), historique[3].DateSortie);

        var instances = await _hote.Suivi(s => s.ObtenirInstancesParCleAffaireAsync(Employe, [Cle, "archivage-conge"]));
        Assert.Equal([(Cle, "approuvee"), ("archivage-conge", "archivee")], instances.Select(i => (i.CleProcessus, i.CodeFin!)));
        Assert.Equal(demarrage.IdInstance, instances[1].IdInstanceParent);
        Assert.Equal(0, _hote.Compter("_ATTEN_PROCS"));
        Assert.Equal(0, _hote.Compter("_V_INSTC_ACTIV"));
    }

    [Fact]
    public async Task Solde_insuffisant_la_demande_est_refusee_sans_tache()
    {
        var resultat = await Demarrer(jours: 20);

        Assert.Equal((StatutInstance.Terminee, "refusee"), (resultat.Statut, resultat.CodeFin));
        Assert.Empty(GestionnaireTachesSql.LireParInstance(_hote.Connexion, resultat.IdInstance!.Value));
        Assert.Equal(15, SoldeEmploye());
        var historique = await _hote.Suivi(s => s.ObtenirHistoriqueAsync(resultat.IdInstance!.Value));
        Assert.Equal(["debut", "calculer-solde", "solde-suffisant", "refusee"], historique.Select(p => p.CodeNoeud));
    }

    [Fact]
    public async Task Refus_du_gestionnaire()
    {
        var demarrage = await Demarrer(jours: 5);
        var resultat = await Decider(demarrage.IdTacheCourante!.Value, "refusee");

        Assert.Equal("refusee-gestionnaire", resultat.CodeFin);
        Assert.Equal(15, SoldeEmploye());
        Assert.Equal([("decision-gestionnaire", "refusee", "gestionnaire-1")], JournalClient.Lire(_hote.Connexion, Employe));
    }

    [Fact]
    public async Task Une_seule_demande_active_par_employe_puis_une_nouvelle_apres_la_fin()
    {
        var premiere = await Demarrer(jours: 2);
        var doublon = await Demarrer(jours: 3);

        Assert.True(doublon.SansEffet);
        Assert.Equal(premiere.IdInstance, doublon.IdInstance);
        Assert.Equal(1, _hote.Compter("_INSTC_PROCS"));

        await Decider(premiere.IdTacheCourante!.Value, "refusee");
        var seconde = await Demarrer(jours: 3);
        Assert.False(seconde.SansEffet);
        Assert.NotEqual(premiere.IdInstance, seconde.IdInstance);
    }

    [Fact]
    public async Task Les_actions_rejouees_sont_sans_effet()
    {
        var demarrage = await Demarrer(jours: 5);
        var idTache = demarrage.IdTacheCourante!.Value;
        await Decider(idTache, "approuvee");

        Assert.True((await Decider(idTache, "refusee")).SansEffet);
        Assert.True((await _hote.Sentier(s => s.CompleterTacheAsync(424242))).SansEffet);
        Assert.True((await _hote.Sentier(s => s.EnvoyerSignalAsync(DemandeConge.SignalRh, Cle, 9999))).SansEffet);
        Assert.True((await _hote.Sentier(s => s.EnvoyerSignalAsync("SIGNAL_INCONNU", Cle, Employe))).SansEffet);

        await _hote.Sentier(s => s.EnvoyerSignalAsync(DemandeConge.SignalRh, Cle, Employe));
        Assert.True((await _hote.Sentier(s => s.EnvoyerSignalAsync(DemandeConge.SignalRh, Cle, Employe))).SansEffet);
        Assert.True((await _hote.Sentier(s => s.AnnulerAsync(demarrage.IdInstance!.Value, "trop tard"))).SansEffet);
        Assert.Equal(10, SoldeEmploye());
    }

    [Fact]
    public async Task Annulation_en_cours_de_route()
    {
        var demarrage = await Demarrer(jours: 5);
        await Decider(demarrage.IdTacheCourante!.Value, "approuvee");
        var autre = await _hote.Sentier(s => s.DemarrerAsync(Cle, 2002, ValeursVariables.Avec(DemandeConge.NbJours, 1L)));

        var annulation = await _hote.Sentier(s => s.AnnulerAsync(demarrage.IdInstance!.Value, "Demande retirée par l'employé"));

        Assert.Equal(StatutInstance.Annulee, annulation.Statut);
        Assert.Equal(0, _hote.Compter("_ATTEN_PROCS", $"NO_SEQ_INSTC_PROCS = {demarrage.IdInstance!.Value.Valeur}"));
        Assert.Equal("ANNULEE : Demande retirée par l'employé", (await _hote.Suivi(s => s.ObtenirHistoriqueAsync(demarrage.IdInstance!.Value)))[^1].Resultat);
        Assert.True((await _hote.Sentier(s => s.EnvoyerSignalAsync(DemandeConge.SignalRh, Cle, Employe))).SansEffet);

        // L'autre demande (employé inconnu : solde 0) n'est pas touchée.
        Assert.Equal("refusee", autre.CodeFin);
    }

    [Fact]
    public async Task Annuler_une_demande_annule_sa_tache_chez_le_client()
    {
        var demarrage = await Demarrer(jours: 5);

        await _hote.Sentier(s => s.AnnulerAsync(demarrage.IdInstance!.Value, "doublon"));

        var tache = GestionnaireTachesSql.Lire(_hote.Connexion, demarrage.IdTacheCourante!.Value);
        Assert.Equal(("ANNULEE", "doublon"), (tache.Statut, tache.Motif));
        Assert.Empty(await _hote.Suivi(s => s.ObtenirTachesOuvertesAsync(demarrage.IdInstance!.Value)));
    }

    [Fact]
    public async Task Le_suivi_expose_l_etat_les_comptes_et_le_diagramme()
    {
        await Demarrer(jours: 5);
        await _hote.Sentier(s => s.DemarrerAsync(Cle, 3003, ValeursVariables.Avec(DemandeConge.NbJours, 1L)));

        var comptes = await _hote.Suivi(s => s.CompterInstancesActivesAsync(Cle));
        Assert.Equal([(1, "approbation", 1)], comptes.Select(c => (c.Version, c.CodeNoeud, c.Nombre)));

        var diagramme = await _hote.Suivi(s => s.ExporterDiagrammeAsync(Cle));
        Assert.Contains("%% Demande de congé (demande-conge v1)", diagramme);
        Assert.Contains("n_attendre_rh -->|echeance| n_relancer_rh", diagramme);
        Assert.Contains("n_relancer_rh --> n_attendre_rh", diagramme);
        Assert.Contains("[[\"archivage<br/><small>archivage-conge</small>\"]]", diagramme);

        var enCours = await _hote.Suivi(s => s.ObtenirInstancesParCleAffaireAsync(Employe, [Cle], [StatutInstance.Suspendue]));
        Assert.Single(enCours);
        Assert.Empty(await _hote.Suivi(s => s.ObtenirInstancesParCleAffaireAsync(Employe, [Cle], [StatutInstance.Terminee])));
    }
}
