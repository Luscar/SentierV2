using ProcessManager.Tests.Exemple;
using static ProcessManager.Tests.Exemple.DeclarationOriginale;

namespace ProcessManager.Tests;

public sealed class DeclarationTests
{
    private readonly Contexte _c = new();
    private readonly Guid _aggregat = Guid.NewGuid();

    private Task<Guid> Demarrer() => _c.Moteur.Demarrer<DeclarationOriginale>(p => p.Demarrer(_aggregat));

    private async Task AtteindreComptabilisation(Guid id)
    {
        _c.Horloge.Maintenant = _c.DateComptabilisation;
        Assert.Equal(1, await _c.Moteur.Tick());
    }

    [Fact]
    public async Task Un_rejet_d_identification_ouvre_une_tache_user_avec_une_seule_sauvegarde_et_un_seul_commit()
    {
        _c.IdentificationAcceptee = false;

        var id = await Demarrer();

        var instance = _c.Instance(id);
        Assert.Equal(StatutProcessus.EnAttente, instance.Statut);
        Assert.Equal(EtapeCorrectionIdentification, instance.Etape);
        Assert.Equal(_c.Horloge.Maintenant.AddDays(5), instance.Echeance);
        Assert.Equal(_aggregat.ToString(), instance.CleCorrelation);
        Assert.Equal(_aggregat, _c.EtatDeclaration(id).AggregateId);
        Assert.Equal(1, _c.Store.Sauvegardes);
        Assert.Equal(1, _c.Store.Commits);
        Assert.Equal([new IdentifierDeclaration(_aggregat)], _c.Commandes.Envoyees);
    }

    [Fact]
    public async Task Identification_et_validation_chainees_ne_sauvegardent_qu_a_l_attente_de_comptabilisation()
    {
        var id = await Demarrer();

        var instance = _c.Instance(id);
        Assert.Equal(EtapeAttenteComptabilisation, instance.Etape);
        Assert.Equal(_c.DateComptabilisation, instance.Echeance);
        Assert.Equal(1, _c.Store.Sauvegardes);
        Assert.Equal(1, _c.Store.Commits);
        Assert.Equal([new IdentifierDeclaration(_aggregat), new ValiderDeclaration(_aggregat)], _c.Commandes.Envoyees);
    }

    [Fact]
    public async Task Parcours_nominal_jusqu_a_la_comptabilisation()
    {
        var id = await Demarrer();
        _c.Horloge.Maintenant = _c.DateComptabilisation.AddMinutes(-1);
        Assert.Equal(0, await _c.Moteur.Tick());

        await AtteindreComptabilisation(id);

        Assert.Equal(StatutProcessus.Termine, _c.Instance(id).Statut);
        Assert.Equal("comptabilisée", _c.EtatDeclaration(id).Resultat);
        Assert.Equal(1, _c.Envoyees<ComptabiliserDeclaration>());
        Assert.Equal(2, _c.Store.Commits);
    }

    [Fact]
    public async Task La_correction_d_identification_reprend_l_identification()
    {
        _c.IdentificationAcceptee = false;
        var id = await Demarrer();
        _c.IdentificationAcceptee = true;

        Assert.True(await _c.Moteur.Signaler<DeclarationOriginale>(_aggregat.ToString(), new CorrectionSoumise(), EtapeCorrectionIdentification));

        Assert.Equal(2, _c.Envoyees<IdentifierDeclaration>());
        Assert.Equal(1, _c.Envoyees<ValiderDeclaration>());
        Assert.Equal(EtapeAttenteComptabilisation, _c.Instance(id).Etape);
    }

    [Fact]
    public async Task La_correction_de_validation_ne_reprend_que_la_validation()
    {
        _c.ValidationAcceptee = false;
        var id = await Demarrer();
        Assert.Equal(EtapeCorrectionValidation, _c.Instance(id).Etape);
        _c.ValidationAcceptee = true;

        Assert.True(await _c.Moteur.Signaler(id, new CorrectionSoumise(), EtapeCorrectionValidation));

        Assert.Equal(1, _c.Envoyees<IdentifierDeclaration>());
        Assert.Equal(2, _c.Envoyees<ValiderDeclaration>());
        Assert.Equal(EtapeAttenteComptabilisation, _c.Instance(id).Etape);
    }

    [Fact]
    public async Task Un_signal_destine_a_une_autre_etape_est_ignore()
    {
        _c.IdentificationAcceptee = false;
        var id = await Demarrer();
        var avant = _c.Instance(id);

        Assert.False(await _c.Moteur.Signaler(id, new CorrectionSoumise(), EtapeCorrectionValidation));
        Assert.False(await _c.Moteur.Signaler(id, new RejetAutorise(), EtapeAutorisationRejet));
        Assert.False(await _c.Moteur.Abandonner(id, etape: EtapeAutorisationRejet));

        Assert.Equal(avant, _c.Instance(id));
        Assert.Equal(1, _c.Store.Commits);
    }

