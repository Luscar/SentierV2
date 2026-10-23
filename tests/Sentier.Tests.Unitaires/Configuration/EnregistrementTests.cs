using System.Data;
using Autofac;
using Autofac.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Sentier.Autofac;
using Sentier.Commandes;
using Sentier.DependencyInjection;
using Sentier.Execution;
using Sentier.Persistance;
using Sentier.Persistance.Dapper;
using Sentier.Persistance.Oracle;
using Sentier.Persistance.Sqlite;
using Sentier.Tests.Unitaires.Support;
using Sentier.Variables;

namespace Sentier.Tests.Unitaires.Configuration;

public class EnregistrementTests
{
    private static ServiceProvider Construire(Action<OptionsSentier> options, Action<IServiceCollection>? avant = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IDbConnection>(new ConnexionFactice());
        services.AddSingleton<Journal>().AddSingleton<Interrupteurs>().AddSingleton<BassinTaches>();
        avant?.Invoke(services);
        services.AddSentier(options);
        return services.BuildServiceProvider();
    }

    [Fact]
    public void Les_options_sont_validees()
        => Assert.Throws<SentierException>(() => new ServiceCollection().AddSentier(_ => { }));

    [Fact]
    public void Services_publics_et_dependances()
    {
        using var fournisseur = Construire(o => o.UtiliserSqlite("TEST").BalayerAssemblyDe<EnregistrementTests>().UtiliserGestionnaireTaches<GestionnaireTachesTest>());
        using var portee = fournisseur.CreateScope();
        var s = portee.ServiceProvider;

        Assert.NotNull(s.GetRequiredService<IServiceSentier>());
        Assert.NotNull(s.GetRequiredService<ISuiviSentier>());
        Assert.NotNull(s.GetRequiredService<InstallateurSchemaSentier>());
        Assert.IsType<SessionSentierParDefaut>(s.GetRequiredService<ISessionSentier>());
        Assert.IsType<HorlogeSysteme>(s.GetRequiredService<IHorloge>());
        Assert.IsType<SerialiseurVariablesJson>(s.GetRequiredService<ISerialiseurVariables>());
        Assert.IsType<DialecteSqlite>(s.GetRequiredService<IDialecteSql>());
        Assert.Equal("TEST", s.GetRequiredService<NomsObjets>().Prefixe);
        Assert.Equal(7, s.GetServices<IExecuteurNoeud>().Count());
        Assert.NotNull(s.GetRequiredService<IDepotInstances>());
        Assert.IsType<GestionnaireTachesTest>(s.GetRequiredService<IGestionnaireTaches>());
        Assert.NotNull(s.GetRequiredService<Tracer>());
        Assert.NotNull(s.GetRequiredService<ParametreVrai>());
        Assert.Contains(typeof(DemandeCongeTest), s.GetRequiredService<RegistreSentier>().Definitions);
        Assert.Same(s.GetRequiredService<CacheDefinitions>(), fournisseur.GetRequiredService<CacheDefinitions>());
    }

    [Fact]
    public void Aucune_connexion_n_est_enregistree_par_Sentier()
    {
        var services = new ServiceCollection().AddSentier(o => o.UtiliserOracle("S"));
        Assert.DoesNotContain(services, d => d.ServiceType == typeof(IDbConnection));
        Assert.DoesNotContain(services, d => d.ServiceType == typeof(IDbTransaction));
    }

    [Fact]
    public void Le_dialecte_oracle_est_choisi_par_defaut()
    {
        using var fournisseur = Construire(o => o.PrefixeTables = "S");
        Assert.IsType<DialecteOracle>(fournisseur.GetRequiredService<IDialecteSql>());
        using var portee = fournisseur.CreateScope();
        Assert.Null(portee.ServiceProvider.GetService<IGestionnaireTaches>());
    }

    [Fact]
    public void Plusieurs_gestionnaires_trouves_par_balayage_sont_refuses()
    {
        var ex = Assert.Throws<SentierException>(() => new ServiceCollection().AddSentier(o => o.UtiliserSqlite("S").BalayerAssemblyDe<EnregistrementTests>()));
        Assert.Contains("UtiliserGestionnaireTaches", ex.Message);
    }

    [Fact]
    public void Un_gestionnaire_deja_enregistre_par_le_client_est_conserve()
    {
        using var fournisseur = Construire(o => o.UtiliserSqlite("S").BalayerAssemblyDe<EnregistrementTests>(), s => s.AddScoped<IGestionnaireTaches, GestionnaireTachesTest>());
        using var portee = fournisseur.CreateScope();
        Assert.IsType<GestionnaireTachesTest>(portee.ServiceProvider.GetRequiredService<IGestionnaireTaches>());
    }

    [Fact]
    public void Le_gestionnaire_explicite_remplace_celui_du_client()
    {
        using var fournisseur = Construire(
            o => o.UtiliserSqlite("S").UtiliserGestionnaireTaches<GestionnaireTachesTest>(),
            s => s.AddScoped<IGestionnaireTaches, AutreGestionnaireTaches>());
        using var portee = fournisseur.CreateScope();
        Assert.IsType<GestionnaireTachesTest>(portee.ServiceProvider.GetRequiredService<IGestionnaireTaches>());
    }

    [Fact]
    public void Session_personnalisee()
    {
        using var fournisseur = Construire(o => o.UtiliserSqlite("S").UtiliserSession<OptionsSentierTests.SessionTest>());
        using var portee = fournisseur.CreateScope();
        Assert.IsType<OptionsSentierTests.SessionTest>(portee.ServiceProvider.GetRequiredService<ISessionSentier>());
    }

    [Fact]
    public void Les_services_du_client_enregistres_avant_sont_conserves()
    {
        var horloge = new HorlogeTest();
        using var fournisseur = Construire(o => o.UtiliserSqlite("S"), s => s.AddSingleton<IHorloge>(horloge));
        Assert.Same(horloge, fournisseur.GetRequiredService<IHorloge>());
    }

    [Fact]
    public void Module_autofac()
    {
        var horloge = new HorlogeTest();
        var builder = new ContainerBuilder();
        builder.RegisterInstance(new Journal());
        builder.RegisterInstance(new Interrupteurs());
        builder.RegisterInstance(new BassinTaches());
        builder.RegisterInstance(new ConfigurationDynamique());
        builder.RegisterModule(new ModuleSentier(o => o.UtiliserSqlite("AUTOFAC").BalayerAssemblyDe<EnregistrementTests>().UtiliserGestionnaireTaches<GestionnaireTachesTest>()));
        builder.RegisterInstance<IHorloge>(horloge);   // après le module : remplace celle de Sentier
        using var conteneur = builder.Build();

        var connexion = new ConnexionFactice();
        using var portee = conteneur.BeginLifetimeScope(b =>
        {
            b.RegisterInstance<IDbConnection>(connexion).ExternallyOwned();
            b.RegisterInstance<IDbTransaction>(new TransactionFactice(connexion)).ExternallyOwned();
        });

        Assert.Same(horloge, portee.Resolve<IHorloge>());
        Assert.Equal("AUTOFAC", portee.Resolve<NomsObjets>().Prefixe);
        Assert.NotNull(portee.Resolve<IServiceSentier>());
        Assert.IsType<GestionnaireTachesTest>(portee.Resolve<IGestionnaireTaches>());
        Assert.NotNull(portee.Resolve<ISessionSentier>().ExigerTransaction());
        using var autrePortee = conteneur.BeginLifetimeScope();
        Assert.NotSame(portee.Resolve<ISessionSentier>(), autrePortee.Resolve<ISessionSentier>());
    }
}
