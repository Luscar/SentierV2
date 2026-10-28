using ProcessManager.Tests.Exemple;

namespace ProcessManager.Tests;

public sealed class DeclarationTests
{
    private readonly Contexte _c = new();
    private readonly Guid _aggregat = Guid.NewGuid();

    private Task<Guid> Demarrer() => _c.Moteur.Demarrer<DeclarationOriginale>(p => p.Demarrer(_aggregat));

    [Fact]
    public async Task Un_demarrage_menant_a_une_tache_user_suspend_avec_une_seule_sauvegarde_et_un_seul_commit()
    {
        var id = await Demarrer();

        var instance = _c.Instance(id);
        Assert.Equal(StatutProcessus.EnAttente, instance.Statut);
        Assert.Equal(DeclarationOriginale.EtapeCorrection, instance.Etape);
        Assert.Equal(_c.Horloge.Maintenant.AddDays(5), instance.Echeance);
        Assert.Equal(_aggregat.ToString(), instance.CleCorrelation);
        Assert.Equal(_aggregat, _c.EtatDeclaration(id).AggregateId);
        Assert.Equal(1, _c.Store.Sauvegardes);
        Assert.Equal(1, _c.Store.Commits);
        Assert.Equal([new IdentifierDeclaration(_aggregat)], _c.Commandes.Envoyees);
    }

    [Fact]
    public async Task Les_etapes_systemiques_et_decisions_chainees_ne_sauvegardent_pas()
    {
        _c.IdentificationReussie(true);

        var id = await Demarrer();

        // Demarrer → Identifiee → appel de Signature → tâche user de Signature : une sauvegarde par instance, un commit.
        Assert.Equal(2, _c.Store.Sauvegardes);
        Assert.Equal(1, _c.Store.Commits);
        Assert.Equal(
            [new IdentifierDeclaration(_aggregat), new ValiderDeclaration(_aggregat), new DemanderSignature(_aggregat)],
            _c.Commandes.Envoyees);
        Assert.Equal(StatutProcessus.AttenteEnfant, _c.Instance(id).Statut);
    }

    [Fact]
    public async Task Un_signal_reveille_le_handle_correspondant()
    {
        await Demarrer();
        _c.IdentificationReussie(true);

        Assert.True(await _c.Moteur.Signaler<DeclarationOriginale>(_aggregat.ToString(), new CorrectionSoumise()));

        Assert.Equal(2, _c.Commandes.Envoyees.OfType<IdentifierDeclaration>().Count());
        Assert.Contains(new ValiderDeclaration(_aggregat), _c.Commandes.Envoyees);
        Assert.Equal(2, _c.Store.Commits);
    }

    [Fact]
    public async Task Un_signal_sans_instance_en_attente_ne_fait_rien()
    {
        Assert.False(await _c.Moteur.Signaler<DeclarationOriginale>("inconnue", new CorrectionSoumise()));
        Assert.False(await _c.Moteur.Signaler(Guid.NewGuid(), new CorrectionSoumise()));
        Assert.False(await _c.Moteur.Abandonner(Guid.NewGuid()));
        Assert.Equal(0, _c.Store.Commits);
    }

    [Fact]
    public async Task Une_fin_de_delai_reveille_DelaiExpire_et_relance()
    {
        var id = await Demarrer();
        _c.Horloge.Avancer(TimeSpan.FromDays(5));

        Assert.Equal(1, await _c.Moteur.Tick());

        var instance = _c.Instance(id);
        Assert.Equal(StatutProcessus.EnAttente, instance.Statut);
        Assert.Equal(_c.Horloge.Maintenant.AddDays(5), instance.Echeance);
        Assert.Equal(1, _c.EtatDeclaration(id).Relances);
        Assert.Contains(new RelancerDeclarant(_aggregat, 1), _c.Commandes.Envoyees);
    }

    [Fact]
    public async Task L_etat_est_restaure_a_chaque_rehydratation()
    {
        var id = await Demarrer();

        for (var i = 0; i < 3; i++)
        {
            _c.Horloge.Avancer(TimeSpan.FromDays(5));
            await _c.Moteur.Tick();
        }

        Assert.Equal(
            [new RelancerDeclarant(_aggregat, 1), new RelancerDeclarant(_aggregat, 2)],
            _c.Commandes.Envoyees.OfType<RelancerDeclarant>());
        var etat = _c.EtatDeclaration(id);
        Assert.Equal(_aggregat, etat.AggregateId);
        Assert.Equal(3, etat.Relances);
        Assert.Equal("expirée", etat.Resultat);
        Assert.Equal(StatutProcessus.Termine, _c.Instance(id).Statut);
    }

