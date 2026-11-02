using System.Text.Json;
using Sentier.Integration.Tests.Infrastructure;
using Sentier.Integration.Tests.Processus;

namespace Sentier.Integration.Tests.Scenarios;

/// <summary>Parcours complets d'un processus avec tâche utilisateur.</summary>
public sealed class DemandeCongeTests : IAsyncLifetime
{
    private const long Employe = 42;
    private EnvironnementSentier _env = null!;

    public async Task InitializeAsync()
    {
        _env = await EnvironnementSentier.DemarrerAsync();
        _env.Metier.Soldes[Employe] = 10;
    }

    public async Task DisposeAsync() => await _env.DisposeAsync();

    private Task<ResultatExecution> DemarrerAsync(long nbJours, long employe = Employe)
        => _env.DemarrerAsync(DemandeConge.Cle, employe, new Dictionary<string, object?> { ["nbJours"] = nbJours });

    [Fact]
    public async Task Solde_insuffisant_la_demande_est_refusee_dans_le_meme_appel()
    {
        var resultat = await DemarrerAsync(nbJours: 15);

        Assert.Equal(StatutInstance.Terminee, resultat.Statut);
        Assert.Equal("refusee", resultat.CodeFin);
        Assert.Null(resultat.IdTacheCourante);

        var historique = await _env.HistoriqueAsync(resultat.IdInstance!.Value);
        Assert.Equal(["debut", "calculer-solde", "solde-suffisant", "refusee"], historique.Select(p => p.CodeNoeud));
        Assert.Equal("solde-insuffisant", historique.Single(p => p.CodeNoeud == "solde-suffisant").Resultat);
        Assert.Contains($"calculer-solde:{Employe}", _env.Metier.Journal);
    }

    [Fact]
    public async Task Solde_suffisant_une_tache_est_creee_chez_le_client_avec_ses_donnees()
    {
        var resultat = await DemarrerAsync(nbJours: 3);

        Assert.Equal(StatutInstance.Suspendue, resultat.Statut);
        Assert.Equal(DemandeConge.NoeudApprobation, resultat.CodeNoeudCourant);
        Assert.NotNull(resultat.IdTacheCourante);

        var tache = await _env.TacheAsync(resultat.IdTacheCourante!.Value);
        Assert.NotNull(tache);
        Assert.Equal("Approuver la demande", tache.NomTache);
        Assert.Equal("gestionnaire", tache.Role);
        Assert.Equal(Employe, tache.CleAffaire);
        Assert.Equal("Ouverte", tache.Statut);
        using var donnees = JsonDocument.Parse(tache.Donnees!);
        Assert.Equal(3, donnees.RootElement.GetProperty("nbJours").GetInt64());
        Assert.Equal(10, donnees.RootElement.GetProperty("solde").GetInt64());

        var instance = await _env.InstanceAsync(resultat.IdInstance!.Value);
        Assert.Equal(10L, instance.Variables["solde"]);
        var ouvertes = await _env.LireAsync(s => s.Suivi().ObtenirTachesOuvertesAsync(resultat.IdInstance!.Value));
        Assert.Equal(resultat.IdTacheCourante, Assert.Single(ouvertes).Id);
    }

    [Fact]
    public async Task Completer_la_tache_fusionne_les_donnees_execute_la_commande_de_sortie_et_termine()
    {
        var depart = await DemarrerAsync(nbJours: 3);

        var fin = await _env.CompleterTacheAsync(depart.IdTacheCourante!.Value,
            new Dictionary<string, object?> { ["decision"] = "approuvee", ["inconnue"] = "ignorée" }, utilisateur: "gestionnaire1");

        Assert.Equal(StatutInstance.Terminee, fin.Statut);
        Assert.Equal("approuvee", fin.CodeFin);
        Assert.Contains($"decision:{Employe}:approuvee:gestionnaire1", _env.Metier.Journal);

        var instance = await _env.InstanceAsync(depart.IdInstance!.Value);
        Assert.Equal("approuvee", instance.Variables["decision"]);
        Assert.False(instance.Variables.ContainsKey("inconnue"));
        Assert.NotNull(instance.DateFin);

        var tache = await _env.TacheAsync(depart.IdTacheCourante!.Value);
        Assert.Equal("Completee", tache!.Statut);
        Assert.Equal("gestionnaire1", tache.CompleteePar);
    }

