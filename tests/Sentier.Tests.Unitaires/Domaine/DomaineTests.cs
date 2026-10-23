using System.Text.Json;

namespace Sentier.Tests.Unitaires.Domaine;

public class IdentifiantsTests
{
    [Fact]
    public void Un_identifiant_se_serialise_comme_un_nombre()
    {
        Assert.Equal("42", JsonSerializer.Serialize(new IdInstance(42)));
        Assert.Equal(new IdInstance(42), JsonSerializer.Deserialize<IdInstance>("42"));
        Assert.Equal("""{"a":7,"b":8}""", JsonSerializer.Serialize(new { a = new IdAttente(7), b = new IdDefinition(8) }));
        Assert.Equal(new IdMigration(3), JsonSerializer.Deserialize<IdMigration>("3"));
        Assert.Equal(new IdPassageNoeud(4), JsonSerializer.Deserialize<IdPassageNoeud>("4"));
    }

    [Fact]
    public void Egalite_affichage_et_fabrique()
    {
        Assert.Equal(new IdInstance(1), IdInstance.Depuis(1));
        Assert.NotEqual(new IdInstance(1), new IdInstance(2));
        Assert.Equal("123", new IdDefinition(123).ToString());
        Assert.Equal(5, IdAttente.Depuis(5).Valeur);
        Assert.Equal("9", IdPassageNoeud.Depuis(9).ToString());
        Assert.Equal("10", IdMigration.Depuis(10).ToString());
        Assert.Equal("11", IdAttente.Depuis(11).ToString());
        Assert.Equal("12", IdInstance.Depuis(12).ToString());
    }
}

public class InstanceEtResultatsTests
{
    [Theory]
    [InlineData(StatutInstance.EnCours, true, false)]
    [InlineData(StatutInstance.Suspendue, true, true)]
    [InlineData(StatutInstance.EnErreur, true, true)]
    [InlineData(StatutInstance.Terminee, false, false)]
    [InlineData(StatutInstance.Annulee, false, false)]
    public void Etats_actif_et_au_repos(StatutInstance statut, bool active, bool auRepos)
    {
        var instance = new InstanceProcessus { Statut = statut };
        Assert.Equal(active, instance.EstActive);
        Assert.Equal(auRepos, instance.EstAuRepos);
    }

    [Fact]
    public void Resultat_depuis_une_instance()
    {
        var instance = new InstanceProcessus
        {
            Id = new IdInstance(3),
            Statut = StatutInstance.Suspendue,
            CodeNoeudCourant = "tache",
            IdTacheCourante = 99,
        };
        var resultat = ResultatExecution.Depuis(instance);
        Assert.Equal((new IdInstance(3), StatutInstance.Suspendue, "tache", 99L, false), (resultat.IdInstance!.Value, resultat.Statut!.Value, resultat.CodeNoeudCourant, resultat.IdTacheCourante!.Value, resultat.SansEffet));

        var aucun = ResultatExecution.Aucun("rien à faire", instance);
        Assert.True(aucun.SansEffet);
        Assert.Equal("rien à faire", aucun.Raison);
        Assert.Equal(new IdInstance(3), aucun.IdInstance);
        Assert.Null(ResultatExecution.Aucun("x").IdInstance);
    }

    [Fact]
    public void Resultat_de_signal()
    {
        var vide = new ResultatSignal { Raison = "personne" };
        Assert.True(vide.SansEffet);
        Assert.Equal(0, vide.NombreInstancesReprises);

        var signal = new ResultatSignal { Resultats = [ResultatExecution.Aucun("x"), new ResultatExecution { Statut = StatutInstance.Terminee }] };
        Assert.False(signal.SansEffet);
        Assert.Equal(1, signal.NombreInstancesReprises);
    }

    [Fact]
    public void Messages_des_exceptions()
    {
        Assert.Contains("'p'", new ProcessusIntrouvableException("p").Message);
        Assert.Contains("version 3", new ProcessusIntrouvableException("p", 3).Message);
        Assert.Equal("p", new ProcessusIntrouvableException("p").CleProcessus);
        Assert.Contains("aucune transition sortante", new AucuneTransitionException("p", "n", null).Message);
        Assert.Contains("aucune transition 's'", new AucuneTransitionException("p", "n", "s").Message);
        Assert.Equal(new IdInstance(4), new ConflitConcurrenceException(new IdInstance(4), 2).IdInstance);
        Assert.Contains("10 étapes", new BoucleInfinieException(new IdInstance(1), 10).Message);
        Assert.Contains("'c'", new CommandeIntrouvableException("c").Message);
        Assert.Contains("'r'", new RequeteIntrouvableException("r").Message);
        Assert.Contains("transaction", new TransactionRequiseException().Message);
        var definition = new DefinitionInvalideException("p", ["e1", "e2"]);
        Assert.Equal(["e1", "e2"], definition.Erreurs);
        Assert.Contains("- e2", definition.Message);
        var interne = new InvalidOperationException();
        Assert.Same(interne, new SentierException("x", interne).InnerException);
    }

    [Fact]
    public void Horloge_systeme_en_utc()
    {
        var maintenant = new HorlogeSysteme().MaintenantUtc;
        Assert.Equal(DateTimeKind.Utc, maintenant.Kind);
        Assert.InRange(maintenant, DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow.AddMinutes(1));
    }
}
