using System.Data;
using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Sentier.DependencyInjection;
using Sentier.Persistance;

namespace Sentier.Integration.Tests.Infrastructure;

/// <summary>
/// Application cliente complète pour un test : base SQLite en mémoire (schéma Sentier + table des tâches),
/// conteneur réel (<c>AddSentier</c>), horloge et services métier simulés. Chaque appel passe par une unité de
/// travail : une portée DI + une transaction validée en fin d'action, ou annulée si l'action lève une exception.
/// </summary>
public sealed class EnvironnementSentier : IAsyncDisposable
{
    public const string Prefixe = "SENTIER";

    private readonly SqliteConnection _connexion;
    private ServiceProvider _fournisseur;

    private EnvironnementSentier(SqliteConnection connexion, ServiceProvider fournisseur)
    {
        _connexion = connexion;
        _fournisseur = fournisseur;
    }

    public HorlogeTest Horloge { get; } = new();

    public ServicesMetier Metier { get; } = new();

    /// <summary>Configuration lue par les définitions qui changent d'une version à l'autre.</summary>
    public ConfigurationProcessus Configuration { get; } = new();

    /// <summary>Crée la base, installe le schéma et déploie toutes les définitions de l'assembly de tests.</summary>
    public static async Task<EnvironnementSentier> DemarrerAsync()
    {
        var connexion = new SqliteConnection($"Data Source=file:{Guid.NewGuid():N}?mode=memory&cache=private");
        connexion.Open();

        var environnement = new EnvironnementSentier(connexion, null!);
        environnement._fournisseur = environnement.Construire();

        await environnement.ExecuterAsync(async s =>
        {
            await s.GetRequiredService<InstallateurSchemaSentier>().InstallerAsync();
            await s.GetRequiredService<ISessionSentier>().Connexion.ExecuteAsync(GestionnaireTachesSql.ScriptCreation, transaction: s.GetRequiredService<ISessionSentier>().Transaction);
        });
        await environnement.DeployerAsync();
        return environnement;
    }

    /// <summary>
    /// Simule un redémarrage de l'application : nouveau conteneur (caches vides), même base.
    /// Sert à déployer une nouvelle version des définitions.
    /// </summary>
    public async Task RedemarrerAsync()
    {
        await _fournisseur.DisposeAsync();
        _fournisseur = Construire();
    }

    public Task<RapportDeploiement> DeployerAsync() => ExecuterAsync(s => s.Service().DeployerDefinitionsAsync());

    /// <summary>Unité de travail : portée DI + transaction, validée si l'action réussit, annulée sinon.</summary>
    public async Task<T> ExecuterAsync<T>(Func<IServiceProvider, Task<T>> action)
    {
        await using var portee = _fournisseur.CreateAsyncScope();
        using var transaction = _connexion.BeginTransaction();
        portee.ServiceProvider.GetRequiredService<IAccesseurTransactionSentier>().Transaction = transaction;

        T resultat;
        try
        {
            resultat = await action(portee.ServiceProvider);
        }
        catch
        {
            transaction.Rollback();
            throw;
        }

        transaction.Commit();
        return resultat;
    }

    public Task ExecuterAsync(Func<IServiceProvider, Task> action) => ExecuterAsync(async s => { await action(s); return 0; });

    /// <summary>Lecture sans transaction (le suivi n'en exige pas).</summary>
    public async Task<T> LireAsync<T>(Func<IServiceProvider, Task<T>> lecture)
    {
        await using var portee = _fournisseur.CreateAsyncScope();
        return await lecture(portee.ServiceProvider);
    }

    /// <summary>Portée sans transaction, pour vérifier que Sentier refuse d'écrire.</summary>
    public async Task SansTransactionAsync(Func<IServiceProvider, Task> action)
    {
        await using var portee = _fournisseur.CreateAsyncScope();
        await action(portee.ServiceProvider);
    }

    // Raccourcis des appels les plus courants des tests.

    public Task<ResultatExecution> DemarrerAsync(string cleProcessus, long cleAffaire, IReadOnlyDictionary<string, object?>? variables = null)
        => ExecuterAsync(s => s.Service().DemarrerAsync(cleProcessus, cleAffaire, variables));

    public Task<ResultatExecution> CompleterTacheAsync(long idTache, IReadOnlyDictionary<string, object?>? donnees = null, string? utilisateur = "agent")
        => ExecuterAsync(s => s.Service().CompleterTacheAsync(idTache, donnees, utilisateur));

    public Task<ResultatSignal> EnvoyerSignalAsync(string nomSignal, string cleProcessus, long cleAffaire, IReadOnlyDictionary<string, object?>? chargeUtile = null)
        => ExecuterAsync(s => s.Service().EnvoyerSignalAsync(nomSignal, cleProcessus, cleAffaire, chargeUtile));

    public Task<EtatInstance> InstanceAsync(IdInstance id)
        => LireAsync(async s => await s.Suivi().ObtenirInstanceAsync(id) ?? throw new InvalidOperationException($"Instance {id} introuvable."));

    public Task<IReadOnlyList<PassageNoeud>> HistoriqueAsync(IdInstance id) => LireAsync(s => s.Suivi().ObtenirHistoriqueAsync(id));

    public Task<GestionnaireTachesSql.LigneTache?> TacheAsync(long idTache)
        => LireAsync(s => ((GestionnaireTachesSql)s.GetRequiredService<IGestionnaireTachesSentier>()).LireLigneAsync(idTache));

    public Task<IReadOnlyList<GestionnaireTachesSql.LigneTache>> TachesAsync(IdInstance id)
        => LireAsync(s => ((GestionnaireTachesSql)s.GetRequiredService<IGestionnaireTachesSentier>()).LireLignesAsync(id));

    /// <summary>Le batch des échéances du client : chaque échéance dans sa propre transaction ; une erreur marque l'instance.</summary>
    public async Task<IReadOnlyList<ResultatExecution>> TraiterEcheancesAsync()
    {
        var echeances = await LireAsync(s => s.Service().ObtenirEcheancesDuesAsync());
        var resultats = new List<ResultatExecution>();
        foreach (var echeance in echeances)
        {
            try
            {
                resultats.Add(await ExecuterAsync(s => s.Service().TraiterEcheanceAsync(echeance.IdAttente)));
            }
            catch (Exception ex)
            {
                await ExecuterAsync(s => s.Service().MarquerEnErreurAsync(echeance.IdInstance, ex.Message));
            }
        }

        return resultats;
    }

    public async ValueTask DisposeAsync()
    {
        await _fournisseur.DisposeAsync();
        await _connexion.DisposeAsync();
    }

    private ServiceProvider Construire()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IDbConnection>(_connexion);
        services.AddSingleton<IHorlogeSentier>(Horloge);
        services.AddSingleton(Metier);
        services.AddSingleton(Configuration);
        services.AddScoped<IGestionnaireTachesSentier, GestionnaireTachesSql>();
        services.AddSentier(o =>
        {
            o.UtiliserSqlite(Prefixe).BalayerAssemblyDe<EnvironnementSentier>();
            o.MaxEtapesParExecution = 200;
        });
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = false });
    }
}

/// <summary>Réglages qui font varier une définition entre deux déploiements.</summary>
public sealed class ConfigurationProcessus
{
    public bool EtapeControleAjoutee { get; set; }
}

public static class ExtensionsFournisseur
{
    public static IServiceSentier Service(this IServiceProvider services) => services.GetRequiredService<IServiceSentier>();

    public static ISuiviSentier Suivi(this IServiceProvider services) => services.GetRequiredService<ISuiviSentier>();
}
