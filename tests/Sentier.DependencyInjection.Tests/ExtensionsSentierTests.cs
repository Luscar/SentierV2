using Microsoft.Extensions.DependencyInjection;
using Sentier.Commandes;
using Sentier.Persistance;

namespace Sentier.DependencyInjection.Tests;

public sealed class ExtensionsSentierTests
{
    private static IServiceCollection Enregistrer(Action<OptionsSentier>? configurer = null, Action<IServiceCollection>? avant = null)
    {
        var services = new ServiceCollection();
        avant?.Invoke(services);
        services.AddSentier(o =>
        {
            o.UtiliserSqlite("SENTIER").BalayerAssemblyDe<ExtensionsSentierTests>().UtiliserGestionnaireTaches<GestionnaireTachesA>();
            configurer?.Invoke(o);
        });
        return services;
    }

    private static ServiceDescriptor Unique<T>(IServiceCollection services)
        => Assert.Single(services, d => d.ServiceType == typeof(T));

    [Fact]
    public void Des_options_invalides_sont_refusees_a_l_enregistrement()
        => Assert.Throws<SentierException>(() => new ServiceCollection().AddSentier(o => o.UtiliserSqlite()));

    [Fact]
    public void Les_services_publics_se_resolvent()
    {
        using var fournisseur = Enregistrer().BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        using var portee = fournisseur.CreateScope();

        Assert.NotNull(portee.ServiceProvider.GetRequiredService<IServiceSentier>());
        Assert.NotNull(portee.ServiceProvider.GetRequiredService<ISuiviSentier>());
        Assert.NotNull(portee.ServiceProvider.GetRequiredService<InstallateurSchemaSentier>());
    }

    [Fact]
    public void Le_dialecte_suit_les_options()
    {
        using var sqlite = Enregistrer().BuildServiceProvider();
        using var oracle = Enregistrer(o => o.UtiliserOracle()).BuildServiceProvider();

        Assert.IsType<DialecteSqlite>(sqlite.GetRequiredService<IDialecteSql>());
        Assert.IsType<DialecteOracle>(oracle.GetRequiredService<IDialecteSql>());
    }

    [Fact]
    public void Les_noms_d_objets_portent_le_prefixe()
    {
        using var fournisseur = Enregistrer(o => o.PrefixeTables = "wf").BuildServiceProvider();

        Assert.Equal("WF_INSTC_PROCS", fournisseur.GetRequiredService<NomsObjets>().Instance);
    }

    [Fact]
    public void Les_commandes_et_requetes_balayees_sont_enregistrees_en_scoped()
    {
        var services = Enregistrer();

        Assert.Equal(ServiceLifetime.Scoped, Unique<CommandeEnregistree>(services).Lifetime);
        Assert.Equal(ServiceLifetime.Scoped, Unique<CommandeNommee>(services).Lifetime);
        Assert.Equal(ServiceLifetime.Scoped, Unique<RequeteEnregistree>(services).Lifetime);
    }

    [Fact]
    public void Le_registre_connait_les_commandes_par_leur_nom()
    {
        using var fournisseur = Enregistrer().BuildServiceProvider();
        var registre = fournisseur.GetRequiredService<RegistreSentier>();

        Assert.Equal(typeof(CommandeEnregistree), registre.TypeCommande(nameof(CommandeEnregistree)));
        Assert.Equal(typeof(CommandeNommee), registre.TypeCommande("nom-explicite"));
        Assert.Equal(typeof(RequeteEnregistree), registre.TypeRequete(nameof(RequeteEnregistree)));
        Assert.Contains(typeof(ProcessusDi), registre.Definitions);
        Assert.Null(registre.TypeCommande("inconnue"));
    }

    [Fact]
    public void Plusieurs_gestionnaires_de_taches_sans_choix_explicite_sont_refuses()
    {
        var ex = Assert.Throws<SentierException>(() => new ServiceCollection().AddSentier(o => o.UtiliserSqlite("S").BalayerAssemblyDe<ExtensionsSentierTests>()));

        Assert.Contains(nameof(GestionnaireTachesA), ex.Message);
        Assert.Contains(nameof(GestionnaireTachesB), ex.Message);
    }

