namespace Sentier.Commandes;

/// <summary>Requête dont les paramètres sont liés à une classe, par nom de propriété.</summary>
/// <typeparam name="TParametres">Classe de paramètres.</typeparam>
public abstract class RequeteSentier<TParametres> : IRequeteSentier where TParametres : new()
{
    /// <inheritdoc />
    public Task<bool> EvaluerAsync(ContexteRequete contexte, CancellationToken ct)
        => EvaluerAsync(contexte, contexte.Parametres.Lier<TParametres>(), ct);

    /// <summary>Évalue la requête avec ses paramètres typés.</summary>
    protected abstract Task<bool> EvaluerAsync(ContexteRequete contexte, TParametres parametres, CancellationToken ct);
}
