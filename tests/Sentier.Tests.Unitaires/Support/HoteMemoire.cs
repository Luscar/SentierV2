using System.Data;
using Microsoft.Extensions.DependencyInjection;
using Sentier.Definitions;
using Sentier.DependencyInjection;
using Sentier.Persistance;

namespace Sentier.Tests.Unitaires.Support;

/// <summary>
/// Sentier complet (moteur, services, exécuteurs, migration) branché sur des dépôts en mémoire :
/// aucune base de données, une portée et une transaction factice par appel.
/// </summary>
public sealed class HoteMemoire : IDisposable
{
    private readonly ServiceProvider _fournisseur;
    private readonly ConnexionFactice _connexion = new();

    public HoteMemoire(Action<IServiceCollection>? services = null, Action<OptionsSentier>? options = null, Action<IServiceCollection>? apresSentier = null)
    {
        var collection = new ServiceCollection();
        collection.AddSingleton<IDbConnection>(_connexion);
        collection.AddSingleton<IDepotDefinitions>(Base.Definitions);
        collection.AddSingleton<IDepotInstances>(Base.Instances);
        collection.AddSingleton<IDepotHistorique>(Base.Historique);
        collection.AddSingleton<IDepotAttentes>(Base.Attentes);
        collection.AddSingleton<IDepotMigrations>(Base.Migrations);
        collection.AddSingleton<IHorloge>(Horloge);
        collection.AddSingleton(Bassin);
        collection.AddSingleton(Journal);
        collection.AddSingleton(Interrupteurs);
        collection.AddSingleton(Configuration);
        services?.Invoke(collection);

        collection.AddSentier(o =>
        {
            o.UtiliserSqlite("TEST").BalayerAssemblyDe<HoteMemoire>().UtiliserGestionnaireTaches<GestionnaireTachesTest>();
            options?.Invoke(o);
        });
        apresSentier?.Invoke(collection);

        _fournisseur = collection.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
    }

    public BaseMemoire Base { get; } = new();

    public HorlogeTest Horloge { get; } = new();

    public BassinTaches Bassin { get; } = new();

    public Journal Journal { get; } = new();

    public Interrupteurs Interrupteurs { get; } = new();

    public ConfigurationDynamique Configuration { get; } = new();

    /// <summary>Publie une définition construite en mémoire, comme si elle avait été déployée.</summary>
    public DefinitionStockee Publier(DefinitionProcessus definition) => Base.Definitions.Publier(definition, Horloge.MaintenantUtc);

    /// <summary>
    /// Exécute une action dans sa propre portée, avec une transaction active. Si l'action échoue,
    /// la base en mémoire est restaurée, comme l'annulation de la transaction du client.
    /// </summary>
    public async Task<T> ExecuterAsync<T>(Func<IServiceProvider, Task<T>> action)
    {
        var annuler = Base.Sauvegarder();
        using var portee = _fournisseur.CreateScope();
        using var transaction = _connexion.BeginTransaction();
        portee.ServiceProvider.GetRequiredService<IAccesseurTransaction>().Transaction = transaction;
        try
        {
            var resultat = await action(portee.ServiceProvider);
            transaction.Commit();
            return resultat;
        }
        catch
        {
            transaction.Rollback();
            annuler();
            throw;
        }
    }

    /// <summary>Exécute une action sur <see cref="IServiceSentier"/> dans une transaction.</summary>
    public Task<T> Sentier<T>(Func<IServiceSentier, Task<T>> action)
        => ExecuterAsync(s => action(s.GetRequiredService<IServiceSentier>()));

    /// <summary>Lecture par <see cref="ISuiviSentier"/>, sans transaction.</summary>
    public async Task<T> Suivi<T>(Func<ISuiviSentier, Task<T>> action)
    {
        using var portee = _fournisseur.CreateScope();
        return await action(portee.ServiceProvider.GetRequiredService<ISuiviSentier>());
    }

    /// <summary>Portée brute, sans transaction (pour vérifier les erreurs de transaction).</summary>
    public IServiceScope CreerPortee() => _fournisseur.CreateScope();

    public Task<ResultatExecution> DemarrerAsync(string cle, long cleAffaire, IReadOnlyDictionary<string, object?>? variables = null)
        => Sentier(s => s.DemarrerAsync(cle, cleAffaire, variables));

    public async Task<EtatInstance> InstanceAsync(IdInstance? id)
        => (await Suivi(s => s.ObtenirInstanceAsync(id!.Value)))!;

    public void Dispose() => _fournisseur.Dispose();
}
