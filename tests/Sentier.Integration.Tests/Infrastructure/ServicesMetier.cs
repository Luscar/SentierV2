using System.Collections.Concurrent;

namespace Sentier.Integration.Tests.Infrastructure;

/// <summary>
/// Services « métier » de l'application simulée, injectés dans les commandes et requêtes des processus de test.
/// Une instance par environnement : les tests restent isolés.
/// </summary>
public sealed class ServicesMetier
{
    /// <summary>Solde de congés par employé (clé d'affaire).</summary>
    public ConcurrentDictionary<long, long> Soldes { get; } = new();

    /// <summary>Appels reçus, dans l'ordre (« commande:cle »).</summary>
    public ConcurrentQueue<string> Journal { get; } = new();

    /// <summary>Quand vrai, la commande de relance échoue (panne d'un service externe).</summary>
    public bool RelanceEnPanne { get; set; }

    public void Noter(string evenement) => Journal.Enqueue(evenement);
}
