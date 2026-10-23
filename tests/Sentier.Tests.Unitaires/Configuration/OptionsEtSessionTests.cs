using System.Data;
using Microsoft.Extensions.DependencyInjection;
using Sentier.Tests.Unitaires.Support;

namespace Sentier.Tests.Unitaires.Configuration;

public class OptionsSentierTests
{
    [Fact]
    public void Valeurs_par_defaut()
    {
        var options = new OptionsSentier();
        Assert.Equal(1000, options.MaxEtapesParExecution);
        Assert.Equal(DialecteBase.Oracle, options.Dialecte);
        Assert.Empty(options.Assemblys);
        Assert.Null(options.TypeGestionnaireTaches);
        Assert.Null(options.TypeSession);
    }

    [Fact]
    public void Configuration_fluide()
    {
        var options = new OptionsSentier()
            .UtiliserSqlite("TESTS")
            .BalayerAssemblyDe<OptionsSentierTests>()
            .BalayerAssemblyDe<OptionsSentierTests>()
            .UtiliserGestionnaireTaches<GestionnaireTachesTest>()
            .UtiliserSession<SessionTest>();

        Assert.Equal("TESTS", options.PrefixeTables);
        Assert.Equal(DialecteBase.Sqlite, options.Dialecte);
        Assert.Single(options.Assemblys);
        Assert.Equal(typeof(GestionnaireTachesTest), options.TypeGestionnaireTaches);
        Assert.Equal(typeof(SessionTest), options.TypeSession);

        options.UtiliserOracle("PROD");
        Assert.Equal(("PROD", DialecteBase.Oracle), (options.PrefixeTables, options.Dialecte));
        options.UtiliserSqlite();
        Assert.Equal(DialecteBase.Sqlite, options.Dialecte);
        options.UtiliserOracle();
        Assert.Equal(DialecteBase.Oracle, options.Dialecte);
    }

    [Theory]
    [InlineData("")]
    [InlineData("1SENTIER")]
    [InlineData("SEN-TIER")]
    [InlineData("TROP_LONG_PREFIXE")]
    public void Prefixe_invalide(string prefixe)
        => Assert.Throws<SentierException>(() => new OptionsSentier { PrefixeTables = prefixe }.Valider());

    [Fact]
    public void Max_etapes_doit_etre_positif()
        => Assert.Throws<SentierException>(() => new OptionsSentier { PrefixeTables = "S", MaxEtapesParExecution = 0 }.Valider());

    [Fact]
    public void Options_valides()
        => new OptionsSentier { PrefixeTables = "Sentier_1" }.Valider();

    public sealed class SessionTest : ISessionSentier
    {
        public IDbConnection Connexion => throw new NotSupportedException();

        public IDbTransaction? Transaction => null;
    }
}

public class SessionSentierTests
{
    [Fact]
    public void La_session_par_defaut_prend_la_transaction_de_l_accesseur()
    {
        var connexion = new ConnexionFactice();
        var transaction = new TransactionFactice(connexion);
        using var fournisseur = new ServiceCollection().AddSingleton<IDbConnection>(connexion).BuildServiceProvider();
        var session = new SessionSentierParDefaut(fournisseur, new AccesseurTransaction { Transaction = transaction });

        Assert.Same(connexion, session.Connexion);
        Assert.Same(transaction, session.Transaction);
        Assert.Same(transaction, session.ExigerTransaction());
    }

    [Fact]
    public void Sinon_la_session_prend_la_transaction_enregistree()
    {
        var connexion = new ConnexionFactice();
        var transaction = new TransactionFactice(connexion);
        using var fournisseur = new ServiceCollection().AddSingleton<IDbConnection>(connexion).AddSingleton<IDbTransaction>(transaction).BuildServiceProvider();
        Assert.Same(transaction, new SessionSentierParDefaut(fournisseur, new AccesseurTransaction()).Transaction);
    }

    [Fact]
    public void Sans_connexion_enregistree()
    {
        using var fournisseur = new ServiceCollection().BuildServiceProvider();
        var session = new SessionSentierParDefaut(fournisseur, new AccesseurTransaction());
        Assert.Throws<SentierException>(() => session.Connexion);
        Assert.Null(session.Transaction);
        Assert.Throws<TransactionRequiseException>(() => session.ExigerTransaction());
    }

    [Fact]
    public void Une_transaction_validee_n_est_plus_active()
    {
        var connexion = new ConnexionFactice();
        var transaction = new TransactionFactice(connexion);
        using var fournisseur = new ServiceCollection().AddSingleton<IDbConnection>(connexion).BuildServiceProvider();
        var session = new SessionSentierParDefaut(fournisseur, new AccesseurTransaction { Transaction = transaction });

        transaction.Commit();
        Assert.Throws<TransactionRequiseException>(() => session.ExigerTransaction());
    }
}
