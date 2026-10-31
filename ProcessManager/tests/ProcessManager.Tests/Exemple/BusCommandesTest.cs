namespace ProcessManager.Tests.Exemple;

// Bus applicatifs simulés : le moteur ne les connaît pas, seuls les processus les utilisent.

public sealed class BusCommandesTest : IBusCommandes
{
    public List<object> Envoyees { get; } = [];

    public Task Envoyer(object commande)
    {
        Envoyees.Add(commande);
        return Task.CompletedTask;
    }
}