    [Fact]
    public async Task Un_rejet_de_comptabilisation_demande_une_autorisation_de_rejet()
    {
        _c.ComptabilisationAcceptee = false;
        var id = await Demarrer();
        await AtteindreComptabilisation(id);

        Assert.Equal(EtapeAutorisationRejet, _c.Instance(id).Etape);
        Assert.Equal(_c.Horloge.Maintenant.AddDays(5), _c.Instance(id).Echeance);
        Assert.Equal(1, _c.Envoyees<DemanderAutorisationRejet>());

        Assert.True(await _c.Moteur.Signaler(id, new RejetAutorise(), EtapeAutorisationRejet));

        Assert.Equal(StatutProcessus.Termine, _c.Instance(id).Statut);
        Assert.Equal("rejetée", _c.EtatDeclaration(id).Resultat);
    }

    [Fact]
    public async Task Un_rejet_refuse_relance_la_comptabilisation()
    {
        _c.ComptabilisationAcceptee = false;
        var id = await Demarrer();
        await AtteindreComptabilisation(id);
        _c.ComptabilisationAcceptee = true;

        Assert.True(await _c.Moteur.Signaler(id, new RejetRefuse(), EtapeAutorisationRejet));

        Assert.Equal(2, _c.Envoyees<ComptabiliserDeclaration>());
        Assert.Equal("comptabilisée", _c.EtatDeclaration(id).Resultat);
    }

    [Fact]
    public async Task Les_relances_restaurent_l_etat_a_chaque_rehydratation_puis_expirent()
    {
        _c.IdentificationAcceptee = false;
        var id = await Demarrer();

        for (var i = 0; i < 3; i++)
        {
            _c.Horloge.Avancer(DelaiTache);
            Assert.Equal(1, await _c.Moteur.Tick());
        }

        Assert.Equal(
            [new Relancer(_aggregat, EtapeCorrectionIdentification, 1), new Relancer(_aggregat, EtapeCorrectionIdentification, 2)],
            _c.Commandes.Envoyees.OfType<Relancer>());
        var etat = _c.EtatDeclaration(id);
        Assert.Equal(_aggregat, etat.AggregateId);
        Assert.Equal(3, etat.Relances);
        Assert.Equal("expirée", etat.Resultat);
        Assert.Equal(StatutProcessus.Termine, _c.Instance(id).Statut);
    }

    [Fact]
    public async Task Les_relances_repartent_de_zero_a_chaque_nouvelle_tache()
    {
        _c.IdentificationAcceptee = false;
        _c.ValidationAcceptee = false;
        var id = await Demarrer();
        _c.Horloge.Avancer(DelaiTache);
        await _c.Moteur.Tick();
        Assert.Equal(1, _c.EtatDeclaration(id).Relances);

        _c.IdentificationAcceptee = true;
        await _c.Moteur.Signaler(id, new CorrectionSoumise());

        Assert.Equal(EtapeCorrectionValidation, _c.Instance(id).Etape);
        Assert.Equal(0, _c.EtatDeclaration(id).Relances);
    }

    [Fact]
    public async Task Un_abandon_reveille_AttenteAbandonnee()
    {
        var id = await Demarrer();

        Assert.True(await _c.Moteur.Abandonner(id, "annulée", EtapeAttenteComptabilisation));

        Assert.Equal(StatutProcessus.Termine, _c.Instance(id).Statut);
        Assert.Equal("abandonnée", _c.EtatDeclaration(id).Resultat);
    }

    [Fact]
    public async Task Apres_un_premier_declencheur_les_suivants_sont_ignores()
    {
        _c.IdentificationAcceptee = false;
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
    public async Task Un_signal_sans_instance_en_attente_ne_fait_rien()
    {
        Assert.False(await _c.Moteur.Signaler<DeclarationOriginale>("inconnue", new CorrectionSoumise()));
        Assert.False(await _c.Moteur.Signaler(Guid.NewGuid(), new CorrectionSoumise()));
        Assert.False(await _c.Moteur.Abandonner(Guid.NewGuid()));
        Assert.Equal(0, _c.Store.Commits);
    }

    [Fact]
    public async Task Tick_ne_reveille_que_les_instances_echues()
    {
        _c.IdentificationAcceptee = false;
        var premiere = await Demarrer();
        _c.Horloge.Avancer(TimeSpan.FromDays(2));
        var seconde = await _c.Moteur.Demarrer<DeclarationOriginale>(p => p.Demarrer(Guid.NewGuid()));
        _c.Horloge.Avancer(TimeSpan.FromDays(3));

        Assert.Equal(1, await _c.Moteur.Tick());

        Assert.Equal(1, _c.EtatDeclaration(premiere).Relances);
        Assert.Equal(0, _c.EtatDeclaration(seconde).Relances);
        Assert.Equal(0, await _c.Moteur.Tick());
    }
}
