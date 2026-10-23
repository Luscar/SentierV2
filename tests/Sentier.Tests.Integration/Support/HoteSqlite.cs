using System.Data;
using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Sentier.DependencyInjection;
using Sentier.Persistance.Dapper;
using Sentier.Tests.Integration.Processus;

namespace Sentier.Tests.Integration.Support;

/// <summary>
/// Application cliente complète sur SQLite en mémoire : connexion du client, AddSentier, schéma installé,
/// définitions déployées. Chaque appel ouvre sa portée et sa transaction, comme une requête web ou un lot.
/// </summary>
public sealed class HoteSqlite : IDisposable
{
    public const string Prefixe = "SENTIER";

    private readonly ServiceProvider _fournisseur;

    private HoteSqlite(Action<IServiceCollection>? services)
    {
        Connexion = new SqliteConnection("Data Source=:memory:");
        Connexion.Open();
        Connexion.Execute("PRAGMA foreign_keys = ON");

        var collection = new ServiceCollection();
        collection.AddSingleton<IDbConnection>(Connexion);
        collection.AddSingleton<IHorloge>(Horloge);
        collection.AddSingleton(Pannes);
        collection.AddSingleton(Versions);
        services?.Invoke(collection);
        collection.AddSentier(o => o.UtiliserSqlite(Prefixe).BalayerAssemblyDe<HoteSqlite>());
        _fournisseur = collection.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    public SqliteConnection Connexion { get; }

    public HorlogeSimulee Horloge { get; } = new();

    public Pannes Pannes { get; } = new();

    public VersionsProcessus Versions { get; } = new();

    /// <summary>Crée l'hôte, installe le schéma de Sentier et celui du client, puis déploie les définitions.</summary>
    public static async Task<HoteSqlite> DemarrerAsync(Action<IServiceCollection>? services = null, bool deployer = true)
    {
        var hote = new HoteSqlite(services);
        await hote.EnTransactionAsync(async s =>
        {
            await s.GetRequiredService<InstallateurSchemaSentier>().InstallerAsync();
            await SchemaClient.InstallerAsync(hote.Connexion, s.GetRequiredService<IAccesseurTransaction>().Transaction!);
        });

        if (deployer)
        {
            await hote.Sentier(s => s.DeployerDefinitionsAsync());
        }

        return hote;
    }

    /// <summary>Exécute une action dans une portée et une transaction : validée si l'action réussit, annulée sinon.</summary>
    public async Task<T> EnTransactionAsync<T>(Func<IServiceProvider, Task<T>> action)
    {
        using var portee = _fournisseur.CreateScope();
        using var transaction = Connexion.BeginTransaction();
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
            throw;
        }
    }

    public Task EnTransactionAsync(Func<IServiceProvider, Task> action) => EnTransactionAsync(async s =>
    {
        await action(s);
        return true;
    });

    /// <summary>Exécute une action, puis annule volontairement la transaction (le client a échoué après l'appel à Sentier).</summary>
    public async Task<T> PuisAnnulerAsync<T>(Func<IServiceProvider, Task<T>> action)
    {
        using var portee = _fournisseur.CreateScope();
        using var transaction = Connexion.BeginTransaction();
        portee.ServiceProvider.GetRequiredService<IAccesseurTransaction>().Transaction = transaction;
        var resultat = await action(portee.ServiceProvider);
        transaction.Rollback();
        return resultat;
    }

    public Task<T> Sentier<T>(Func<IServiceSentier, Task<T>> action) => EnTransactionAsync(s => action(s.GetRequiredService<IServiceSentier>()));

    /// <summary>Portée sans transaction.</summary>
    public IServiceScope CreerPortee() => _fournisseur.CreateScope();

    /// <summary>Lecture sans transaction, comme un écran de suivi.</summary>
    public async Task<T> Suivi<T>(Func<ISuiviSentier, Task<T>> action)
    {
        using var portee = _fournisseur.CreateScope();
        return await action(portee.ServiceProvider.GetRequiredService<ISuiviSentier>());
    }

    public async Task<EtatInstance> InstanceAsync(IdInstance? id) => (await Suivi(s => s.ObtenirInstanceAsync(id!.Value)))!;

    /// <summary>
    /// Batch externe des échéances (README, section 8) : une transaction par échéance ; une erreur est marquée
    /// dans une transaction séparée et ne bloque que son instance.
    /// </summary>
    public async Task<int> ExecuterBatchEcheancesAsync()
    {
        IReadOnlyList<EcheanceDue> dues;
        using (var portee = _fournisseur.CreateScope())
        {
            dues = await portee.ServiceProvider.GetRequiredService<IServiceSentier>().ObtenirEcheancesDuesAsync(tailleMax: 200);
        }

        var traitees = 0;
        foreach (var echeance in dues)
        {
            try
            {
                if (!(await Sentier(s => s.TraiterEcheanceAsync(echeance.IdAttente))).SansEffet)
                {
                    traitees++;
                }
            }
            catch (Exception ex)
            {
                await Sentier(s => s.MarquerEnErreurAsync(echeance.IdInstance, ex.Message));
            }
        }

        return traitees;
    }

    /// <summary>Nombre de lignes d'une table de Sentier (sans préfixe : « _INSTC_PROCS »…).</summary>
    public long Compter(string suffixeTable, string? filtre = null)
        => Connexion.ExecuteScalar<long>($"SELECT COUNT(*) FROM {Prefixe}{suffixeTable}{(filtre is null ? "" : " WHERE " + filtre)}");

    public void Dispose()
    {
        _fournisseur.Dispose();
        Connexion.Dispose();
    }
}

/// <summary>Horloge déplacée à la main pour simuler le passage du temps.</summary>
public sealed class HorlogeSimulee : IHorloge
{
    public static readonly DateTime Origine = new(2026, 3, 2, 9, 0, 0, DateTimeKind.Utc);

    public DateTime MaintenantUtc { get; set; } = Origine;

    public void Avancer(TimeSpan duree) => MaintenantUtc += duree;
}

/// <summary>Pannes simulées des services externes du client.</summary>
public sealed class Pannes
{
    public bool Archivage { get; set; }

    public bool Relance { get; set; }
}
