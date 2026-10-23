using Sentier.Definitions;
using Sentier.Variables;

namespace Sentier.Tests.Unitaires.Definitions;

public class ValidateurSousProcessusTests
{
    private static readonly Variable<long> Montant = Variable.Entier("montant");
    private static readonly Variable<decimal> MontantDecimal = Variable.Decimal("montant");
    private static readonly Variable<string> Texte = Variable.Texte("texte");
    private static readonly Variable<long> Obligatoire = Variable.Entier("obligatoire").Obligatoire();

    private static DefinitionProcessus Enfant() => ConstructeurProcessus.Nouveau("enfant")
        .Variables(MontantDecimal, Texte, Obligatoire)
        .Decision("d", d => d.Si("vrai", p => p.Valeur("valeur", true)).Alors(b => b.Fin("accepte")).Sinon(b => b.Fin("refuse")))
        .Construire();

    private static IReadOnlyList<string> Valider(Action<ConstructeurSousProcessus> configuration, Action<ConstructeurProcessus>? suite = null)
    {
        var constructeur = ConstructeurProcessus.Nouveau("parent").Variables(Montant, Texte).SousProcessus("s", "enfant", configuration);
        if (suite is null)
        {
            constructeur.Fin("fin");
        }
        else
        {
            suite(constructeur);
        }

        var parent = constructeur.Construire();
        return ValidateurSousProcessus.Valider(parent, (NoeudSousProcessus)parent.Noeud("s"), Enfant());
    }

    [Fact]
    public void Un_mappage_correct_est_valide()
        => Assert.Empty(Valider(s => s
            .Entree("montant", "montant")                  // Entier → Décimal : compatible
            .EntreeCleAffaire(Obligatoire)
            .Sortie("texte", "texte")));

    [Fact]
    public void La_variable_obligatoire_de_l_enfant_doit_etre_mappee()
        => Assert.Contains(Valider(_ => { }), e => e.Contains("la variable obligatoire 'obligatoire'"));

    [Fact]
    public void Entree_vers_une_variable_inconnue_de_l_enfant()
        => Assert.Contains(Valider(s => s.EntreeCleAffaire(Obligatoire).Entree("inconnue", "montant")), e => e.Contains("'inconnue' n'est pas déclarée dans l'enfant"));

    [Fact]
    public void Entree_de_type_incompatible()
        => Assert.Contains(Valider(s => s.Entree("obligatoire", "texte")), e => e.Contains("de type Entier, la source est de type Texte"));

    [Fact]
    public void Entree_statique_de_type_incompatible()
        => Assert.Contains(Valider(s => s.EntreeCleAffaire(Obligatoire).EntreeValeur(Variable.Booleen("texte"), true)), e => e.Contains("la source est de type Booleen"));

    [Fact]
    public void Sortie_depuis_une_variable_inconnue_ou_incompatible()
    {
        var erreurs = Valider(s => s.EntreeCleAffaire(Obligatoire).Sortie("inconnue", "texte").Sortie("montant", "montant"));
        Assert.Contains(erreurs, e => e.Contains("variable de sortie 'inconnue'"));
        Assert.Contains(erreurs, e => e.Contains("'montant' du parent est de type Entier"));
    }

    [Fact]
    public void Branche_sur_un_code_de_fin_inexistant()
        => Assert.Contains(Valider(s => s.EntreeCleAffaire(Obligatoire).SiFin("inexistant")), e => e.Contains("le code de fin 'inexistant' n'existe pas"));

    [Fact]
    public void Sans_sortie_par_defaut_chaque_fin_de_l_enfant_doit_avoir_sa_branche()
    {
        var erreurs = Valider(
            s => s.EntreeCleAffaire(Obligatoire).SiFin("accepte", "fin").SiAnnule("fin"),
            p => p.Fin("fin"));

        // « fin » suit le nœud : il existe donc une sortie par défaut, aucune erreur.
        Assert.Empty(erreurs);

        var parent = new DefinitionProcessus
        {
            CleProcessus = "parent",
            Nom = "parent",
            Noeuds = [new NoeudDebut { Code = "debut" }, new NoeudSousProcessus { Code = "s", CleProcessus = "enfant", Entrees = [new Parametre { Nom = "obligatoire", Source = SourceParametre.CleAffaire }] }, new NoeudFin { Code = "fin" }],
            Transitions = [new Transition { Source = "debut", Destination = "s" }, new Transition { Source = "s", Destination = "fin", NomSortie = "accepte" }],
        };
        var sansDefaut = ValidateurSousProcessus.Valider(parent, (NoeudSousProcessus)parent.Noeud("s"), Enfant());
        Assert.Contains(sansDefaut, e => e.Contains("le code de fin 'refuse' de l'enfant n'a ni branche ni sortie par défaut"));
    }
}
