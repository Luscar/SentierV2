using Sentier.Definitions;
using Sentier.Tests.Unitaires.Support;
using Sentier.Variables;

namespace Sentier.Tests.Unitaires.Execution;

/// <summary>Sous-processus : démarrage de l'enfant, reprise du parent, mappages, fins et annulations.</summary>
public class SousProcessusTests : IDisposable
{
    private static readonly Variable<long> Montant = Variable.Entier("montant");
    private static readonly Variable<string> Verdict = Variable.Texte("verdict");
    private static readonly Variable<long> Compte = Variable.Entier("compte");
    private static readonly Variable<string> Resultat = Variable.Texte("resultat");

    private readonly HoteMemoire _hote = new();

    public SousProcessusTests()
    {
        // Enfant : attend une décision par signal, puis se termine sur « accepte » ou « refuse ».
        _hote.Publier(ConstructeurProcessus.Nouveau("controle")
            .Variables(Montant, Verdict)
            .AttendreSignal("attendre-verdict", "VERDICT")
            .Decision("est-accepte", d => d
                .Si("vrai", p => p.Valeur("valeur", true)).Alors(b => b.Fin("accepte"))
                .Sinon(b => b.Fin("refuse")))
            .Construire());

        // Enfant immédiat : se termine dans la même exécution que son parent.
        _hote.Publier(ConstructeurProcessus.Nouveau("immediat")
            .Variables(Montant, Verdict)
            .Systemique("calculer", "ecrire", p => p.Valeur("variable", "verdict").Valeur("valeur", "calcule"))
            .Fin("calcule")
            .Construire());
    }

    public void Dispose() => _hote.Dispose();

    [Fact]
    public async Task Le_parent_attend_l_enfant_puis_reprend_avec_le_mappage_de_sortie()
    {
        _hote.Publier(ConstructeurProcessus.Nouveau("dossier")
            .Variables(Montant, Resultat)
            .SousProcessus("controler", "controle", s => s.Entree("montant", "montant").Sortie("verdict", "resultat"))
            .Systemique("apres", "tracer")
            .Fin("termine")
            .Construire());

        var parent = await _hote.DemarrerAsync("dossier", 8, ValeursVariables.Avec(Montant, 150L));
        Assert.Equal((StatutInstance.Suspendue, "controler"), (parent.Statut, parent.CodeNoeudCourant));

        var instances = await _hote.Suivi(s => s.ObtenirInstancesParCleAffaireAsync(8, ["dossier", "controle"]));
        var enfant = instances.Single(i => i.CleProcessus == "controle");
        Assert.Equal(parent.IdInstance, enfant.IdInstanceParent);
        Assert.Equal(150L, enfant.Variables["montant"]);
        Assert.Equal(8, enfant.CleAffaire);

        await _hote.Sentier(s => s.EnvoyerSignalAsync("VERDICT", "controle", 8, new Dictionary<string, object?> { ["verdict"] = "conforme" }));

        var final = await _hote.InstanceAsync(parent.IdInstance);
        Assert.Equal(("termine", StatutInstance.Terminee), (final.CodeFin, final.Statut));
        Assert.Equal("conforme", final.Variables["resultat"]);
        Assert.Equal("accepte", final.Variables[VariablesProcessus.VariableFinEnfant]);
        Assert.Equal(["est-accepte:requete:True", "apres:Execution"], _hote.Journal.Entrees);

        var passage = (await _hote.Suivi(s => s.ObtenirHistoriqueAsync(parent.IdInstance!.Value))).Single(p => p.CodeNoeud == "controler");
        Assert.Contains("accepte", passage.Resultat);
    }

    [Fact]
    public async Task Un_enfant_qui_termine_immediatement_ne_suspend_pas_le_parent()
    {
        _hote.Publier(ConstructeurProcessus.Nouveau("parent-immediat")
            .Variables(Montant, Resultat)
            .SousProcessus("calculer", "immediat", s => s.EntreeValeur(Montant, 5L).Sortie("verdict", "resultat"))
            .Fin("fin")
            .Construire());

        var parent = await _hote.DemarrerAsync("parent-immediat", 1);

        Assert.Equal(StatutInstance.Terminee, parent.Statut);
        Assert.Equal("calcule", (await _hote.InstanceAsync(parent.IdInstance)).Variables["resultat"]);
    }

    [Fact]
    public async Task Branches_par_code_de_fin_de_l_enfant()
    {
        _hote.Publier(ConstructeurProcessus.Nouveau("aiguillage")
            .SousProcessus("controler", "controle", s => s.SiFin("refuse", b => b.Fin("rejete")))
            .Fin("valide")
            .Construire());

        var parent = await _hote.DemarrerAsync("aiguillage", 3);
        await _hote.Sentier(s => s.EnvoyerSignalAsync("VERDICT", "controle", 3));

        Assert.Equal("valide", (await _hote.InstanceAsync(parent.IdInstance)).CodeFin);
    }

