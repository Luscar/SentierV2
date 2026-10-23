using Sentier.Definitions;
using Sentier.Tests.Unitaires.Support;

namespace Sentier.Tests.Unitaires.Execution;

/// <summary>Annulation, mise en erreur, reprise et suivi des instances.</summary>
public class AnnulationEtErreursTests : IDisposable
{
    private readonly HoteMemoire _hote = new();

    public AnnulationEtErreursTests()
    {
        _hote.Publier(ConstructeurProcessus.Nouveau("tache").Interactif("traiter").Fin("fin").Construire());
        _hote.Publier(ConstructeurProcessus.Nouveau("instable").Systemique<CommandeInstable>("appeler").Fin("fin").Construire());
        _hote.Publier(ConstructeurProcessus.Nouveau("delai").AttendreDelai("attendre", TimeSpan.FromHours(1)).Systemique<CommandeInstable>("appeler").Fin("fin").Construire());
    }

    public void Dispose() => _hote.Dispose();

    [Fact]
    public async Task Annuler_une_instance_annule_ses_taches_et_ses_attentes()
    {
        var demarrage = await _hote.DemarrerAsync("tache", 1);
        _hote.Horloge.Avancer(TimeSpan.FromMinutes(5));

        var resultat = await _hote.Sentier(s => s.AnnulerAsync(demarrage.IdInstance!.Value, "demande retirée"));

        Assert.Equal(StatutInstance.Annulee, resultat.Statut);
        Assert.Null(resultat.IdTacheCourante);
        var tache = _hote.Bassin.Tache(demarrage.IdTacheCourante!.Value);
        Assert.Equal((StatutTache.Annulee, "demande retirée"), (tache.Statut, tache.MotifAnnulation));

        var etat = await _hote.InstanceAsync(demarrage.IdInstance);
        Assert.Equal(HorlogeTest.Origine.AddMinutes(5), etat.DateFin);
        var passage = (await _hote.Suivi(s => s.ObtenirHistoriqueAsync(demarrage.IdInstance!.Value)))[^1];
        Assert.Equal("ANNULEE : demande retirée", passage.Resultat);

        var completion = await _hote.Sentier(s => s.CompleterTacheAsync(tache.Id));
        Assert.True(completion.SansEffet);
    }

    [Fact]
    public async Task Annuler_une_instance_terminee_ou_inconnue_est_sans_effet()
    {
        var demarrage = await _hote.DemarrerAsync("tache", 1);
        await _hote.Sentier(s => s.CompleterTacheAsync(demarrage.IdTacheCourante!.Value));

        var terminee = await _hote.Sentier(s => s.AnnulerAsync(demarrage.IdInstance!.Value, "trop tard"));
        var inconnue = await _hote.Sentier(s => s.AnnulerAsync(new IdInstance(999), "x"));

        Assert.True(terminee.SansEffet);
        Assert.Contains("déjà terminée", terminee.Raison);
        Assert.True(inconnue.SansEffet);
        Assert.Contains("introuvable", inconnue.Raison);
    }

    [Fact]
    public async Task Une_annulation_deux_fois_est_sans_effet()
    {
        var demarrage = await _hote.DemarrerAsync("tache", 1);
        await _hote.Sentier(s => s.AnnulerAsync(demarrage.IdInstance!.Value, "x"));
        var encore = await _hote.Sentier(s => s.AnnulerAsync(demarrage.IdInstance!.Value, "x"));
        Assert.Contains("déjà annulée", encore.Raison);
    }

