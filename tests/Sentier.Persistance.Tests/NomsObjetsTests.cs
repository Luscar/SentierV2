namespace Sentier.Persistance.Tests;

public sealed class NomsObjetsTests
{
    [Fact]
    public void Le_prefixe_est_mis_en_majuscules_et_prefixe_chaque_objet()
    {
        var noms = new NomsObjets("wf");

        Assert.Equal("WF", noms.Prefixe);
        Assert.Equal("WF_DEFIN_PROCS", noms.Definition);
        Assert.Equal("WF_INSTC_PROCS", noms.Instance);
        Assert.Equal("WF_HIST_NOEUD_PROCS", noms.HistNoeud);
        Assert.Equal("WF_ATTEN_PROCS", noms.Attente);
        Assert.Equal("WF_MIGRA_PROCS", noms.Migration);
        Assert.Equal("WF_V_INSTC_ACTIV", noms.VueInstanceActive);
    }

    [Fact]
    public void Appliquer_remplace_le_jeton_de_prefixe()
        => Assert.Equal("CREATE TABLE WF_SEQ (x INT)", new NomsObjets("WF").Appliquer("CREATE TABLE {{P}}_SEQ (x INT)"));

    [Theory]
    [InlineData("")]
    [InlineData("9ABC")]
    [InlineData("A-B")]
    [InlineData("ABCDEFGHIJKLMN")]
    public void Un_prefixe_invalide_ou_trop_long_est_refuse(string prefixe)
        => Assert.Throws<SentierException>(() => new NomsObjets(prefixe));

    [Fact]
    public void Le_nom_le_plus_long_tient_en_30_caracteres_avec_le_prefixe_maximal()
    {
        var noms = new NomsObjets(new string('A', OptionsSentier.LongueurMaxPrefixe));
        Assert.True(noms.HistNoeud.Length <= NomsObjets.LongueurMaxNom);
    }
}
