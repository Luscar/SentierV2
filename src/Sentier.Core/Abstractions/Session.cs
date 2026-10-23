using System.Data;
using Microsoft.Extensions.DependencyInjection;

namespace Sentier;

/// <summary>
/// Fournit à Sentier la connexion et la transaction courantes du client.
/// Enregistrée en Scoped. Sentier n'ouvre, ne valide et n'annule jamais de transaction.
/// </summary>
public interface ISessionSentier
{
    /// <summary>Connexion du client, déjà ouverte.</summary>
    IDbConnection Connexion { get; }

    /// <summary>Transaction courante du client, ou <c>null</c> si aucune n'est active.</summary>
    IDbTransaction? Transaction { get; }
}

/// <summary>
/// Accesseur que le client alimente avec sa transaction courante, utilisé par
/// l'implémentation par défaut de <see cref="ISessionSentier"/>. Enregistré en Scoped.
/// </summary>
public interface IAccesseurTransaction
{
    /// <summary>Transaction courante du client.</summary>
    IDbTransaction? Transaction { get; set; }
}

/// <summary>Implémentation par défaut de <see cref="IAccesseurTransaction"/>.</summary>
public sealed class AccesseurTransaction : IAccesseurTransaction
{
    /// <inheritdoc />
    public IDbTransaction? Transaction { get; set; }
}

/// <summary>
/// Implémentation par défaut de <see cref="ISessionSentier"/> : résout l'<see cref="IDbConnection"/>
/// enregistrée par le client dans la DI ; la transaction est celle de <see cref="IAccesseurTransaction"/>,
/// sinon l'<see cref="IDbTransaction"/> enregistrée dans la portée, s'il y en a une.
/// </summary>
public sealed class SessionSentierParDefaut : ISessionSentier
{
    private readonly IServiceProvider _services;
    private readonly IAccesseurTransaction _accesseur;

    /// <summary>Crée la session.</summary>
    public SessionSentierParDefaut(IServiceProvider services, IAccesseurTransaction accesseur)
    {
        _services = services;
        _accesseur = accesseur;
    }

    /// <inheritdoc />
    public IDbConnection Connexion => _services.GetService<IDbConnection>()
        ?? throw new SentierException("Aucune IDbConnection n'est enregistrée dans l'injection de dépendances du client.");

    /// <inheritdoc />
    public IDbTransaction? Transaction => _accesseur.Transaction ?? _services.GetService<IDbTransaction>();
}

/// <summary>Méthodes utilitaires sur <see cref="ISessionSentier"/>.</summary>
public static class ExtensionsSessionSentier
{
    /// <summary>
    /// Retourne la transaction active, ou lève <see cref="TransactionRequiseException"/>.
    /// Une transaction validée ou annulée (sans connexion) n'est plus considérée comme active.
    /// </summary>
    public static IDbTransaction ExigerTransaction(this ISessionSentier session)
    {
        var transaction = session.Transaction;
        if (transaction is null || transaction.Connection is null)
        {
            throw new TransactionRequiseException();
        }

        return transaction;
    }
}
