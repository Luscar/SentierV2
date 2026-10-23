using Sentier.Migration;
using Sentier.Tests.Unitaires.Support;
using Sentier.Variables;

namespace Sentier.Tests.Unitaires.Migration;

public class ConstructeurPlanMigrationTests
{
    private static readonly Variable<long> Solde = Variable.Entier("solde");

    private static PlanMigration Construire(Action<ConstructeurPlanMigration> definir)
    {
        var constructeur = new ConstructeurPlanMigration();
        definir(constructeur);
        return constructeur.Construire("plan-test");
    }

    [Fact]
    public void Plan_complet()
    {
        var plan = Construire(p => p
            .Noeud("ancien", "nouveau", r => r.ConserverAttentes().ConserverTaches())
            .Noeud("a", "b")
            .PourNoeud("tache", r => r.PuisReprendreA("suite"))
            .RenommerVariable("vieux", "neuf")
            .ValeurVariable(Solde, 5L)
            .ValeurVariable("note", 1.5m)
            .CalculerVariables<EcrireValeur>(c => c.Valeur("variable", "x"))
            .CalculerVariables("tracer"));

        Assert.Equal(("nouveau", ActionAttentes.Conserver, ActionTaches.Conserver), (plan.Noeuds["ancien"].CodeCible, plan.Noeuds["ancien"].Attentes, plan.Noeuds["ancien"].Taches));
        Assert.Null(plan.Noeuds["a"].Attentes);
        var suite = plan.Noeuds["tache"];
        Assert.Equal(("suite", ActionAttentes.Annuler, ActionTaches.Annuler), (suite.CodeSuite, suite.Attentes, suite.Taches));
        Assert.Null(suite.CodeCible);
        Assert.Equal("neuf", plan.RenommagesVariables["vieux"]);
        Assert.Equal(5L, plan.ValeursVariables["solde"].VersObjet());
        Assert.Equal(TypeVariable.Decimal, plan.ValeursVariables["note"].Type);
        Assert.Equal(["ecrire", "tracer"], plan.CommandesVariables.Select(c => c.Nom));
    }

    [Fact]
    public void Les_regles_d_un_meme_noeud_se_cumulent()
    {
        var plan = Construire(p => p.PourNoeud("n", r => r.Vers("m")).PourNoeud("n", r => r.RecreerAttentes()));
        Assert.Equal(("m", ActionAttentes.Recreer), (plan.Noeuds["n"].CodeCible, plan.Noeuds["n"].Attentes));
    }

    [Fact]
    public void Annuler_sans_noeud_de_suite_est_refuse()
    {
        var ex = Assert.Throws<MigrationInvalideException>(() => Construire(p => p.PourNoeud("n", r => r.AnnulerAttentes())));
        Assert.Contains("plan-test", ex.Message);
        Assert.Contains("PuisReprendreA", ex.Message);
        Assert.Throws<MigrationInvalideException>(() => Construire(p => p.PourNoeud("n", r => r.AnnulerTaches())));
    }

    [Fact]
    public void Conserver_avec_un_noeud_de_suite_est_refuse()
    {
        Assert.Throws<MigrationInvalideException>(() => Construire(p => p.PourNoeud("n", r => r.ConserverAttentes().PuisReprendreA("x"))));
        Assert.Throws<MigrationInvalideException>(() => Construire(p => p.PourNoeud("n", r => r.ConserverTaches().PuisReprendreA("x"))));
    }

    [Fact]
    public void Vers_et_reprise_sont_exclusifs()
    {
        var ex = Assert.Throws<MigrationInvalideException>(() => Construire(p => p.Noeud("n", "m", r => r.PuisReprendreA("x"))));
        Assert.Contains("pas les deux", ex.Message);
    }

    [Fact]
    public void Deux_variables_renommees_vers_le_meme_nom()
        => Assert.Throws<MigrationInvalideException>(() => Construire(p => p.RenommerVariable("a", "c").RenommerVariable("b", "c")));
}