    [Fact]
    public async Task Erreur_puis_reprise_d_un_noeud_systemique()
    {
        // Stratégie du batch : la transaction qui échoue est annulée, puis l'erreur est marquée dans une autre transaction.
        var demarrage = await _hote.DemarrerAsync("delai", 1);
        _hote.Interrupteurs.Echouer = true;
        _hote.Horloge.Avancer(TimeSpan.FromHours(1));
        var due = Assert.Single(await _hote.Sentier(s => s.ObtenirEcheancesDuesAsync()));

        await Assert.ThrowsAsync<InvalidOperationException>(() => _hote.Sentier(s => s.TraiterEcheanceAsync(due.IdAttente)));
        var enErreur = await _hote.Sentier(s => s.MarquerEnErreurAsync(demarrage.IdInstance!.Value, "Service indisponible."));

        Assert.Equal(StatutInstance.EnErreur, enErreur.Statut);
        var erreurs = await _hote.Suivi(s => s.ObtenirInstancesEnErreurAsync());
        Assert.Equal("Service indisponible.", Assert.Single(erreurs).MessageErreur);
        Assert.Equal(_hote.Horloge.MaintenantUtc, erreurs[0].DateErreur);

        // Le batch ignore les instances en erreur.
        Assert.Empty(await _hote.Sentier(s => s.ObtenirEcheancesDuesAsync()));

        _hote.Interrupteurs.Echouer = false;
        var reprise = await _hote.Sentier(s => s.ReprendreAsync(demarrage.IdInstance!.Value));

        // L'attente était toujours en place et son échéance est atteinte : elle est traitée tout de suite.
        Assert.Equal(StatutInstance.Terminee, reprise.Statut);
        Assert.Equal(["appeler:instable"], _hote.Journal.Entrees);
        Assert.Null((await _hote.InstanceAsync(demarrage.IdInstance)).MessageErreur);
        Assert.Empty(await _hote.Suivi(s => s.ObtenirInstancesEnErreurAsync()));
    }

    [Fact]
    public async Task Reprendre_une_instance_en_erreur_qui_attend_encore()
    {
        var demarrage = await _hote.DemarrerAsync("tache", 1);
        await _hote.Sentier(s => s.MarquerEnErreurAsync(demarrage.IdInstance!.Value, "incident"));

        var reprise = await _hote.Sentier(s => s.ReprendreAsync(demarrage.IdInstance!.Value));

        Assert.Equal(StatutInstance.Suspendue, reprise.Statut);
        Assert.Equal(demarrage.IdTacheCourante, reprise.IdTacheCourante);
        Assert.Equal(StatutTache.Ouverte, _hote.Bassin.Tache(demarrage.IdTacheCourante!.Value).Statut);
    }

    [Fact]
    public async Task Une_instance_en_erreur_peut_encore_recevoir_sa_tache()
    {
        var demarrage = await _hote.DemarrerAsync("tache", 1);
        await _hote.Sentier(s => s.MarquerEnErreurAsync(demarrage.IdInstance!.Value, "incident"));

        var resultat = await _hote.Sentier(s => s.CompleterTacheAsync(demarrage.IdTacheCourante!.Value));

        Assert.Equal(StatutInstance.Terminee, resultat.Statut);
    }

    [Fact]
    public async Task Reprendre_reexecute_le_noeud_courant_quand_plus_rien_n_est_attendu()
    {
        var demarrage = await _hote.DemarrerAsync("delai", 1);
        await _hote.Sentier(s => s.MarquerEnErreurAsync(demarrage.IdInstance!.Value, "attente perdue"));
        await _hote.Base.Attentes.SupprimerParInstanceAsync(demarrage.IdInstance!.Value, default);
        _hote.Horloge.Avancer(TimeSpan.FromMinutes(30));

        var reprise = await _hote.Sentier(s => s.ReprendreAsync(demarrage.IdInstance!.Value));

        Assert.Equal((StatutInstance.Suspendue, "attendre"), (reprise.Statut, reprise.CodeNoeudCourant));
        var attente = Assert.Single(await _hote.Suivi(s => s.ObtenirAttentesAsync(demarrage.IdInstance!.Value)));
        Assert.Equal(_hote.Horloge.MaintenantUtc.AddHours(1), attente.DateEcheance);
    }

    [Fact]
    public async Task Reprendre_une_instance_qui_n_est_pas_en_erreur_est_sans_effet()
    {
        var demarrage = await _hote.DemarrerAsync("tache", 1);
        var reprise = await _hote.Sentier(s => s.ReprendreAsync(demarrage.IdInstance!.Value));
        Assert.True(reprise.SansEffet);
        Assert.Contains("n'est pas en erreur", reprise.Raison);
    }

