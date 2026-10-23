using System.Data;
using Autofac;
using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Sentier.Autofac;
using Sentier.Persistance.Dapper;
using Sentier.Tests.Integration.Processus;
using Sentier.Tests.Integration.Support;
using Sentier.Variables;

namespace Sentier.Tests.Integration;

/// <summary>Installation du schéma, premier déploiement et intégration par Autofac, sur une base réelle.</summary>
public sealed class SchemaEtConfigurationTests
{
    [Fact]
    public async Task Le_schema_est_installe_avec_le_prefixe_et_reinstallable()
    {
        using var hote = await HoteSqlite.DemarrerAsync(deployer: false);

        var objets = hote.Connexion.Query<(string Type, string Nom)>("SELECT type, name FROM sqlite_master WHERE name LIKE 'SENTIER%' ORDER BY name").ToList();
        Assert.Contains(("table", "SENTIER_DEFIN_PROCS"), objets);
        Assert.Contains(("table", "SENTIER_INSTC_PROCS"), objets);
        Assert.Contains(("table", "SENTIER_HIST_NOEUD_PROCS"), objets);
        Assert.Contains(("table", "SENTIER_ATTEN_PROCS"), objets);
        Assert.Contains(("table", "SENTIER_MIGRA_PROCS"), objets);
        Assert.Contains(("table", "SENTIER_SEQ"), objets);
        Assert.Contains(("view", "SENTIER_V_INSTC_ACTIV"), objets);

        await hote.EnTransactionAsync(s => s.GetRequiredService<InstallateurSchemaSentier>().InstallerAsync());
        Assert.Equal(5, hote.Compter("_SEQ"));

        var script = await hote.EnTransactionAsync(s => Task.FromResult(s.GetRequiredService<InstallateurSchemaSentier>().GenererScript()));
        Assert.Contains("CREATE TABLE IF NOT EXISTS SENTIER_INSTC_PROCS", script);
        Assert.DoesNotContain("{{P}}", script);
    }

    [Fact]
    public async Task Premier_deploiement_puis_deploiement_inchange()
    {
        using var hote = await HoteSqlite.DemarrerAsync(deployer: false);

        var premier = await hote.Sentier(s => s.DeployerDefinitionsAsync());
        var second = await hote.Sentier(s => s.DeployerDefinitionsAsync());

        Assert.Equal(["abonnement", "archivage-conge", "demande-conge"], premier.Definitions.Select(d => d.CleProcessus).Order());
        Assert.All(premier.Definitions, d => Assert.Equal((EtatDeploiement.NouvelleVersion, 1), (d.Etat, d.Version)));
        Assert.All(second.Definitions, d => Assert.Equal(EtatDeploiement.Inchangee, d.Etat));
        Assert.Equal(3, hote.Compter("_DEFIN_PROCS"));
        Assert.Equal("Demande de congé", hote.Connexion.ExecuteScalar<string>("SELECT NOM_DEFIN FROM SENTIER_DEFIN_PROCS WHERE CLE = 'demande-conge'"));
    }

    [Fact]
    public async Task Avant_le_deploiement_aucun_processus_ne_demarre()
    {
        using var hote = await HoteSqlite.DemarrerAsync(deployer: false);
        await Assert.ThrowsAsync<ProcessusIntrouvableException>(() => hote.Sentier(s => s.DemarrerAsync("demande-conge", 1, ValeursVariables.Avec(DemandeConge.NbJours, 1L))));
    }

    [Fact]
    public async Task Parcours_avec_le_module_autofac()
    {
        using var connexion = new SqliteConnection("Data Source=:memory:");
        connexion.Open();

        var builder = new ContainerBuilder();
        builder.RegisterModule(new ModuleSentier(o => o.UtiliserSqlite("AUTOFAC").BalayerAssemblyDe<HoteSqlite>()));
        builder.RegisterInstance(new HorlogeSimulee()).As<IHorloge>();
        builder.RegisterInstance(new Pannes());
        builder.RegisterInstance(new VersionsProcessus());
        using var conteneur = builder.Build();

        // Le client ouvre sa transaction et l'enregistre dans la portée, avec sa connexion.
        async Task<T> EnTransaction<T>(Func<ILifetimeScope, Task<T>> action)
        {
            using var transaction = connexion.BeginTransaction();
            await using var portee = conteneur.BeginLifetimeScope(b =>
            {
                b.RegisterInstance(connexion).As<IDbConnection>().ExternallyOwned();
                b.RegisterInstance(transaction).As<IDbTransaction>().ExternallyOwned();
            });
            var resultat = await action(portee);
            transaction.Commit();
            return resultat;
        }

        await EnTransaction(async s =>
        {
            await s.Resolve<InstallateurSchemaSentier>().InstallerAsync();
            await SchemaClient.InstallerAsync(connexion, s.Resolve<IDbTransaction>());
            await connexion.ExecuteAsync("INSERT INTO CLIENT_EMPLOYE (ID, NOM, SOLDE) VALUES (1, 'Carole', 3)", transaction: s.Resolve<IDbTransaction>());
            return await s.Resolve<IServiceSentier>().DeployerDefinitionsAsync();
        });

        var demarrage = await EnTransaction(s => s.Resolve<IServiceSentier>().DemarrerAsync("demande-conge", 1, ValeursVariables.Avec(DemandeConge.NbJours, 2L)));
        Assert.Equal("approbation", demarrage.CodeNoeudCourant);
        Assert.Equal("OUVERTE", GestionnaireTachesSql.Lire(connexion, demarrage.IdTacheCourante!.Value).Statut);

        var refus = await EnTransaction(s => s.Resolve<IServiceSentier>().CompleterTacheAsync(demarrage.IdTacheCourante!.Value, new Dictionary<string, object?> { ["decision"] = "non" }, "chef"));
        Assert.Equal("refusee-gestionnaire", refus.CodeFin);
        Assert.Equal(1, connexion.ExecuteScalar<long>("SELECT COUNT(*) FROM AUTOFAC_INSTC_PROCS WHERE STAT = 'TERMINEE'"));
    }
}
