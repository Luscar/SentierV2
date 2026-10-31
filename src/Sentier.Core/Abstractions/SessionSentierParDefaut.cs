using System.Data;
using Microsoft.Extensions.DependencyInjection;

namespace Sentier;

/// <summary>
/// Implémentation par défaut de <see cref="ISessionSentier"/> : résout l'<see cref="IDbConnection"/>
/// enregistrée par le client dans la DI ; la transaction est celle de <see cref="IAccesseurTransactionSentier"/>,
/// sinon l'<see cref="IDbTransaction"/> enregistrée dans la portée, s'il y en a une. Sans transaction, Sentier
/// s'appuie sur la transaction ambiante (<see cref="System.Transactions.TransactionScope"/>, Oracle).
/// </summary>
public sealed class SessionSentierParDefaut : ISessionSentier
{
    private readonly IServiceProvider _services;
    private readonly IAccesseurTransactionSentier _accesseur;

    /// <summary>Crée la session.</summary>
    public SessionSentierParDefaut(IServiceProvider services, IAccesseurTransactionSentier accesseur)
    {
        _services = services;
        _accesseur = accesseur;
    }

    /// <inheritdoc />
    public IDbConnection Connexion => _services.GetService<IDbConnection>()
        ?? throw new SentierException("Aucune IDbConnection n'est enregistrée dans l'injection de dépendances du client (ou, pour passer par sa propre couche d'accès aux données, UtiliserExecuteurSql<T>()).");

    /// <inheritdoc />
    public IDbTransaction? Transaction => _accesseur.Transaction ?? _services.GetService<IDbTransaction>();
}
