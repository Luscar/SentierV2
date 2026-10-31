namespace ProcessManager.Tests.Exemple;

public interface IBusCommandes
{
    Task Envoyer(object commande);
}
