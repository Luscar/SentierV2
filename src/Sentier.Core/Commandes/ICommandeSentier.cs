namespace Sentier.Commandes;

/// <summary>
/// Commande métier exécutée par un nœud. Enregistrée en Scoped par balayage d'assemblys ;
/// elle peut injecter ses propres services et travaille dans la transaction du moteur.
/// </summary>
public interface ICommandeSentier
{
    /// <summary>Exécute la commande.</summary>
    Task ExecuterAsync(ContexteCommandeSentier contexte, CancellationToken ct);
}