    [Fact]
    public async Task Une_decision_autre_qu_approuvee_mene_au_rejet()
    {
        var depart = await DemarrerAsync(nbJours: 3);

        var fin = await _env.CompleterTacheAsync(depart.IdTacheCourante!.Value, new Dictionary<string, object?> { ["decision"] = "non" });

        Assert.Equal("rejetee", fin.CodeFin);
    }

    [Fact]
    public async Task Completer_deux_fois_la_meme_tache_est_sans_effet()
    {
        var depart = await DemarrerAsync(nbJours: 3);
        await _env.CompleterTacheAsync(depart.IdTacheCourante!.Value, new Dictionary<string, object?> { ["decision"] = "approuvee" });

        var second = await _env.CompleterTacheAsync(depart.IdTacheCourante!.Value);

        Assert.True(second.SansEffet);
        Assert.Contains("complétée", second.Raison);
    }

    [Fact]
    public async Task Une_tache_inconnue_est_sans_effet()
    {
        var resultat = await _env.CompleterTacheAsync(999_999);

        Assert.True(resultat.SansEffet);
        Assert.Null(resultat.IdInstance);
    }

    [Fact]
    public async Task Assigner_la_tache_passe_par_le_gestionnaire_du_client()
    {
        var depart = await DemarrerAsync(nbJours: 3);

        var resultat = await _env.ExecuterAsync(s => s.Service().AssignerTacheAsync(depart.IdTacheCourante!.Value, "jdoe"));

        Assert.False(resultat.SansEffet);
        Assert.Equal("jdoe", (await _env.TacheAsync(depart.IdTacheCourante!.Value))!.Assigne);
    }

    [Fact]
    public async Task Une_seule_instance_active_par_cle_d_affaire()
    {
        var premiere = await DemarrerAsync(nbJours: 3);

        var seconde = await DemarrerAsync(nbJours: 3);

        Assert.True(seconde.SansEffet);
        Assert.Equal(premiere.IdInstance, seconde.IdInstance);

        // Une autre clé d'affaire démarre normalement.
        _env.Metier.Soldes[43] = 10;
        Assert.False((await DemarrerAsync(nbJours: 3, employe: 43)).SansEffet);
    }

    [Fact]
    public async Task Apres_la_fin_une_nouvelle_instance_peut_demarrer_sur_la_meme_cle()
    {
        await DemarrerAsync(nbJours: 50);   // refusée, donc terminée

        var nouvelle = await DemarrerAsync(nbJours: 3);

        Assert.False(nouvelle.SansEffet);
        var instances = await _env.LireAsync(s => s.Suivi().ObtenirInstancesParCleAffaireAsync(Employe, [DemandeConge.Cle]));
        Assert.Equal(2, instances.Count);
        var actives = await _env.LireAsync(s => s.Suivi().ObtenirInstancesParCleAffaireAsync(Employe, [DemandeConge.Cle], [StatutInstance.Suspendue]));
        Assert.Equal(nouvelle.IdInstance, Assert.Single(actives).Id);
    }

    [Fact]
    public async Task Une_variable_obligatoire_manquante_est_refusee_et_rien_n_est_ecrit()
    {
        await Assert.ThrowsAsync<VariableInvalideException>(() => _env.DemarrerAsync(DemandeConge.Cle, Employe));

        var instances = await _env.LireAsync(s => s.Suivi().ObtenirInstancesParCleAffaireAsync(Employe, [DemandeConge.Cle]));
        Assert.Empty(instances);
    }

    [Fact]
    public async Task Une_variable_non_declaree_ou_mal_typee_au_demarrage_est_refusee()
    {
        await Assert.ThrowsAsync<VariableInvalideException>(() =>
            _env.DemarrerAsync(DemandeConge.Cle, Employe, new Dictionary<string, object?> { ["nbJours"] = 3L, ["inconnue"] = 1 }));
        await Assert.ThrowsAsync<VariableInvalideException>(() =>
            _env.DemarrerAsync(DemandeConge.Cle, Employe, new Dictionary<string, object?> { ["nbJours"] = "trois" }));
    }

    [Fact]
    public async Task Un_processus_inconnu_est_refuse()
        => await Assert.ThrowsAsync<ProcessusIntrouvableException>(() => _env.DemarrerAsync("inconnu", 1));

