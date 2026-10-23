using Autofac;
using Autofac.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Sentier.DependencyInjection;

namespace Sentier.Autofac;

/// <summary>
/// Module Autofac de Sentier : enregistre les mêmes services que <see cref="ExtensionsSentier.AddSentier"/>
/// (Scoped → une instance par lifetime scope). N'enregistre jamais d'<see cref="System.Data.IDbConnection"/> :
/// le client la fournit dans chaque lifetime scope, avec sa transaction.
/// </summary>
/// <example>
/// <code>
/// builder.RegisterModule(new ModuleSentier(o => o
///     .UtiliserOracle("SENTIER")              // ou UtiliserSqlite("SENTIER") pour les tests
///     .BalayerAssemblyDe&lt;Program&gt;()));     // définitions, commandes, requêtes, gestionnaire de tâches
///
/// using var scope = container.BeginLifetimeScope(b =>
/// {
///     b.RegisterInstance(connexion).As&lt;IDbConnection&gt;().ExternallyOwned();
///     b.RegisterInstance(transaction).As&lt;IDbTransaction&gt;().ExternallyOwned();
/// });
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
