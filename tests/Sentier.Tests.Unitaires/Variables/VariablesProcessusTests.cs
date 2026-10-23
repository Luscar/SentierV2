using System.Text.Json;
using Sentier.Variables;

namespace Sentier.Tests.Unitaires.Variables;

public class VariablesProcessusTests
{
    private static readonly Variable<long> NbJours = Variable.Entier("nbJours").Obligatoire();
    private static readonly Variable<long> Solde = Variable.Entier("solde").ParDefaut(10);
    private static readonly Variable<string> Motif = Variable.Texte("motif");
    private static readonly Variable<decimal> Montant = Variable.Decimal("montant");
    private static readonly Variable<DateTime> Debut = Variable.Date("debut");
    private static readonly Variable<bool> Urgent = Variable.Booleen("urgent");
    private static readonly Variable<int[]> Liste = Variable.Json<int[]>("liste");

    private static readonly IReadOnlyDictionary<string, DeclarationVariable> Declarations =
        new IVariable[] { NbJours, Solde, Motif, Montant, Debut, Urgent, Liste }.Select(v => v.VersDeclaration()).ToDictionary(d => d.Nom);

    private static VariablesProcessus Vides() => new(Declarations, []);

    [Fact]
    public void Variable_porte_nom_type_et_options()
    {
        Assert.Equal("nbJours", NbJours.Nom);
        Assert.Equal(TypeVariable.Entier, NbJours.Type);
        Assert.True(NbJours.EstObligatoire);
        Assert.Null(NbJours.Defaut);
        Assert.Equal(10L, Solde.Defaut!.VersObjet());
        Assert.Equal("nbJours", NbJours.ToString());
    }

    [Fact]
    public void Obligatoire_et_ParDefaut_retournent_une_copie()
    {
        var base_ = Variable.Texte("x");
        var obligatoire = base_.Obligatoire();
        Assert.False(base_.EstObligatoire);
        Assert.True(obligatoire.EstObligatoire);
        Assert.Null(base_.Defaut);
        Assert.Equal("défaut", obligatoire.ParDefaut("défaut").Defaut!.VersObjet());
    }

    [Fact]
    public void Une_variable_sans_nom_est_refusee()
        => Assert.Throws<ArgumentException>(() => Variable.Texte(" "));

    [Fact]
    public void VersDeclaration_reprend_la_variable()
    {
        var declaration = Solde.VersDeclaration();
        Assert.Equal("solde", declaration.Nom);
        Assert.Equal(TypeVariable.Entier, declaration.Type);
        Assert.False(declaration.Obligatoire);
        Assert.Equal(TypeVariable.Entier, declaration.Defaut!.Type);
    }

    [Fact]
    public void Initialiser_applique_les_valeurs_et_les_defauts()
    {
        var variables = VariablesProcessus.Initialiser(Declarations, ValeursVariables.Avec(NbJours, 3L).Et(Motif, "Vacances"));
        Assert.Equal(3L, variables.Lire(NbJours));
        Assert.Equal(10L, variables.Lire(Solde));
        Assert.Equal("Vacances", variables.Lire(Motif));
        Assert.False(variables.Contient("montant"));
    }

    [Fact]
    public void Initialiser_signale_les_obligatoires_manquantes()
    {
        var ex = Assert.Throws<VariableInvalideException>(() => VariablesProcessus.Initialiser(Declarations, null));
        Assert.Contains("nbJours", ex.Message);
    }

    [Fact]
    public void Initialiser_refuse_une_variable_non_declaree()
        => Assert.Throws<VariableInvalideException>(() => VariablesProcessus.Initialiser(Declarations, new Dictionary<string, object?> { ["nbJours"] = 1L, ["inconnue"] = 1 }));

    [Fact]
    public void Initialiser_normalise_les_valeurs()
    {
        var variables = VariablesProcessus.Initialiser(Declarations, new Dictionary<string, object?> { ["nbJours"] = 3, ["montant"] = 4 });
        Assert.IsType<long>(variables.Lire("nbJours"));
        Assert.Equal(4m, variables.Lire("montant"));
    }

    [Fact]
    public void Ecrire_valide_la_declaration_le_type_et_le_nom()
    {
        var variables = Vides();
        variables.Ecrire(Montant, 12.5m);
        Assert.Equal(12.5m, variables.Lire(Montant));

        Assert.Throws<VariableInvalideException>(() => variables.Ecrire("inconnue", 1));
        Assert.Throws<VariableInvalideException>(() => variables.Ecrire("nbJours", "trois"));
        Assert.Throws<VariableInvalideException>(() => variables.Ecrire("_systeme", "x"));
    }

    [Fact]
    public void Effacer_retire_la_valeur()
    {
        var variables = Vides();
        variables.Ecrire(Motif, "x");
        variables.Effacer("motif");
        Assert.False(variables.Contient("motif"));
        Assert.Null(variables.Lire("motif"));
    }