    [Fact]
    public void Le_gestionnaire_de_taches_explicite_est_retenu()
    {
        var services = Enregistrer(o => o.UtiliserGestionnaireTaches<GestionnaireTachesB>());

        Assert.Equal(typeof(GestionnaireTachesB), Unique<IGestionnaireTachesSentier>(services).ImplementationType);
    }

    [Fact]
    public void Le_gestionnaire_deja_enregistre_par_le_client_est_conserve()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IGestionnaireTachesSentier, GestionnaireTachesB>();
        services.AddSentier(o => o.UtiliserSqlite("S").BalayerAssemblyDe<ExtensionsSentierTests>());

        var descripteur = Unique<IGestionnaireTachesSentier>(services);
        Assert.Equal(typeof(GestionnaireTachesB), descripteur.ImplementationType);
        Assert.Equal(ServiceLifetime.Singleton, descripteur.Lifetime);
    }

    [Fact]
    public void L_executeur_sql_par_defaut_est_dapper_et_peut_etre_remplace()
    {
        Assert.Equal(typeof(ExecuteurSqlDapper), Unique<IExecuteurSqlSentier>(Enregistrer()).ImplementationType);
        Assert.Equal(typeof(ExecuteurSqlClient), Unique<IExecuteurSqlSentier>(Enregistrer(o => o.UtiliserExecuteurSql<ExecuteurSqlClient>())).ImplementationType);
    }

    [Fact]
    public void La_session_peut_etre_remplacee()
    {
        Assert.Equal(typeof(SessionSentierParDefaut), Unique<ISessionSentier>(Enregistrer()).ImplementationType);
        Assert.Equal(typeof(SessionClient), Unique<ISessionSentier>(Enregistrer(o => o.UtiliserSession<SessionClient>())).ImplementationType);
    }

    [Fact]
    public void Une_horloge_enregistree_avant_AddSentier_est_conservee()
    {
        using var fournisseur = Enregistrer(avant: s => s.AddSingleton<IHorlogeSentier, HorlogeFixe>()).BuildServiceProvider();

        Assert.IsType<HorlogeFixe>(fournisseur.GetRequiredService<IHorlogeSentier>());
    }

    [Fact]
    public void L_horloge_par_defaut_est_l_horloge_systeme()
    {
        using var fournisseur = Enregistrer().BuildServiceProvider();

        Assert.IsType<HorlogeSystemeSentier>(fournisseur.GetRequiredService<IHorlogeSentier>());
    }

    [Fact]
    public void Sans_connexion_la_session_par_defaut_explique_quoi_enregistrer()
    {
        using var fournisseur = Enregistrer().BuildServiceProvider();
        using var portee = fournisseur.CreateScope();
        var session = portee.ServiceProvider.GetRequiredService<ISessionSentier>();

        var ex = Assert.Throws<SentierException>(() => session.Connexion);
        Assert.Contains("IDbConnection", ex.Message);
    }

    [Fact]
    public void Sans_transaction_une_ecriture_est_refusee()
    {
        using var fournisseur = Enregistrer().BuildServiceProvider();
        using var portee = fournisseur.CreateScope();
        var session = portee.ServiceProvider.GetRequiredService<ISessionSentier>();

        Assert.Throws<TransactionRequiseException>(() => session.ExigerTransaction());
    }

    [Fact]
    public void Appeler_AddSentier_deux_fois_ne_duplique_pas_les_executeurs_de_noeuds()
    {
        var services = Enregistrer();
        services.AddSentier(o => o.UtiliserSqlite("SENTIER").BalayerAssemblyDe<ExtensionsSentierTests>().UtiliserGestionnaireTaches<GestionnaireTachesA>());

        var executeurs = services.Where(d => d.ServiceType.Name == "IExecuteurNoeud").ToList();
        Assert.Equal(executeurs.Count, executeurs.Select(d => d.ImplementationType).Distinct().Count());
    }
}
