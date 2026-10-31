namespace Sentier.Commandes;

/// <summary>Commande dont les paramètres sont liés à une classe, par nom de propriété.</summary>
/// <typeparam name="TParametres">Classe de paramètres.</typeparam>
public abstract class CommandeSentier<TParametres> : ICommandeSentier where TParametres : new()
{
    /// <inheritdoc />
    public Task ExecuterAsync(ContexteCommandeSentier contexte, CancellationToken ct)
        => ExecuterAsync(contexte, contexte.Parametres.Lier<TParametres>(), ct);

    /// <summary>Exécute la commande avec ses paramètres typés.</summary>
    protected abstract Task ExecuterAsync(ContexteCommandeSentier contexte, TParametres parametres, CancellationToken ct);
}
