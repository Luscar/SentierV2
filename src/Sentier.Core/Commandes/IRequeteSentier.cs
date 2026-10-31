namespace Sentier.Commandes;

/// <summary>
/// Requête booléenne en lecture seule, évaluée par une branche de nœud de décision.
/// Même enregistrement que les commandes.
/// </summary>
public interface IRequeteSentier
{
    /// <summary>Évalue la requête.</summary>
    Task<bool> EvaluerAsync(ContexteRequete contexte, CancellationToken ct);
}
