using Sentier.Definitions;
using Sentier.Variables;

namespace Sentier.Tests.Unitaires.Definitions;

/// <summary>
/// Le validateur est testé sur des définitions écrites à la main (comme après une désérialisation),
/// pour couvrir des erreurs que le constructeur empêche en amont.
/// </summary>
public class ValidateurDefinitionTests
{
    private static Transition T(string source, string destination, string? sortie = null) => new() { Source = source, Destination = destination, NomSortie = sortie };

    private static DefinitionProcessus Definition(IEnumerable<Noeud> noeuds, IEnumerable<Transition> transitions, params DeclarationVariable[] variables) => new()
    {
        CleProcessus = "p",
        Nom = "p",
        Noeuds = noeuds.ToList(),
        Transitions = transitions.ToList(),
        Variables = variables,
    };

    private static DeclarationVariable Declaration(string nom, TypeVariable type, ValeurTypee? defaut = null) => new() { Nom = nom, Type = type, Defaut = defaut };

    private static AppelCommande Commande(string nom = "tracer", params Parametre[] parametres) => new() { Nom = nom, Parametres = parametres };

    private static Parametre ParVariable(string nom, string? variable) => new() { Nom = nom, Source = SourceParametre.Variable, Variable = variable };

    private static IReadOnlyList<string> Erreurs(DefinitionProcessus definition) => ValidateurDefinition.Valider(definition);

    private static readonly Noeud Debut = new NoeudDebut { Code = "debut" };
    private static readonly Noeud Fin = new NoeudFin { Code = "fin" };

    [Fact]
    public void Une_definition_minimale_est_valide()
        => Assert.Empty(Erreurs(Definition([Debut, Fin], [T("debut", "fin")])));

    [Fact]
    public void Sans_debut_ni_fin()
    {
        var erreurs = Erreurs(Definition([new NoeudSystemique { Code = "a", Commande = Commande() }], [T("a", "a")]));
        Assert.Contains("La définition n'a pas de nœud de début.", erreurs);
        Assert.Contains("La définition doit avoir au moins un nœud de fin.", erreurs);
    }

    [Fact]
    public void Plusieurs_debuts()
        => Assert.Contains("La définition a plusieurs nœuds de début.", Erreurs(Definition([Debut, new NoeudDebut { Code = "d2" }, Fin], [T("debut", "fin"), T("d2", "fin")])));

    [Fact]
    public void Codes_en_double()
        => Assert.Contains(Erreurs(Definition([Debut, Fin, new NoeudFin { Code = "fin" }], [T("debut", "fin")])), e => e.Contains("'fin' est utilisé plusieurs fois"));

    [Fact]
    public void Variables_invalides()
    {
        var erreurs = Erreurs(Definition([Debut, Fin], [T("debut", "fin")],
            Declaration("", TypeVariable.Texte),
            Declaration("_reservee", TypeVariable.Texte),
            Declaration("double", TypeVariable.Texte),
            Declaration("double", TypeVariable.Texte),
            Declaration("mauvaisDefaut", TypeVariable.Entier, ValeurTypee.Depuis("x"))));

        Assert.Contains("Une variable n'a pas de nom.", erreurs);
        Assert.Contains(erreurs, e => e.Contains("'_reservee' est réservé"));
        Assert.Contains("La variable 'double' est déclarée plusieurs fois.", erreurs);
        Assert.Contains(erreurs, e => e.Contains("'mauvaisDefaut'") && e.Contains("Texte au lieu de Entier"));
    }

    [Fact]
    public void Transitions_vers_ou_depuis_un_noeud_inexistant()
    {
        var erreurs = Erreurs(Definition([Debut, Fin], [T("debut", "fin"), T("fantome", "fin"), T("debut", "ailleurs", "x")]));
        Assert.Contains("Une transition part du nœud inexistant 'fantome'.", erreurs);
        Assert.Contains("Le lien du nœud 'debut' mène au nœud inexistant 'ailleurs'.", erreurs);
    }

