using Autofac;
using Autofac.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Sentier.DependencyInjection;

namespace Sentier.Autofac;

/// <summary>
/// Module Autofac de Sentier : enregistre les mêmes services que <see cref="ExtensionsSentier.AddSentier"/>
/// (Scoped → une instance par lifetime scope). N'enregistre jamais d'<see cref="System.Data.IDbConnection"/> :
/// le client la fournit dans chaque lifetime scope. La transaction est celle du client : un
/// <see cref="System.Transactions.TransactionScope"/> (Oracle, aucune <see cref="System.Data.IDbTransaction"/> à enregistrer)
/// ou une <see cref="System.Data.IDbTransaction"/> enregistrée avec la connexion.
/// </summary>
/// <example>
/// <code>
/// builder.RegisterModule(new ModuleSentier(o => o
///     .UtiliserOracle("SENTIER")              // ou UtiliserSqlite("SENTIER") pour les tests
///     .BalayerAssemblyDe&lt;Program&gt;()));     // définitions, commandes, requêtes, gestionnaire de tâches
///
/// // Oracle : connexion ouverte dans un TransactionScope, seule l'IDbConnection est enregistrée.
/// using var portee = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);
/// using var connexion = fabrique.Ouvrir();
/// using var scope = container.BeginLifetimeScope(b =>
///     b.RegisterInstance(connexion).As&lt;IDbConnection&gt;().ExternallyOwned());
///     // avec une IDbTransaction : b.RegisterInstance(transaction).As&lt;IDbTransaction&gt;().ExternallyOwned();
/// </code>
/// </example>
public sealed class ModuleSentier : Module
{
    private readonly Action<OptionsSentier> _configurer;

    /// <summary>Crée le module avec la configuration de Sentier.</summary>
    public ModuleSentier(Action<OptionsSentier> configurer)
    {
        _configurer = configurer;
    }

    /// <inheritdoc />
    protected override void Load(ContainerBuilder builder)
    {
        var services = new ServiceCollection();
        services.AddSentier(_configurer);
        builder.Populate(services);
    }
}
