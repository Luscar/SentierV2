namespace Sentier.Core.Tests;

public sealed class OptionsSentierTests
{
    [Fact]
    public void Des_options_completes_sont_valides()
    {
        var options = new OptionsSentier().UtiliserSqlite("SENTIER").BalayerAssemblyDe<OptionsSentierTests>();

        options.Valider();

        Assert.Equal(TypeBaseDonnees.Sqlite, options.Dialecte);
        Assert.Equal("SENTIER", options.PrefixeTables);
        Assert.Single(options.Assemblys);
    }

    [Fact]
    public void Le_dialecte_par_defaut_est_oracle()
        => Assert.Equal(TypeBaseDonnees.Oracle, new OptionsSentier().Dialecte);

    [Fact]
    public void Balayer_deux_fois_le_meme_assembly_ne_l_ajoute_qu_une_fois()
    {
        var options = new OptionsSentier().BalayerAssemblyDe<OptionsSentierTests>().BalayerAssemblyDe<CommandeFictive>();
        Assert.Single(options.Assemblys);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("1SENTIER")]
    [InlineData("SEN-TIER")]
    [InlineData("SENTIER_TROP_LONG")]
    public void Un_prefixe_invalide_est_refuse(string prefixe)
        => Assert.Throws<SentierException>(() => new OptionsSentier { PrefixeTables = prefixe }.Valider());

    [Fact]
    public void Un_prefixe_de_12_caracteres_est_accepte()
        => new OptionsSentier { PrefixeTables = "ABCDEFGHIJKL" }.Valider();

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Le_nombre_maximal_d_etapes_doit_etre_positif(int max)
        => Assert.Throws<SentierException>(() => new OptionsSentier { PrefixeTables = "S", MaxEtapesParExecution = max }.Valider());
}
