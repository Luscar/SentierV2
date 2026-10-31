namespace ProcessManager.Tests.Exemple;

public sealed class BusQueriesTest : IBusQueries
{
    public Func<object, object> Repondre { get; set; } = _ => throw new InvalidOperationException("Aucune réponse configurée.");

    public Task<TResultat> Demander<TResultat>(object query) => Task.FromResult((TResultat)Repondre(query));
}
