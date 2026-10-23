using Microsoft.Extensions.DependencyInjection;
using Sentier.Commandes;
using Sentier.Definitions;
using Sentier.Tests.Unitaires.Support;
using Sentier.Variables;

namespace Sentier.Tests.Unitaires.Execution;

/// <summary>Exécution des nœuds non suspensifs : début, systémique, décision, fin.</summary>
public class ExecutionNoeudsTests : IDisposable
{
    private static readonly Variable<long> Compteur = Variable.Entier("compteur").ParDefaut(0);
    private static readonly Variable<string> Statut = Variable.Texte("statut");
    private static readonly Variable<long> Obligatoire = Variable.Entier("obligatoire").Obligatoire();

    private readonly HoteMemoire _hote = new();

    public void Dispose() => _hote.Dispose();

    [Fact]
    public async Task Un_processus_lineaire_se_termine_dans_la_meme_transaction()
    {
        _hote.Publier(ConstructeurProcessus.Nouveau("lineaire")
            .Variables(Statut)
            .Systemique("a", "tracer", p => p.Valeur("message", "un"))
            .Systemique("b", "ecrire", p => p.Valeur("variable", "statut").Valeur("valeur", "fait"))
            .Fin("termine")
            .Construire());

        var resultat = await _hote.DemarrerAsync("lineaire", 42);

        Assert.False(resultat.SansEffet);
        Assert.Equal(StatutInstance.Terminee, resultat.Statut);
        Assert.Equal("termine", resultat.CodeFin);
        Assert.Equal("termine", resultat.CodeNoeudCourant);
        Assert.Equal(["a:Execution:un"], _hote.Journal.Entrees);

        var etat = await _hote.InstanceAsync(resultat.IdInstance);
        Assert.Equal(42, etat.CleAffaire);
        Assert.Equal("fait", etat.Variables["statut"]);
        Assert.Equal(1, etat.VersionDefinition);
        Assert.Equal(HorlogeTest.Origine, etat.DateFin);

        var historique = await _hote.Suivi(s => s.ObtenirHistoriqueAsync(resultat.IdInstance!.Value));
        Assert.Equal(["debut", "a", "b", "termine"], historique.Select(p => p.CodeNoeud));
        Assert.Equal([TypeNoeud.Debut, TypeNoeud.Systemique, TypeNoeud.Systemique, TypeNoeud.Fin], historique.Select(p => p.TypeNoeud));
        Assert.All(historique, p => Assert.NotNull(p.DateSortie));
        Assert.Equal("termine", historique[^1].Resultat);
    }

    [Fact]
    public async Task Demarrer_un_processus_inconnu()
        => await Assert.ThrowsAsync<ProcessusIntrouvableException>(() => _hote.DemarrerAsync("inconnu", 1));

    [Fact]
    public async Task Demarrer_exige_une_transaction()
    {
        _hote.Publier(ConstructeurProcessus.Nouveau("p").Fin("fin").Construire());
        using var portee = _hote.CreerPortee();
        await Assert.ThrowsAsync<TransactionRequiseException>(() => portee.ServiceProvider.GetRequiredService<IServiceSentier>().DemarrerAsync("p", 1));
    }

    [Fact]
    public async Task Variables_au_demarrage()
    {
        _hote.Publier(ConstructeurProcessus.Nouveau("p").Variables(Compteur, Obligatoire).Fin("fin").Construire());

        await Assert.ThrowsAsync<VariableInvalideException>(() => _hote.DemarrerAsync("p", 1));
        await Assert.ThrowsAsync<VariableInvalideException>(() => _hote.DemarrerAsync("p", 1, new Dictionary<string, object?> { ["obligatoire"] = 1, ["inconnue"] = 2 }));
        await Assert.ThrowsAsync<VariableInvalideException>(() => _hote.DemarrerAsync("p", 1, new Dictionary<string, object?> { ["obligatoire"] = "un" }));

        var resultat = await _hote.DemarrerAsync("p", 1, ValeursVariables.Avec(Obligatoire, 7L));
        var etat = await _hote.InstanceAsync(resultat.IdInstance);
        Assert.Equal(7L, etat.Variables["obligatoire"]);
        Assert.Equal(0L, etat.Variables["compteur"]);
    }