    [Fact]
    public async Task Cle_d_affaire_de_l_enfant_tiree_d_une_variable()
    {
        _hote.Publier(ConstructeurProcessus.Nouveau("par-compte")
            .Variables(Compte)
            .SousProcessus("controler", "controle", s => s.CleAffaire(Compte).EntreeCleAffaire(Montant))
            .Fin("fin")
            .Construire());

        await _hote.DemarrerAsync("par-compte", 1, ValeursVariables.Avec(Compte, 9000L));
        var enfant = Assert.Single(await _hote.Suivi(s => s.ObtenirInstancesParCleAffaireAsync(9000, ["controle"])));
        Assert.Equal(1L, enfant.Variables["montant"]);

        await Assert.ThrowsAsync<VariableInvalideException>(() => _hote.DemarrerAsync("par-compte", 2));
    }

    [Fact]
    public async Task Un_enfant_unique_deja_actif_empeche_le_demarrage()
    {
        _hote.Publier(ConstructeurProcessus.Nouveau("parent").SousProcessus("controler", "controle").Fin("fin").Construire());
        await _hote.DemarrerAsync("controle", 4);

        var ex = await Assert.ThrowsAsync<SentierException>(() => _hote.DemarrerAsync("parent", 4));
        Assert.Contains("existe déjà", ex.Message);
    }

    [Fact]
    public async Task Un_processus_enfant_absent_ou_incompatible()
    {
        _hote.Publier(ConstructeurProcessus.Nouveau("orphelin").SousProcessus("enfant", "inexistant").Fin("fin").Construire());
        _hote.Publier(ConstructeurProcessus.Nouveau("incompatible").Variables(Resultat).SousProcessus("enfant", "controle", s => s.Entree("montant", "resultat")).Fin("fin").Construire());

        await Assert.ThrowsAsync<ProcessusIntrouvableException>(() => _hote.DemarrerAsync("orphelin", 1));
        await Assert.ThrowsAsync<DefinitionInvalideException>(() => _hote.DemarrerAsync("incompatible", 1));
    }

    [Fact]
    public async Task L_annulation_de_l_enfant_annule_le_parent_sans_branche_dediee()
    {
        _hote.Publier(ConstructeurProcessus.Nouveau("parent").SousProcessus("controler", "controle").Fin("fin").Construire());
        var parent = await _hote.DemarrerAsync("parent", 6);
        var enfant = (await _hote.Suivi(s => s.ObtenirInstancesParCleAffaireAsync(6, ["controle"]))).Single();

        await _hote.Sentier(s => s.AnnulerAsync(enfant.Id, "abandon"));

        var etat = await _hote.InstanceAsync(parent.IdInstance);
        Assert.Equal(StatutInstance.Annulee, etat.Statut);
        var passage = (await _hote.Suivi(s => s.ObtenirHistoriqueAsync(parent.IdInstance!.Value)))[^1];
        Assert.Contains("ANNULEE", passage.Resultat);
    }

    [Fact]
    public async Task L_annulation_de_l_enfant_suit_la_branche_annule()
    {
        _hote.Publier(ConstructeurProcessus.Nouveau("parent")
            .SousProcessus("controler", "controle", s => s.SiAnnule(b => b.Systemique("compenser", "tracer").Vers("abandonne")))
            .Fin("fin")
            .Fin("abandonne")
            .Construire());
        var parent = await _hote.DemarrerAsync("parent", 6);
        var enfant = (await _hote.Suivi(s => s.ObtenirInstancesParCleAffaireAsync(6, ["controle"]))).Single();

        await _hote.Sentier(s => s.AnnulerAsync(enfant.Id, "abandon"));

        Assert.Equal("abandonne", (await _hote.InstanceAsync(parent.IdInstance)).CodeFin);
        Assert.Equal(["compenser:Execution"], _hote.Journal.Entrees);
    }

    [Fact]
    public async Task Annuler_le_parent_annule_ses_enfants_en_cascade()
    {
        _hote.Publier(ConstructeurProcessus.Nouveau("parent").SousProcessus("controler", "controle").Fin("fin").Construire());
        var parent = await _hote.DemarrerAsync("parent", 6);

        var resultat = await _hote.Sentier(s => s.AnnulerAsync(parent.IdInstance!.Value, "dossier retiré"));

        Assert.Equal(StatutInstance.Annulee, resultat.Statut);
        var instances = await _hote.Suivi(s => s.ObtenirInstancesParCleAffaireAsync(6, ["parent", "controle"]));
        Assert.All(instances, i => Assert.Equal(StatutInstance.Annulee, i.Statut));
        Assert.Empty(await _hote.Suivi(s => s.ObtenirAttentesAsync(instances.Single(i => i.CleProcessus == "controle").Id)));
    }
}
