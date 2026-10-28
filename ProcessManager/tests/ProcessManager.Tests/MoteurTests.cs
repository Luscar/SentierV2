using ProcessManager.Tests.Infrastructure;

namespace ProcessManager.Tests;

public sealed class MoteurTests
{
    private readonly HorlogeTest _horloge = new();
    private readonly StoreMemoire _store = new();
    private readonly FabriqueTest _fabrique = new();
    private readonly MoteurProcessus _moteur;

    public MoteurTests()
    {
        _fabrique
            .Enregistrer(() => new Chaine())
            .Enregistrer(() => new RaiseApresSuspension())
            .Enregistrer(() => new SansHandle())
            .Enregistrer(() => new SansSuite())
            .Enregistrer(() => new DoubleSuite())
            .Enregistrer(() => new EnErreur());
        _moteur = new MoteurProcessus(_store, _store, _fabrique, _horloge);
    }

    [Fact]
    public async Task Une_chaine_d_evenements_internes_ne_sauvegarde_qu_a_la_fin()
    {
        var id = await _moteur.Demarrer<Chaine>(p => p.Demarrer());

        Assert.Equal(1, _store.Sauvegardes);
        Assert.Equal(1, _store.Commits);
        Assert.Equal(StatutProcessus.Termine, _store.Lire(id)!.Statut);
        Assert.Equal("""{"Trace":"ABC"}""", _store.Lire(id)!.Etat);
    }

    [Fact]
    public async Task Attente_a_date_fixe()
    {
        var id = await _moteur.Demarrer<Chaine>(p => p.AttendreJusquA(new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero)));

        Assert.Equal(new DateTimeOffset(2026, 3, 1, 0, 0, 0, TimeSpan.Zero), _store.Lire(id)!.Echeance);
    }

    [Fact]
    public async Task Un_DelaiExpire_dont_l_echeance_n_est_plus_depassee_est_ignore()
    {
        var id = await _moteur.Demarrer<Chaine>(p => p.AttendreJusquA(_horloge.Maintenant.AddDays(1)));
        var perimee = _store.Lire(id)! with { Echeance = _horloge.Maintenant };
        var moteur = new MoteurProcessus(new StoreListeFigee(_store, perimee), _store, _fabrique, _horloge);

        Assert.Equal(0, await moteur.Tick());
        Assert.Equal(StatutProcessus.EnAttente, _store.Lire(id)!.Statut);
    }

    [Fact]
    public async Task Un_evenement_leve_apres_une_suspension_est_une_erreur()
    {
        var erreur = await Assert.ThrowsAsync<ProcessusException>(() => _moteur.Demarrer<RaiseApresSuspension>(p => p.Demarrer()));

        Assert.Contains("RaiseApresSuspension", erreur.Message);
        Assert.Contains("après un point de suspension", erreur.Message);
        Assert.Equal(0, _store.Commits);
    }

    [Fact]
    public async Task Un_evenement_sans_handle_est_une_erreur_explicite()
    {
        var erreur = await Assert.ThrowsAsync<ProcessusException>(() => _moteur.Demarrer<SansHandle>(p => p.Demarrer()));

        Assert.Equal("Le processus 'SansHandle' n'a pas de méthode Handle(EvenementOrphelin).", erreur.Message);
    }

    [Fact]
    public async Task Une_execution_sans_suspension_ni_fin_est_une_erreur()
    {
        var erreur = await Assert.ThrowsAsync<ProcessusException>(() => _moteur.Demarrer<SansSuite>(p => p.Demarrer()));

        Assert.Contains("'SansSuite'", erreur.Message);
        Assert.Contains("sans suspension ni fin", erreur.Message);
        Assert.Equal(0, _store.Sauvegardes);
    }

    [Fact]
    public async Task Deux_suites_dans_la_meme_execution_sont_une_erreur()
    {
        var erreur = await Assert.ThrowsAsync<ProcessusException>(() => _moteur.Demarrer<DoubleSuite>(p => p.Demarrer()));

        Assert.Contains("déjà déclaré", erreur.Message);
    }

    [Fact]
    public async Task Les_exceptions_des_handlers_remontent_telles_quelles_sans_commit()
    {
        var id = await _moteur.Demarrer<EnErreur>(p => p.Demarrer());
        var avant = _store.Lire(id);

        await Assert.ThrowsAsync<ErreurMetier>(() => _moteur.Signaler(id, new Echec()));

        Assert.Equal(1, _store.Commits);
        Assert.Equal(avant, _store.Lire(id));
    }

    [Fact]
    public void La_couche_ne_reference_aucun_type_applicatif()
    {
        var references = typeof(MoteurProcessus).Assembly.GetReferencedAssemblies().Select(a => a.Name!);

        Assert.All(references, nom => Assert.True(nom == "System" || nom.StartsWith("System.") || nom == "netstandard", nom));
    }

    // Processus de test.

    private sealed record A;
    private sealed record B;
    private sealed record C;
    private sealed record EvenementOrphelin;
    private sealed record Echec;
    private sealed class ErreurMetier : Exception;

    private sealed class EtatChaine
    {
        public string Trace { get; set; } = "";
    }

    private sealed class Chaine : Processus<EtatChaine>
    {
        public Task Demarrer()
        {
            Raise(new A());
            return Task.CompletedTask;
        }

        public Task AttendreJusquA(DateTimeOffset date)
        {
            Attendre("date", date);
            return Task.CompletedTask;
        }

        private void Handle(A _) { Etat.Trace += "A"; Raise(new B()); }
        private async ValueTask Handle(B _) { await Task.Yield(); Etat.Trace += "B"; Raise(new C()); }
        private void Handle(C _) { Etat.Trace += "C"; Terminer(); }
        private void Handle(DelaiExpire _) => Terminer();
    }

    private sealed class RaiseApresSuspension : ProcessusBase
    {
        public Task Demarrer()
        {
            Attendre("x");
            Raise(new A());
            return Task.CompletedTask;
        }
    }

    private sealed class SansHandle : ProcessusBase
    {
        public Task Demarrer()
        {
            Raise(new EvenementOrphelin());
            return Task.CompletedTask;
        }
    }

    private sealed class SansSuite : ProcessusBase
    {
        public Task Demarrer() => Task.CompletedTask;
    }

    private sealed class DoubleSuite : ProcessusBase
    {
        public Task Demarrer()
        {
            Attendre("x");
            Terminer();
            return Task.CompletedTask;
        }
    }

    private sealed class EnErreur : ProcessusBase
    {
        public Task Demarrer()
        {
            Attendre("x");
            return Task.CompletedTask;
        }

        private void Handle(Echec _)
        {
            Terminer();
            throw new ErreurMetier();
        }
    }

    /// <summary>Simule une liste d'échues obtenue avant une modification de l'échéance.</summary>
    private sealed class StoreListeFigee(StoreMemoire store, InstanceProcessus figee) : IStoreInstances
    {
        public Task<InstanceProcessus?> Charger(Guid id, CancellationToken ct = default) => store.Charger(id, ct);
        public Task Sauvegarder(InstanceProcessus instance, CancellationToken ct = default) => store.Sauvegarder(instance, ct);
        public Task<IReadOnlyList<InstanceProcessus>> ListerEchues(DateTimeOffset maintenant, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<InstanceProcessus>>([figee]);
        public Task<InstanceProcessus?> TrouverEnAttente(string typeProcessus, string cle, CancellationToken ct = default) =>
            store.TrouverEnAttente(typeProcessus, cle, ct);
    }
}