    [Fact]
    public async Task Une_seule_instance_active_par_cle_d_affaire()
    {
        _hote.Publier(ConstructeurProcessus.Nouveau("unique").AttendreSignal("attendre", "GO").Fin("fin").Construire());

        var premiere = await _hote.DemarrerAsync("unique", 5);
        var seconde = await _hote.DemarrerAsync("unique", 5);
        var autreCle = await _hote.DemarrerAsync("unique", 6);

        Assert.True(seconde.SansEffet);
        Assert.Equal(premiere.IdInstance, seconde.IdInstance);
        Assert.Contains("existe déjà", seconde.Raison);
        Assert.False(autreCle.SansEffet);

        // Une fois la première terminée, un nouveau démarrage est possible.
        await _hote.Sentier(s => s.EnvoyerSignalAsync("GO", "unique", 5));
        Assert.False((await _hote.DemarrerAsync("unique", 5)).SansEffet);
    }

    [Fact]
    public async Task Instances_multiples()
    {
        _hote.Publier(ConstructeurProcessus.Nouveau("multiple").InstancesMultiples().AttendreSignal("attendre", "GO").Fin("fin").Construire());

        var a = await _hote.DemarrerAsync("multiple", 5);
        var b = await _hote.DemarrerAsync("multiple", 5);

        Assert.NotEqual(a.IdInstance, b.IdInstance);
        Assert.Equal(2, (await _hote.Suivi(s => s.ObtenirInstancesParCleAffaireAsync(5, ["multiple"]))).Count);
        Assert.Equal(2, (await _hote.Sentier(s => s.EnvoyerSignalAsync("GO", "multiple", 5))).NombreInstancesReprises);
    }

    [Theory]
    [InlineData(true, false, "branche-a")]
    [InlineData(false, true, "branche-b")]
    [InlineData(false, false, "branche-sinon")]
    [InlineData(true, true, "branche-a")]
    public async Task Decision_prend_la_premiere_branche_vraie(bool a, bool b, string attendue)
    {
        _hote.Publier(ConstructeurProcessus.Nouveau("decision")
            .Decision("choix", d => d
                .Si("vrai", p => p.Valeur("valeur", a)).Nommee("a").Alors(x => x.Fin("branche-a"))
                .Si("vrai", p => p.Valeur("valeur", b)).Nommee("b").Alors(x => x.Fin("branche-b"))
                .Sinon(x => x.Fin("branche-sinon")))
            .Construire());

        var resultat = await _hote.DemarrerAsync("decision", 1);

        Assert.Equal(attendue, resultat.CodeFin);
        var historique = await _hote.Suivi(s => s.ObtenirHistoriqueAsync(resultat.IdInstance!.Value));
        Assert.Equal(attendue == "branche-sinon" ? "sinon" : attendue[^1..], historique.Single(p => p.CodeNoeud == "choix").Resultat);

        // L'évaluation s'arrête à la première branche vraie.
        Assert.Equal(a ? 1 : 2, _hote.Journal.Entrees.Count(e => e.StartsWith("choix:requete")));
    }

    [Fact]
    public async Task SiPas_inverse_le_resultat()
    {
        _hote.Publier(ConstructeurProcessus.Nouveau("inverse")
            .Decision("choix", d => d.SiPas("vrai", p => p.Valeur("valeur", false)).Alors(x => x.Fin("pas-vrai")).Sinon(x => x.Fin("vrai")))
            .Construire());

        Assert.Equal("pas-vrai", (await _hote.DemarrerAsync("inverse", 1)).CodeFin);
    }

    [Fact]
    public async Task Une_boucle_avec_compteur_et_requete_typee()
    {
        _hote.Publier(ConstructeurProcessus.Nouveau("boucle")
            .Variables(Compteur)
            .Systemique("incrementer", "incrementer")
            .Decision("assez", d => d
                .Si<CompteurAtteint>(p => p.Variable(Compteur).Valeur("seuil", 3)).Alors(b => b.Fin("fin"))
                .Sinon("incrementer"))
            .Construire());

        var resultat = await _hote.DemarrerAsync("boucle", 1);

        Assert.Equal("fin", resultat.CodeFin);
        Assert.Equal(3L, (await _hote.InstanceAsync(resultat.IdInstance)).Variables["compteur"]);
    }

