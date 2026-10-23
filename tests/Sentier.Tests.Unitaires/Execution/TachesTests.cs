using Sentier.Definitions;
using Sentier.Tests.Unitaires.Support;
using Sentier.Variables;

namespace Sentier.Tests.Unitaires.Execution;

/// <summary>Nœuds interactifs et délégation au gestionnaire de tâches du client.</summary>
public class TachesTests : IDisposable
{
    private static readonly Variable<long> NbJours = Variable.Entier("nbJours");
    private static readonly Variable<string> Decision = Variable.Texte("decision");
    private static readonly Variable<string> Responsable = Variable.Texte("responsable");
    private static readonly Variable<DateTime> Limite = Variable.Date("limite");

    private readonly HoteMemoire _hote = new();

    public TachesTests()
    {
        _hote.Publier(ConstructeurProcessus.Nouveau("approbation")
            .Variables(NbJours, Decision, Responsable)
            .Interactif("approuver", "Approuver la demande", i => i
                .Role("gestionnaire")
                .Assigne(Responsable)
                .Echeance(TimeSpan.FromDays(3))
                .Donnees(NbJours)
                .Donnees(p => p.CleAffaire("employe").Valeur("source", "portail"))
                .CommandeEntree("tracer", p => p.Valeur("message", "entree"))
                .CommandeSortie("tracer", p => p.Valeur("message", "sortie")))
            .Fin("traitee")
            .Construire());
    }

    public void Dispose() => _hote.Dispose();

    private Task<ResultatExecution> Demarrer() => _hote.DemarrerAsync("approbation", 33, ValeursVariables.Avec(NbJours, 4L).Et(Responsable, "bob"));

    [Fact]
    public async Task L_entree_cree_la_tache_chez_le_client()
    {
        var resultat = await Demarrer();

        Assert.Equal(StatutInstance.Suspendue, resultat.Statut);
        var tache = _hote.Bassin.Tache(resultat.IdTacheCourante!.Value);
        var demande = tache.Demande;
        Assert.Equal((resultat.IdInstance!.Value, "approbation", 33L, "approuver"), (demande.IdInstance, demande.CleProcessus, demande.CleAffaire, demande.CodeNoeud));
        Assert.Equal(("Approuver la demande", "gestionnaire", "bob"), (demande.Nom, demande.Role, demande.Utilisateur));
        Assert.Equal(HorlogeTest.Origine.AddDays(3), demande.DateEcheance);
        Assert.Equal(4L, demande.Donnees["nbJours"]);
        Assert.Equal(33L, demande.Donnees["employe"]);
        Assert.Equal("portail", demande.Donnees["source"]);
        Assert.Equal(["approuver:Entree:entree"], _hote.Journal.Entrees);

        var etat = await _hote.InstanceAsync(resultat.IdInstance);
        Assert.Equal(tache.Id, etat.IdTacheCourante);
        Assert.Single(await _hote.Suivi(s => s.ObtenirTachesOuvertesAsync(resultat.IdInstance!.Value)));
    }

    [Fact]
    public async Task Completer_fusionne_les_donnees_et_execute_la_sortie()
    {
        var demarrage = await Demarrer();
        var idTache = demarrage.IdTacheCourante!.Value;

        var resultat = await _hote.Sentier(s => s.CompleterTacheAsync(idTache, new Dictionary<string, object?> { ["decision"] = "approuvee", ["autre"] = 1 }, "carole"));

        Assert.Equal(("traitee", StatutInstance.Terminee), (resultat.CodeFin, resultat.Statut));
        Assert.Null(resultat.IdTacheCourante);
        Assert.Equal(["approuver:Entree:entree", "approuver:Sortie:sortie"], _hote.Journal.Entrees);

        var tache = _hote.Bassin.Tache(idTache);
        Assert.Equal((StatutTache.Completee, "carole", 1), (tache.Statut, tache.Utilisateur, tache.NombreCompletions));
        Assert.Equal("approuvee", tache.DonneesCompletion!["decision"]);

        var etat = await _hote.InstanceAsync(demarrage.IdInstance);
        Assert.Equal("approuvee", etat.Variables["decision"]);
        Assert.False(etat.Variables.ContainsKey("autre"));
        var passage = (await _hote.Suivi(s => s.ObtenirHistoriqueAsync(demarrage.IdInstance!.Value))).Single(p => p.CodeNoeud == "approuver");
        Assert.Equal("tâche complétée par carole", passage.Resultat);
    }

    [Fact]
    public async Task Une_donnee_de_mauvais_type_fait_echouer_la_completion()
    {
        var demarrage = await Demarrer();
        await Assert.ThrowsAsync<VariableInvalideException>(() => _hote.Sentier(s => s.CompleterTacheAsync(demarrage.IdTacheCourante!.Value, new Dictionary<string, object?> { ["nbJours"] = "beaucoup" })));
    }

