namespace ProcessManager.Tests.Exemple;

public interface IBusQueries
{
    Task<TResultat> Demander<TResultat>(object query);
}