    [Fact]
    public async Task Un_echec_annule_tout_le_travail_de_la_transaction()
    {
        var demarrage = await _hote.DemarrerAsync("delai", 1);
        _hote.Interrupteurs.Echouer = true;
        _hote.Horloge.Avancer(TimeSpan.FromHours(2));
        var due = Assert.Single(await _hote.Sentier(s => s.ObtenirEcheancesDuesAsync()));

        await Assert.ThrowsAsync<InvalidOperationException>(() => _hote.Sentier(s => s.TraiterEcheanceAsync(due.IdAttente)));

        // L'attente et l'instance sont restées telles quelles : le batch la retrouvera.
        Assert.Equal(due.IdAttente, Assert.Single(await _hote.Sentier(s => s.ObtenirEcheancesDuesAsync())).IdAttente);
        Assert.Equal(StatutInstance.Suspendue, (await _hote.InstanceAsync(demarrage.IdInstance)).Statut);
        _hote.Interrupteurs.Echouer = false;
        Assert.Equal(StatutInstance.Terminee, (await _hote.Sentier(s => s.TraiterEcheanceAsync(due.IdAttente))).Statut);
    }

    [Fact]
    public async Task Marquer_ou_reprendre_une_instance_inconnue_ou_inactive()
    {
        var demarrage = await _hote.DemarrerAsync("tache", 1);
        await _hote.Sentier(s => s.CompleterTacheAsync(demarrage.IdTacheCourante!.Value));

        Assert.True((await _hote.Sentier(s => s.MarquerEnErreurAsync(new IdInstance(999), "x"))).SansEffet);
        Assert.True((await _hote.Sentier(s => s.MarquerEnErreurAsync(demarrage.IdInstance!.Value, "x"))).SansEffet);
        Assert.True((await _hote.Sentier(s => s.ReprendreAsync(new IdInstance(999)))).SansEffet);
    }

    [Fact]
    public async Task Un_message_d_erreur_trop_long_est_tronque()
    {
        var demarrage = await _hote.DemarrerAsync("tache", 1);
        await _hote.Sentier(s => s.MarquerEnErreurAsync(demarrage.IdInstance!.Value, new string('x', 5000)));
        Assert.Equal(4000, (await _hote.InstanceAsync(demarrage.IdInstance)).MessageErreur!.Length);
    }

    [Fact]
    public async Task Suivi_pagination_comptes_et_diagramme()
    {
        for (var cle = 1; cle <= 3; cle++)
        {
            var d = await _hote.DemarrerAsync("tache", cle);
            _hote.Horloge.Avancer(TimeSpan.FromMinutes(1));
            await _hote.Sentier(s => s.MarquerEnErreurAsync(d.IdInstance!.Value, $"erreur {cle}"));
        }

        var page0 = await _hote.Suivi(s => s.ObtenirInstancesEnErreurAsync(0, 2));
        var page1 = await _hote.Suivi(s => s.ObtenirInstancesEnErreurAsync(1, 2));
        Assert.Equal(["erreur 3", "erreur 2"], page0.Select(i => i.MessageErreur));
        Assert.Equal(["erreur 1"], page1.Select(i => i.MessageErreur));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => _hote.Suivi(s => s.ObtenirInstancesEnErreurAsync(-1)));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => _hote.Suivi(s => s.ObtenirInstancesEnErreurAsync(0, 0)));

        var compte = Assert.Single(await _hote.Suivi(s => s.CompterInstancesActivesAsync("tache")));
        Assert.Equal((1, "traiter", 3), (compte.Version, compte.CodeNoeud, compte.Nombre));

        Assert.Contains("flowchart TD", await _hote.Suivi(s => s.ExporterDiagrammeAsync("tache")));
        Assert.Contains("(tache v1)", await _hote.Suivi(s => s.ExporterDiagrammeAsync("tache", 1)));
        await Assert.ThrowsAsync<ProcessusIntrouvableException>(() => _hote.Suivi(s => s.ExporterDiagrammeAsync("tache", 7)));
        await Assert.ThrowsAsync<ProcessusIntrouvableException>(() => _hote.Suivi(s => s.ExporterDiagrammeAsync("inconnu")));

        Assert.Null(await _hote.Suivi(s => s.ObtenirInstanceAsync(new IdInstance(999))));
        await Assert.ThrowsAsync<ArgumentException>(() => _hote.Suivi(s => s.ObtenirInstancesParCleAffaireAsync(1, [])));
        var filtrees = await _hote.Suivi(s => s.ObtenirInstancesParCleAffaireAsync(1, ["tache"], [StatutInstance.Terminee]));
        Assert.Empty(filtrees);
    }
}
