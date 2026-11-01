using Sentier.Variables;

namespace Sentier.Core.Tests.Variables;

public sealed class VariableTests
{
    [Fact]
    public void Les_fabriques_donnent_le_bon_type()
    {
        Assert.Equal(TypeVariable.Texte, Variable.Texte("t").Type);
        Assert.Equal(TypeVariable.Entier, Variable.Entier("e").Type);
        Assert.Equal(TypeVariable.Decimal, Variable.Decimal("d").Type);
        Assert.Equal(TypeVariable.Booleen, Variable.Booleen("b").Type);
        Assert.Equal(TypeVariable.Date, Variable.Date("date").Type);
        Assert.Equal(TypeVariable.Json, Variable.Json<object>("j").Type);
    }

    [Fact]
    public void Obligatoire_et_ParDefaut_ne_modifient_pas_la_variable_d_origine()
    {
        var origine = Variable.Entier("solde");
        var obligatoire = origine.Obligatoire();
        var avecDefaut = obligatoire.ParDefaut(10);

        Assert.False(origine.EstObligatoire);
        Assert.Null(origine.Defaut);
        Assert.True(obligatoire.EstObligatoire);
        Assert.True(avecDefaut.EstObligatoire);
        Assert.NotNull(avecDefaut.Defaut);
    }

    [Fact]
    public void La_declaration_reprend_le_nom_le_type_et_le_caractere_obligatoire()
    {
        var declaration = Variable.Texte("decision").Obligatoire().ParDefaut("aucune").VersDeclaration();

        Assert.Equal("decision", declaration.Nom);
        Assert.Equal(TypeVariable.Texte, declaration.Type);
        Assert.True(declaration.Obligatoire);
        Assert.NotNull(declaration.Defaut);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void Un_nom_vide_est_refuse(string nom)
        => Assert.Throws<ArgumentException>(() => Variable.Texte(nom));

    [Fact]
    public void Les_noms_commencant_par_un_souligne_sont_reserves()
    {
        Assert.True(VariablesProcessus.EstReserve("_declencheur"));
        Assert.False(VariablesProcessus.EstReserve("declencheur"));
    }

    [Fact]
    public void ToString_donne_le_nom()
        => Assert.Equal("montant", Variable.Decimal("montant").ToString());
}