    [Fact]
    public void Une_fin_avec_sortie_et_un_noeud_sans_sortie()
    {
        var erreurs = Erreurs(Definition([Debut, new NoeudSystemique { Code = "a", Commande = Commande() }, Fin], [T("debut", "a"), T("fin", "a")]));
        Assert.Contains("Le nœud de fin 'fin' ne peut pas avoir de sortie.", erreurs);
        Assert.Contains(erreurs, e => e.StartsWith("Le nœud 'a' n'a pas de sortie"));
    }

    [Fact]
    public void Sorties_nommees_en_double()
        => Assert.Contains("Le nœud 'debut' a plusieurs sorties nommées 'x'.", Erreurs(Definition([Debut, Fin], [T("debut", "fin", "x"), T("debut", "fin", "x")])));

    [Fact]
    public void Noeud_inatteignable()
        => Assert.Contains(Erreurs(Definition([Debut, Fin, new NoeudFin { Code = "autre" }], [T("debut", "fin")])), e => e.Contains("'autre' n'est jamais atteint"));

    [Fact]
    public void Decision_sans_branche_conditionnelle_ni_sinon()
    {
        var erreurs = Erreurs(Definition([Debut, new NoeudDecision { Code = "d" }, Fin], [T("debut", "d"), T("d", "fin", "x")]));
        Assert.Contains("La décision 'd' doit avoir au moins une branche conditionnelle.", erreurs);
        Assert.Contains("La décision 'd' doit avoir exactement une branche « sinon ».", erreurs);
    }

    [Fact]
    public void Decision_avec_requete_sur_variable_non_declaree()
    {
        var erreurs = Erreurs(Definition(
            [Debut, new NoeudDecision { Code = "d" }, Fin],
            [
                T("debut", "d"),
                new Transition { Source = "d", Destination = "fin", NomSortie = "q", Requete = new AppelRequete { Nom = "q", Parametres = [ParVariable("p", "absente")] } },
                new Transition { Source = "d", Destination = "fin", NomSortie = "sinon", EstSinon = true },
            ]));
        Assert.Contains(erreurs, e => e.Contains("'absente'") && e.Contains("n'est pas déclarée"));
    }

    [Fact]
    public void Appels_invalides()
    {
        var erreurs = Erreurs(Definition(
            [Debut, new NoeudSystemique { Code = "a", Commande = Commande(" ", ParVariable("p", null), new Parametre { Nom = "s", Source = SourceParametre.Statique }, ParVariable("x", null)) }, Fin],
            [T("debut", "a"), T("a", "fin")]));

        Assert.Contains(erreurs, e => e.StartsWith("Le nom de la commande du nœud 'a' est vide"));
        Assert.Contains(erreurs, e => e.Contains("'p'") && e.Contains("ne désigne aucune variable"));
        Assert.Contains(erreurs, e => e.Contains("statique 's'") && e.Contains("n'a pas de valeur"));
    }

    [Fact]
    public void Parametres_en_double()
    {
        var erreurs = Erreurs(Definition(
            [Debut, new NoeudSystemique { Code = "a", Commande = Commande("c", new Parametre { Nom = "x", Source = SourceParametre.CleAffaire }, new Parametre { Nom = "x", Source = SourceParametre.CleAffaire }) }, Fin],
            [T("debut", "a"), T("a", "fin")]));
        Assert.Contains(erreurs, e => e.Contains("Le paramètre 'x'") && e.Contains("plusieurs fois"));
    }