    [Fact]
    public async Task Un_abandon_reveille_AttenteAbandonnee()
    {
        var id = await Demarrer();

        Assert.True(await _c.Moteur.Abandonner(id, "annulée par le déclarant"));

        Assert.Equal(StatutProcessus.Termine, _c.Instance(id).Statut);
        Assert.Equal("abandonnée", _c.EtatDeclaration(id).Resultat);
    }

    [Fact]
    public async Task Apres_un_premier_declencheur_les_suivants_sont_ignores()
    {
        var id = await Demarrer();
        await _c.Moteur.Abandonner(id);
        var apres = _c.Instance(id);
        _c.Horloge.Avancer(TimeSpan.FromDays(30));

        Assert.False(await _c.Moteur.Signaler(id, new CorrectionSoumise()));
        Assert.False(await _c.Moteur.Abandonner(id));
        Assert.Equal(0, await _c.Moteur.Tick());

        Assert.Equal(apres, _c.Instance(id));
        Assert.Equal(2, _c.Store.Commits);
    }

    [Fact]
    public async Task Tick_ne_reveille_que_les_instances_echues()
    {
        var premiere = await Demarrer();
        _c.Horloge.Avancer(TimeSpan.FromDays(2));
        var seconde = await _c.Moteur.Demarrer<DeclarationOriginale>(p => p.Demarrer(Guid.NewGuid()));
        _c.Horloge.Avancer(TimeSpan.FromDays(3));

        Assert.Equal(1, await _c.Moteur.Tick());

        Assert.Equal(1, _c.EtatDeclaration(premiere).Relances);
        Assert.Equal(0, _c.EtatDeclaration(seconde).Relances);
        Assert.Equal(0, await _c.Moteur.Tick());
    }

    [Theory]
    [InlineData("signal", "signée")]
    [InlineData("delai", "non-signée")]
    [InlineData("abandon", "abandonnée")]
    public async Task La_fin_du_sous_processus_reprend_le_parent_dans_la_meme_transaction(string declencheur, string resultat)
    {
        _c.IdentificationReussie(true);
        var id = await Demarrer();
        var enfant = _c.Enfant(id);
        Assert.Equal(StatutProcessus.AttenteEnfant, _c.Instance(id).Statut);
        Assert.Equal(DeclarationOriginale.EtapeSignature, _c.Instance(id).Etape);
        Assert.Equal(StatutProcessus.EnAttente, enfant.Statut);
        Assert.Equal(_c.Horloge.Maintenant.AddDays(10), enfant.Echeance);
        var commits = _c.Store.Commits;

        switch (declencheur)
        {
            case "signal":
                await _c.Moteur.Signaler<Signature>(_aggregat.ToString(), new SignatureRecue());
                break;
            case "delai":
                _c.Horloge.Avancer(TimeSpan.FromDays(10));
                Assert.Equal(1, await _c.Moteur.Tick());
                break;
            default:
                await _c.Moteur.Abandonner(enfant.Id);
                break;
        }

        Assert.Equal(commits + 1, _c.Store.Commits);
        Assert.Equal(StatutProcessus.Termine, _c.Instance(enfant.Id).Statut);
        Assert.Equal(StatutProcessus.Termine, _c.Instance(id).Statut);
        Assert.Equal(resultat, _c.EtatDeclaration(id).Resultat);
    }

    [Fact]
    public async Task Un_sous_processus_peut_etre_demarre_seul()
    {
        var id = await _c.Moteur.Demarrer<Signature>(s => s.Demarrer(_aggregat));

        var instance = _c.Instance(id);
        Assert.Equal(StatutProcessus.EnAttente, instance.Statut);
        Assert.Equal(Signature.EtapeSignature, instance.Etape);
        Assert.Null(instance.ParentId);

        Assert.True(await _c.Moteur.Signaler(id, new SignatureRecue()));
        Assert.Equal(StatutProcessus.Termine, _c.Instance(id).Statut);
    }
}
