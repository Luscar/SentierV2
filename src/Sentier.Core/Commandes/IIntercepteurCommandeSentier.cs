namespace Sentier.Commandes;

/// <summary>Optionnel : code exécuté autour de chaque commande (journalisation, audit).</summary>
public interface IIntercepteurCommandeSentier
{
    /// <summary>Intercepte l'exécution ; appeler <paramref name="suivant"/> pour exécuter la commande.</summary>
    Task ExecuterAsync(string nomCommande, ContexteCommandeSentier contexte, Func<Task> suivant, CancellationToken ct);
}