    [Fact]
    public void Attentes_invalides()
    {
        var attente = new NoeudAttente
        {
            Code = "w",
            Declencheurs =
            [
                new DeclencheurEcheance { NomSortie = "e1", Duree = TimeSpan.FromDays(1), DateAbsolue = DateTime.UtcNow },
                new DeclencheurEcheance { NomSortie = "e2", Variable = "texte" },
                new DeclencheurSignal { NomSortie = "s1", NomSignal = "S" },
                new DeclencheurSignal { NomSortie = "s2", NomSignal = "S", VariableCorrelation = "absente" },
                new DeclencheurSignal { NomSortie = "s3", NomSignal = " " },
            ],
            CommandesEntree = [Commande(" ")],
        };
        var erreurs = Erreurs(Definition([Debut, attente, Fin], [T("debut", "w"), T("w", "fin", "e1")], Declaration("texte", TypeVariable.Texte)));

        Assert.Contains("L'attente 'w' a plus d'une échéance.", erreurs);
        Assert.Contains("L'échéance de l'attente 'w' doit avoir exactement une source : date, durée ou variable.", erreurs);
        Assert.Contains(erreurs, e => e.Contains("'texte'") && e.Contains("doit être de type Date"));
        Assert.Contains("L'attente 'w' attend plusieurs fois le signal 'S'.", erreurs);
        Assert.Contains(erreurs, e => e.Contains("'absente'") && e.Contains("corrélation"));
        Assert.Contains("Un signal de l'attente 'w' n'a pas de nom.", erreurs);
        Assert.Contains("Le déclencheur 's1' de l'attente 'w' n'a pas de sortie.", erreurs);
        Assert.Contains(erreurs, e => e.Contains("commande d'entrée de l'attente 'w'"));
    }

    [Fact]
    public void Attente_sans_declencheur()
        => Assert.Contains("L'attente 'w' doit avoir au moins un déclencheur.",
            Erreurs(Definition([Debut, new NoeudAttente { Code = "w" }, Fin], [T("debut", "w"), T("w", "fin")])));

    [Fact]
    public void Interactifs_invalides()
    {
        var interactif = new NoeudInteractif
        {
            Code = "i",
            NomTache = " ",
            Assigne = ParVariable("assigne", "nombre"),
            VariableEcheance = "nombre",
            Donnees = [ParVariable("d", "absente")],
            CommandesSortie = [Commande(" ")],
        };
        var erreurs = Erreurs(Definition([Debut, interactif, Fin], [T("debut", "i"), T("i", "fin")], Declaration("nombre", TypeVariable.Entier)));

        Assert.Contains("Le nœud interactif 'i' n'a pas de nom de tâche.", erreurs);
        Assert.Contains(erreurs, e => e.Contains("assigné du nœud 'i'") && e.Contains("Texte"));
        Assert.Contains(erreurs, e => e.Contains("échéance du nœud 'i'") && e.Contains("Date"));
        Assert.Contains(erreurs, e => e.Contains("'absente'"));
        Assert.Contains(erreurs, e => e.Contains("commande du nœud 'i'"));
    }

    [Fact]
    public void Sous_processus_invalides()
    {
        var sousProcessus = new NoeudSousProcessus
        {
            Code = "s",
            CleProcessus = " ",
            VariableCleAffaire = "texte",
            Entrees = [ParVariable("x", "texte"), ParVariable("x", "texte")],
            Sorties = [new MappageSortie { VariableEnfant = "y", VariableParent = "absente" }],
        };
        var erreurs = Erreurs(Definition([Debut, sousProcessus, Fin], [T("debut", "s"), T("s", "fin")], Declaration("texte", TypeVariable.Texte)));

        Assert.Contains("Le sous-processus 's' n'indique pas la clé du processus enfant.", erreurs);
        Assert.Contains(erreurs, e => e.Contains("clé d'affaire du sous-processus 's'") && e.Contains("Entier"));
        Assert.Contains("Le sous-processus 's' remplit plusieurs fois la variable 'x' de l'enfant.", erreurs);
        Assert.Contains(erreurs, e => e.Contains("'absente'") && e.Contains("mappage de sortie"));
    }
}
