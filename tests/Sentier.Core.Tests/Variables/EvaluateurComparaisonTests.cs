using System.Text.Json;
using Sentier.Variables;

namespace Sentier.Core.Tests.Variables;

public sealed class EvaluateurComparaisonTests
{
    [Theory]
    [InlineData(OperateurComparaison.Superieur, 5L, 3L, true)]
    [InlineData(OperateurComparaison.Superieur, 3L, 3L, false)]
    [InlineData(OperateurComparaison.SuperieurOuEgal, 3L, 3L, true)]
    [InlineData(OperateurComparaison.Inferieur, 2L, 3L, true)]
    [InlineData(OperateurComparaison.InferieurOuEgal, 4L, 3L, false)]
    [InlineData(OperateurComparaison.Egal, 3L, 3L, true)]
    [InlineData(OperateurComparaison.Different, 3L, 4L, true)]
    public void Compare_les_entiers(OperateurComparaison operateur, long gauche, long droite, bool attendu)
        => Assert.Equal(attendu, EvaluateurComparaison.Evaluer(operateur, gauche, droite));

    [Fact]
    public void Compare_un_entier_et_un_decimal()
    {
        Assert.True(EvaluateurComparaison.Evaluer(OperateurComparaison.Egal, 10L, 10.0m));
        Assert.True(EvaluateurComparaison.Evaluer(OperateurComparaison.Superieur, 10.5m, 10L));
        Assert.True(EvaluateurComparaison.Evaluer(OperateurComparaison.Inferieur, 9L, 9.01m));
    }

    [Fact]
    public void Compare_les_dates_en_utc()
    {
        var utc = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var plusTard = utc.AddMinutes(1);

        Assert.True(EvaluateurComparaison.Evaluer(OperateurComparaison.Superieur, plusTard, utc));
        Assert.True(EvaluateurComparaison.Evaluer(OperateurComparaison.Egal, utc, utc.ToLocalTime()));
    }

    [Fact]
    public void Compare_les_textes_en_ordre_ordinal_et_sensible_a_la_casse()
    {
        Assert.True(EvaluateurComparaison.Evaluer(OperateurComparaison.Inferieur, "B", "a"));
        Assert.True(EvaluateurComparaison.Evaluer(OperateurComparaison.Contient, "Dossier urgent", "urgent"));
        Assert.False(EvaluateurComparaison.Evaluer(OperateurComparaison.Contient, "Dossier urgent", "URGENT"));
        Assert.True(EvaluateurComparaison.Evaluer(OperateurComparaison.CommencePar, "CP-03", "CP-"));
        Assert.False(EvaluateurComparaison.Evaluer(OperateurComparaison.CommencePar, "cp-03", "CP-"));
    }

    [Fact]
    public void Une_valeur_vide_ne_satisfait_que_EstVide_et_Different()
    {
        Assert.True(EvaluateurComparaison.Evaluer(OperateurComparaison.EstVide, null, null));
        Assert.False(EvaluateurComparaison.Evaluer(OperateurComparaison.EstRenseignee, null, null));
        Assert.True(EvaluateurComparaison.Evaluer(OperateurComparaison.Different, null, 1L));
        Assert.False(EvaluateurComparaison.Evaluer(OperateurComparaison.Egal, null, 1L));
        Assert.False(EvaluateurComparaison.Evaluer(OperateurComparaison.Superieur, null, 1L));
        Assert.False(EvaluateurComparaison.Evaluer(OperateurComparaison.Inferieur, null, 1L));
        Assert.False(EvaluateurComparaison.Evaluer(OperateurComparaison.Contient, null, "x"));
    }

    [Fact]
    public void Egal_a_null_equivaut_a_EstVide()
    {
        Assert.True(EvaluateurComparaison.Evaluer(OperateurComparaison.Egal, null, null));
        Assert.False(EvaluateurComparaison.Evaluer(OperateurComparaison.Egal, "x", null));
    }

    [Fact]
    public void Des_types_incomparables_ne_satisfont_aucun_ordre()
    {
        Assert.False(EvaluateurComparaison.Evaluer(OperateurComparaison.Superieur, "10", 5L));
        Assert.False(EvaluateurComparaison.Evaluer(OperateurComparaison.Egal, "10", 10L));
        Assert.True(EvaluateurComparaison.Evaluer(OperateurComparaison.Different, "10", 10L));
    }

    [Fact]
    public void Compare_les_booleens()
    {
        Assert.True(EvaluateurComparaison.Evaluer(OperateurComparaison.Egal, true, true));
        Assert.False(EvaluateurComparaison.Evaluer(OperateurComparaison.Egal, true, false));
    }

    [Fact]
    public void Compare_les_json_en_profondeur()
    {
        var a = JsonSerializer.SerializeToElement(new { rue = "Principale", numero = 12 });
        var b = JsonSerializer.SerializeToElement(new { rue = "Principale", numero = 12 });
        var c = JsonSerializer.SerializeToElement(new { rue = "Principale", numero = 13 });

        Assert.True(EvaluateurComparaison.Evaluer(OperateurComparaison.Egal, a, b));
        Assert.False(EvaluateurComparaison.Evaluer(OperateurComparaison.Egal, a, c));
    }
}
