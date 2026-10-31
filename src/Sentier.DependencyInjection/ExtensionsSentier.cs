using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Sentier.Commandes;
using Sentier.Execution;
using Sentier.Execution.Executeurs;
using Sentier.Persistance;
using Sentier.Persistance.Dapper;
using Sentier.Persistance.Dapper.Depots;
using Sentier.Persistance.Oracle;
using Sentier.Persistance.Sqlite;
using Sentier.Services;
using Sentier.Variables;

namespace Sentier.DependencyInjection;

/// <summary>Enregistrement de Sentier dans l'injection de dépendances.</summary>
public static class ExtensionsSentier
{
    /// <summary>
    /// Enregistre Sentier : options, registre des commandes / requêtes / définitions / plans / gestionnaire de tâches
    /// (balayage d'assemblys),
    /// moteur, services, dépôts Dapper et dialecte. N'enregistre jamais d'<see cref="System.Data.IDbConnection"/> :
    /// la connexion est fournie par le client (Scoped, déjà ouverte).
    /// </summary>
    /// <example>
    /// <code>
    /// services.AddScoped&lt;IDbConnection&gt;(_ => OuvrirConnexion());
    /// services.AddSentier(o =>
    /// {
    ///     o.PrefixeTables = "SENTIER";
    ///     o.UtiliserOracle().BalayerAssemblyDe&lt;Program&gt;();   // trouve aussi l'IGestionnaireTachesSentier du client
    /// });
    /// </code>
    /// </example>
    public static IServiceCollection AddSentier(this IServiceCollection services, Action<OptionsSentier> configurer)
    {
        var options = new OptionsSentier();
        configurer(options);
        options.Valider();

        var registre = RegistreSentier.Balayer(options.Assemblys);
        SentierDapper.EnregistrerGestionnairesTypes();

        services.AddSingleton(options);
        services.AddSingleton(registre);
        services.AddSingleton(new NomsObjets(options.PrefixeTables));
        services.AddSingleton<IDialecteSql>(options.Dialecte == TypeBaseDonnees.Oracle ? new DialecteOracle() : new DialecteSqlite());
        services.TryAddSingleton<CacheDefinitions>();
        services.TryAddSingleton<IHorlogeSentier, HorlogeSystemeSentier>();
        services.TryAddSingleton<ISerialiseurVariablesSentier, SerialiseurVariablesJson>();

        // Connexion et transaction du client.
        services.TryAddScoped<IAccesseurTransactionSentier, AccesseurTransactionSentier>();
        if (options.TypeSession is not null)
        {
            services.AddScoped(typeof(ISessionSentier), options.TypeSession);
        }
        else
        {
            services.TryAddScoped<ISessionSentier, SessionSentierParDefaut>();
        }

        // Gestionnaire de tâches du client : explicite (remplace tout enregistrement), sinon déjà enregistré
        // par le client, sinon l'unique implémentation trouvée par balayage.
        if (options.TypeGestionnaireTaches is not null)
        {
            services.Replace(ServiceDescriptor.Scoped(typeof(IGestionnaireTachesSentier), options.TypeGestionnaireTaches));
        }
        else if (!services.Any(d => d.ServiceType == typeof(IGestionnaireTachesSentier)))
        {
            switch (registre.GestionnairesTaches.Count)
            {
                case 1:
                    services.AddScoped(typeof(IGestionnaireTachesSentier), registre.GestionnairesTaches[0]);
                    break;
                case > 1:
                    throw new SentierException(
                        $"Plusieurs implémentations de IGestionnaireTachesSentier ont été trouvées ({string.Join(", ", registre.GestionnairesTaches.Select(t => t.FullName))}) : "
                        + "choisir celle à utiliser avec UtiliserGestionnaireTaches<T>().");
            }
        }

        // Commandes et requêtes du client : Scoped, résolues par leur type concret.
        foreach (var type in registre.Commandes.Values.Concat(registre.Requetes.Values).Distinct())
        {
            services.TryAddScoped(type);
        }

        // Persistance : exécuteur SQL du client, sinon Dapper sur l'IDbConnection de la portée.
        if (options.TypeExecuteurSql is not null)
        {
            services.Replace(ServiceDescriptor.Scoped(typeof(IExecuteurSqlSentier), options.TypeExecuteurSql));
        }
        else
        {
            services.TryAddScoped<IExecuteurSqlSentier, ExecuteurSqlDapper>();
        }

        services.TryAddScoped<ContexteSql>();
        services.TryAddScoped<IDepotDefinitions, DepotDefinitionsDapper>();
        services.TryAddScoped<IDepotInstances, DepotInstancesDapper>();
        services.TryAddScoped<IDepotHistorique, DepotHistoriqueDapper>();
        services.TryAddScoped<IDepotAttentes, DepotAttentesDapper>();
        services.TryAddScoped<IDepotMigrations, DepotMigrationsDapper>();
        services.TryAddScoped<InstallateurSchemaSentier>();

        // Moteur.
        services.TryAddScoped<FournisseurDefinitions>();
        services.TryAddScoped<ExecuteurAppels>();
        services.TryAddScoped<PlanificateurAttentes>();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IExecuteurNoeud, ExecuteurDebut>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IExecuteurNoeud, ExecuteurSystemique>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IExecuteurNoeud, ExecuteurAttente>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IExecuteurNoeud, ExecuteurInteractif>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IExecuteurNoeud, ExecuteurDecision>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IExecuteurNoeud, ExecuteurSousProcessus>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IExecuteurNoeud, ExecuteurFin>());
        services.TryAddScoped<MoteurSentier>();

        // Services publics.
        services.TryAddScoped<DeployeurDefinitions>();
        services.TryAddScoped<ServiceMigration>();
        services.TryAddScoped<IServiceSentier, ServiceSentier>();
        services.TryAddScoped<ISuiviSentier, SuiviSentier>();

        return services;
    }
}
