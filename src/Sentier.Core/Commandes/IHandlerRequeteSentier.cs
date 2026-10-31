namespace Sentier.Commandes;

/// <summary>Optionnel : code exécuté autour de chaque requête.</summary>
public interface IHandlerRequeteSentier
{
    /// <summary>Enveloppe l'évaluation ; appeler <paramref name="suivant"/> pour évaluer la requête.</summary>
    Task<bool> EvaluerAsync(string nomRequete, ContexteRequeteSentier contexte, Func<Task<bool>> suivant, CancellationToken ct);
}