    [Fact]
    public async Task Completer_deux_fois_est_sans_effet()
    {
        var demarrage = await Demarrer();
        var idTache = demarrage.IdTacheCourante!.Value;
        await _hote.Sentier(s => s.CompleterTacheAsync(idTache));

        var encore = await _hote.Sentier(s => s.CompleterTacheAsync(idTache));

        Assert.True(encore.SansEffet);
        Assert.Contains("déjà complétée", encore.Raison);
        Assert.Equal(1, _hote.Bassin.Tache(idTache).NombreCompletions);
    }

    [Fact]
    public async Task Une_tache_inconnue_est_sans_effet()
    {
        var resultat = await _hote.Sentier(s => s.CompleterTacheAsync(999));
        Assert.True(resultat.SansEffet);
        Assert.Contains("999", resultat.Raison);
    }

    [Fact]
    public async Task Une_tache_annulee_chez_le_client_ne_reprend_pas_l_instance()
    {
        var demarrage = await Demarrer();
        var idTache = demarrage.IdTacheCourante!.Value;
        _hote.Bassin.Tache(idTache).Statut = StatutTache.Annulee;

        var resultat = await _hote.Sentier(s => s.CompleterTacheAsync(idTache));

        Assert.True(resultat.SansEffet);
        Assert.Contains("l'instance l'attend toujours", resultat.Raison);
        Assert.Equal(StatutInstance.Suspendue, (await _hote.InstanceAsync(demarrage.IdInstance)).Statut);
    }

    [Fact]
    public async Task Une_tache_marquee_completee_chez_le_client_peut_etre_completee_de_nouveau()
    {
        // La transaction de Sentier a été annulée après la complétion chez le client : l'instance attend encore la tâche.
        var demarrage = await Demarrer();
        var idTache = demarrage.IdTacheCourante!.Value;
        _hote.Bassin.Tache(idTache).Statut = StatutTache.Completee;

        var resultat = await _hote.Sentier(s => s.CompleterTacheAsync(idTache));

        Assert.False(resultat.SansEffet);
        Assert.Equal("traitee", resultat.CodeFin);
    }

    [Fact]
    public async Task Une_tache_orpheline_ne_reprend_pas_l_instance_et_est_annulee_a_la_sortie()
    {
        var demarrage = await Demarrer();

        // Tâche créée chez le client par une transaction de Sentier annulée : l'instance ne la connaît pas.
        var orpheline = await _hote.ExecuterAsync(_ => new GestionnaireTachesTest(_hote.Bassin).CreerTacheAsync(new DemandeTacheUtilisateur
        {
            IdInstance = demarrage.IdInstance!.Value,
            CleProcessus = "approbation",
            CleAffaire = 33,
            CodeNoeud = "approuver",
            Nom = "orpheline",
        }));

        var refus = await _hote.Sentier(s => s.CompleterTacheAsync(orpheline));
        Assert.True(refus.SansEffet);
        Assert.Contains("n'attend pas la tâche", refus.Raison);

        await _hote.Sentier(s => s.CompleterTacheAsync(demarrage.IdTacheCourante!.Value));
        Assert.Equal(StatutTache.Annulee, _hote.Bassin.Tache(orpheline).Statut);
        Assert.Contains("orpheline", _hote.Bassin.Tache(orpheline).MotifAnnulation);
    }

    [Fact]
    public async Task Echeance_et_assigne_tires_de_variables()
    {
        _hote.Publier(ConstructeurProcessus.Nouveau("variables")
            .Variables(Limite, Responsable)
            .Interactif("tache", i => i.Echeance(Limite).Assigne(Responsable))
            .Fin("fin")
            .Construire());

        var sansValeurs = await _hote.DemarrerAsync("variables", 1);
        var demande = _hote.Bassin.Tache(sansValeurs.IdTacheCourante!.Value).Demande;
        Assert.Null(demande.DateEcheance);
        Assert.Null(demande.Utilisateur);
        Assert.Equal("tache", demande.Nom);

        var date = new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc);
        var avecValeurs = await _hote.DemarrerAsync("variables", 2, ValeursVariables.Avec(Limite, date).Et(Responsable, "dora"));
        demande = _hote.Bassin.Tache(avecValeurs.IdTacheCourante!.Value).Demande;
        Assert.Equal((date, "dora"), (demande.DateEcheance, demande.Utilisateur));
    }

    [Fact]
    public async Task Un_noeud_interactif_sans_gestionnaire_echoue()
    {
        using var hote = new HoteMemoire(options: o => o.UtiliserGestionnaireTaches<AutreGestionnaireTaches>());
        hote.Publier(ConstructeurProcessus.Nouveau("p").Interactif("tache").Fin("fin").Construire());
        await Assert.ThrowsAsync<NotSupportedException>(() => hote.DemarrerAsync("p", 1));
    }
}
