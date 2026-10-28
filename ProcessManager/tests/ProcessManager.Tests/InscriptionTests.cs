using System.Text.Json;
using ProcessManager.Tests.Exemple;
using ProcessManager.Tests.Infrastructure;

namespace ProcessManager.Tests;

public sealed class InscriptionTests
{
    private readonly HorlogeTest _horloge = new();
    private readonly StoreMemoire _store = new();
    private readonly BusCommandesTest _commandes = new();
    private readonly MoteurProcessus _moteur;
    private readonly Guid _dossier = Guid.NewGuid();

    public InscriptionTests()
    {
        var fabrique = new FabriqueTest().Enregistrer(() => new Inscription(_commandes));
        _moteur = new MoteurProcessus(_store, _store, fabrique, _horloge);
    }

    private Task<Guid> Demarrer() => _moteur.Demarrer<Inscription>(p => p.Demarrer(_dossier));

    private InstanceProcessus Instance(Guid id) => _store.Lire(id)!;

    private EtatInscription Etat(Guid id) => JsonSerializer.Deserialize<EtatInscription>(Instance(id).Etat!)!;

    [Fact]
    public async Task Enchaine_plusieurs_taches_user_et_attentes_chacune_avec_son_signal()
    {
        var id = await Demarrer();
        Assert.Equal(Inscription.EtapeSaisie, Instance(id).Etape);

        Assert.True(await _moteur.Signaler(id, new DossierSoumis(), Inscription.EtapeSaisie));
        Assert.Equal(Inscription.EtapeCarence, Instance(id).Etape);
        Assert.Equal(_horloge.Maintenant.AddDays(2), Instance(id).Echeance);

        _horloge.Avancer(TimeSpan.FromDays(2));
        Assert.Equal(1, await _moteur.Tick());
        Assert.Equal(Inscription.EtapeApprobation, Instance(id).Etape);
        Assert.Equal(_horloge.Maintenant.AddDays(3), Instance(id).Echeance);
        Assert.Equal([new NotifierApprobateur(_dossier)], _commandes.Envoyees);

        Assert.True(await _moteur.Signaler<Inscription>(_dossier.ToString(), new DemandeApprouvee(), Inscription.EtapeApprobation));
        Assert.Equal(StatutProcessus.Termine, Instance(id).Statut);
        Assert.Equal("approuvée", Etat(id).Resultat);
        Assert.Equal(4, _store.Commits);
    }

    [Fact]
    public async Task Un_signal_peut_ramener_a_une_etape_precedente()
    {
        var id = await Demarrer();
        await _moteur.Signaler(id, new DossierSoumis());
        _horloge.Avancer(TimeSpan.FromDays(2));
        await _moteur.Tick();

        Assert.True(await _moteur.Signaler(id, new DemandeRefusee(), Inscription.EtapeApprobation));

        Assert.Equal(Inscription.EtapeSaisie, Instance(id).Etape);
        Assert.Equal(StatutProcessus.EnAttente, Instance(id).Statut);
        Assert.Equal(1, Etat(id).Refus);
    }

    [Fact]
    public async Task Un_signal_destine_a_une_autre_etape_est_ignore()
    {
        var id = await Demarrer();
        var avant = Instance(id);

        Assert.False(await _moteur.Signaler(id, new DemandeApprouvee(), Inscription.EtapeApprobation));
        Assert.False(await _moteur.Signaler<Inscription>(_dossier.ToString(), new DemandeApprouvee(), Inscription.EtapeApprobation));
        Assert.False(await _moteur.Abandonner(id, etape: Inscription.EtapeApprobation));

        Assert.Equal(avant, Instance(id));
        Assert.Equal(1, _store.Commits);
    }

    [Theory]
    [InlineData(Inscription.EtapeSaisie, "saisie expirée")]
    [InlineData(Inscription.EtapeApprobation, "approbation expirée")]
    public async Task DelaiExpire_est_aiguille_selon_l_etape(string etape, string resultat)
    {
        var id = await Demarrer();
        if (etape == Inscription.EtapeApprobation)
        {
            await _moteur.Signaler(id, new DossierSoumis());
            _horloge.Avancer(TimeSpan.FromDays(2));
            await _moteur.Tick();
        }

        _horloge.Avancer(TimeSpan.FromDays(5));
        Assert.Equal(1, await _moteur.Tick());

        Assert.Equal(resultat, Etat(id).Resultat);
    }

    [Fact]
    public async Task L_abandon_porte_l_etape_abandonnee()
    {
        var id = await Demarrer();
        await _moteur.Signaler(id, new DossierSoumis());

        Assert.True(await _moteur.Abandonner(id, "retrait", Inscription.EtapeCarence));

        Assert.Equal("abandonnée (Carence)", Etat(id).Resultat);
    }
}