    [Fact]
    public async Task Sans_transaction_sentier_refuse_d_ecrire()
        => await _env.SansTransactionAsync(s => Assert.ThrowsAsync<TransactionRequiseException>(() =>
            s.Service().DemarrerAsync(DemandeConge.Cle, Employe, new Dictionary<string, object?> { ["nbJours"] = 3L })));

    [Fact]
    public async Task Annuler_l_instance_annule_sa_tache_ouverte()
    {
        var depart = await DemarrerAsync(nbJours: 3);

        var resultat = await _env.ExecuterAsync(s => s.Service().AnnulerAsync(depart.IdInstance!.Value, "demande retirée"));

        Assert.Equal(StatutInstance.Annulee, resultat.Statut);
        var tache = await _env.TacheAsync(depart.IdTacheCourante!.Value);
        Assert.Equal("Annulee", tache!.Statut);
        Assert.Equal("demande retirée", tache.MotifAnnulation);

        // La tâche annulée ne peut plus reprendre l'instance, et une seconde annulation est sans effet.
        Assert.True((await _env.CompleterTacheAsync(depart.IdTacheCourante!.Value)).SansEffet);
        Assert.True((await _env.ExecuterAsync(s => s.Service().AnnulerAsync(depart.IdInstance!.Value, "encore"))).SansEffet);
    }

    [Fact]
    public async Task Modifier_les_variables_d_une_instance_au_repos()
    {
        var depart = await DemarrerAsync(nbJours: 3);
        var id = depart.IdInstance!.Value;

        var resultat = await _env.ExecuterAsync(s => s.Service().ModifierVariablesAsync(id, new Dictionary<string, object?> { ["motif"] = "vacances" }, "admin"));

        Assert.False(resultat.SansEffet);
        Assert.Equal("vacances", (await _env.InstanceAsync(id)).Variables["motif"]);
        Assert.Equal(DemandeConge.NoeudApprobation, (await _env.InstanceAsync(id)).CodeNoeudCourant);

        await Assert.ThrowsAsync<VariableInvalideException>(() =>
            _env.ExecuterAsync(s => s.Service().ModifierVariablesAsync(id, new Dictionary<string, object?> { ["nbJours"] = null })));
        await Assert.ThrowsAsync<VariableInvalideException>(() =>
            _env.ExecuterAsync(s => s.Service().ModifierVariablesAsync(id, new Dictionary<string, object?> { ["nbJours"] = "dix" })));
    }

    [Fact]
    public async Task Rechercher_les_instances_par_variables()
    {
        _env.Metier.Soldes[1] = 20;
        _env.Metier.Soldes[2] = 20;
        _env.Metier.Soldes[3] = 20;
        await DemarrerAsync(nbJours: 2, employe: 1);
        await DemarrerAsync(nbJours: 5, employe: 2);
        await DemarrerAsync(nbJours: 8, employe: 3);

        var longues = await _env.LireAsync(s => s.Suivi().RechercherParVariablesAsync(new RechercheParVariables
        {
            CleProcessus = DemandeConge.Cle,
            Filtres = [FiltreVariable.SuperieurOuEgal(DemandeConge.NbJours, 5L), FiltreVariable.EstVide(DemandeConge.Decision)],
        }));
        var exacte = await _env.LireAsync(s => s.Suivi().RechercherParVariableAsync(DemandeConge.Cle, DemandeConge.NbJours, 2L));

        Assert.Equal([3L, 2L], longues.Select(i => i.CleAffaire));
        Assert.Equal(1L, Assert.Single(exacte).CleAffaire);
    }

    [Fact]
    public async Task Le_suivi_compte_les_instances_actives_et_exporte_le_diagramme()
    {
        _env.Metier.Soldes[1] = 20;
        await DemarrerAsync(nbJours: 2);
        await DemarrerAsync(nbJours: 2, employe: 1);

        var comptes = await _env.LireAsync(s => s.Suivi().CompterInstancesActivesAsync(DemandeConge.Cle));
        var diagramme = await _env.LireAsync(s => s.Suivi().ExporterDiagrammeAsync(DemandeConge.Cle));

        var compte = Assert.Single(comptes);
        Assert.Equal(DemandeConge.NoeudApprobation, compte.CodeNoeud);
        Assert.Equal(2, compte.Nombre);
        Assert.StartsWith("flowchart TD", diagramme);
        Assert.Contains("demande-conge v1", diagramme);
    }
}