    [Fact]
    public async Task Une_boucle_infinie_est_interrompue()
    {
        using var hote = new HoteMemoire(options: o => o.MaxEtapesParExecution = 20);
        hote.Publier(ConstructeurProcessus.Nouveau("infinie")
            .Systemique("tourner", "tracer")
            .Decision("encore", d => d.Si("vrai", p => p.Valeur("valeur", true)).Vers("tourner").Sinon(b => b.Fin("fin")))
            .Construire());

        await Assert.ThrowsAsync<BoucleInfinieException>(() => hote.DemarrerAsync("infinie", 1));
    }

    [Fact]
    public async Task Les_parametres_sont_resolus_et_lies()
    {
        var montant = Variable.Decimal("montant");
        var client = Variable.Texte("client");
        _hote.Publier(ConstructeurProcessus.Nouveau("facturation")
            .Variables(montant, client)
            .Systemique<EmettreFacture>("facturer", p => p
                .CleAffaire("numero")
                .Variable(montant)
                .VariableOptionnelle(client)
                .Valeur("urgente", true))
            .Systemique<EmettreFacture>("facturer-defaut", p => p
                .CleAffaire("numero")
                .Variable(montant)
                .VariableOptionnelle("client", client, "anonyme"))
            .Fin("fin")
            .Construire());

        await _hote.DemarrerAsync("facturation", 77, ValeursVariables.Avec(montant, 12.5m));

        Assert.Equal(["facture 77  12.50 urgente", "facture 77 anonyme 12.50 normale"], _hote.Journal.Entrees);
    }

    [Fact]
    public async Task Une_variable_obligatoire_absente_d_un_parametre_est_une_erreur()
    {
        var montant = Variable.Decimal("montant");
        _hote.Publier(ConstructeurProcessus.Nouveau("p").Variables(montant).Systemique<EmettreFacture>("facturer", p => p.Variable(montant)).Fin("fin").Construire());

        var ex = await Assert.ThrowsAsync<ParametreInvalideException>(() => _hote.DemarrerAsync("p", 1));
        Assert.Contains("'montant'", ex.Message);
    }

    [Fact]
    public async Task Commande_ou_requete_non_enregistree()
    {
        _hote.Publier(ConstructeurProcessus.Nouveau("sans-commande").Systemique("a", "inexistante").Fin("fin").Construire());
        _hote.Publier(ConstructeurProcessus.Nouveau("sans-requete").Decision("d", d => d.Si("inexistante").Sinon()).Fin("fin").Construire());

        await Assert.ThrowsAsync<CommandeIntrouvableException>(() => _hote.DemarrerAsync("sans-commande", 1));
        await Assert.ThrowsAsync<RequeteIntrouvableException>(() => _hote.DemarrerAsync("sans-requete", 1));
    }

    [Fact]
    public async Task Une_requete_ne_recoit_les_variables_qu_en_lecture()
    {
        _hote.Publier(ConstructeurProcessus.Nouveau("lecture").Decision("d", d => d.Si<SondeLectureSeule>().Sinon()).Fin("fin").Construire());
        await _hote.DemarrerAsync("lecture", 1);
        Assert.Equal(["variables:lecture"], _hote.Journal.Entrees);
    }

    [Fact]
    public async Task Les_intercepteurs_encadrent_commandes_et_requetes()
    {
        using var hote = new HoteMemoire(services => services
            .AddScoped<IIntercepteurCommande, IntercepteurTrace>()
            .AddScoped<IIntercepteurRequete, IntercepteurTrace>());
        hote.Publier(ConstructeurProcessus.Nouveau("intercepte")
            .Systemique("a", "tracer")
            .Decision("d", d => d.Si("vrai", p => p.Valeur("valeur", false)).Sinon())
            .Fin("fin")
            .Construire());

        await hote.DemarrerAsync("intercepte", 1);

        Assert.Equal(["avant tracer", "a:Execution", "après tracer", "d:requete:False", "requête vrai = False"], hote.Journal.Entrees);
    }

    [Fact]
    public async Task L_utilisateur_courant_est_transmis_aux_commandes()
    {
        using var hote = new HoteMemoire(services => services.AddScoped<IContexteUtilisateur>(_ => new ContexteUtilisateurTest("alice")));
        hote.Publier(ConstructeurProcessus.Nouveau("audit").Interactif("tache").Fin("fin").Construire());

        var resultat = await hote.DemarrerAsync("audit", 1);
        await hote.Sentier(s => s.CompleterTacheAsync(resultat.IdTacheCourante!.Value));

        Assert.Equal("alice", hote.Bassin.Tache(resultat.IdTacheCourante!.Value).Utilisateur);
    }
}