    [Fact]
    public void Lectures_typees()
    {
        var variables = Vides();
        variables.Ecrire(Urgent, true);
        Assert.True(variables.Lire(Urgent));
        Assert.True(variables.TryLire(Urgent, out var urgent) && urgent);
        Assert.False(variables.TryLire(Motif, out _));
        Assert.Equal("aucun", variables.LireOuDefaut(Motif, "aucun"));
        Assert.Throws<VariableInvalideException>(() => variables.Lire(Motif));
    }

    [Fact]
    public void Une_variable_json_se_relit_dans_son_type()
    {
        var variables = Vides();
        variables.Ecrire(Liste, new[] { 1, 2, 3 });
        Assert.Equal(new[] { 1, 2, 3 }, variables.Lire(Liste));
    }

    [Fact]
    public void Fusionner_ne_retient_que_les_variables_declarees()
    {
        var variables = Vides();
        var nombre = variables.Fusionner(new Dictionary<string, object?> { ["motif"] = "Maladie", ["inconnue"] = 1, ["_declencheur"] = "x" });
        Assert.Equal(1, nombre);
        Assert.Equal("Maladie", variables.Lire(Motif));
        Assert.False(variables.Valeurs.ContainsKey("inconnue"));
        Assert.False(variables.Valeurs.ContainsKey("_declencheur"));
        Assert.Equal(0, variables.Fusionner(null));
    }

    [Fact]
    public void Fusionner_valide_le_type()
        => Assert.Throws<VariableInvalideException>(() => Vides().Fusionner(new Dictionary<string, object?> { ["nbJours"] = "x" }));

    [Fact]
    public void Les_variables_systeme_sont_reservees()
    {
        Assert.True(VariablesProcessus.EstReserve("_x"));
        Assert.False(VariablesProcessus.EstReserve("x"));

        var variables = Vides();
        variables.EcrireSysteme(VariablesProcessus.VariableDeclencheur, "echeance");
        Assert.Equal("echeance", variables.Lire(VariablesProcessus.VariableDeclencheur));
    }

    [Fact]
    public void Serialisation_aller_retour_avec_les_types_declares()
    {
        var variables = VariablesProcessus.Initialiser(Declarations, ValeursVariables
            .Avec(NbJours, 3L)
            .Et(Montant, 12.50m)
            .Et(Debut, new DateTime(2026, 7, 1, 8, 0, 0, DateTimeKind.Utc))
            .Et(Urgent, false)
            .Et(Liste, [4, 5]));
        variables.EcrireSysteme(VariablesProcessus.VariableFinEnfant, "fin");
        var serialiseur = new SerialiseurVariablesJson();

        var json = serialiseur.Serialiser(variables.Valeurs);
        var relues = VariablesProcessus.Charger(Declarations, serialiseur, json);

        Assert.Equal(3L, relues.Lire("nbJours"));
        Assert.Equal(10L, relues.Lire("solde"));
        Assert.Equal(12.50m, relues.Lire("montant"));
        Assert.Equal(new DateTime(2026, 7, 1, 8, 0, 0, DateTimeKind.Utc), relues.Lire("debut"));
        Assert.Equal(false, relues.Lire("urgent"));
        Assert.Equal(new[] { 4, 5 }, relues.Lire(Liste));
        Assert.Equal("fin", relues.Lire(VariablesProcessus.VariableFinEnfant));
    }

    [Fact]
    public void Deserialiser_lit_les_cles_non_declarees_en_json()
    {
        var valeurs = new SerialiseurVariablesJson().Deserialiser("""{"ancienne":{"a":1}}""", new Dictionary<string, TypeVariable>());
        Assert.IsType<JsonElement>(valeurs["ancienne"]);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Deserialiser_un_json_vide_donne_aucune_valeur(string? json)
        => Assert.Empty(new SerialiseurVariablesJson().Deserialiser(json, new Dictionary<string, TypeVariable>()));

    [Fact]
    public void ValeursVariables_se_construit_avec_les_constantes()
    {
        var valeurs = ValeursVariables.Avec(NbJours, 2L).Et(Motif, "x");
        Assert.Equal(2L, valeurs["nbJours"]);
        Assert.Equal("x", valeurs["motif"]);
    }

    [Fact]
    public void ValeurTypee_deduit_ou_impose_le_type()
    {
        var deduite = ValeurTypee.Depuis(5);
        Assert.Equal(TypeVariable.Entier, deduite.Type);
        Assert.Equal(5L, deduite.VersObjet());

        var imposee = ValeurTypee.Depuis(5, TypeVariable.Decimal);
        Assert.Equal(TypeVariable.Decimal, imposee.Type);
        Assert.Equal(5m, imposee.VersObjet());

        Assert.Throws<VariableInvalideException>(() => ValeurTypee.Depuis("x", TypeVariable.Entier));
    }
}
