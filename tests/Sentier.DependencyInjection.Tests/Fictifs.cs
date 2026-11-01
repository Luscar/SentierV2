using Sentier.Commandes;
using Sentier.Definitions;

namespace Sentier.DependencyInjection.Tests;

public sealed class CommandeEnregistree : ICommandeSentier
{
    public Task ExecuterAsync(ContexteCommandeSentier contexte, CancellationToken ct) => Task.CompletedTask;
}

[CommandeSentier("nom-explicite")]
public sealed class CommandeNommee : ICommandeSentier
{
    public Task ExecuterAsync(ContexteCommandeSentier contexte, CancellationToken ct) => Task.CompletedTask;
}

public sealed class RequeteEnregistree : IRequeteSentier
{
    public Task<bool> EvaluerAsync(ContexteRequeteSentier contexte, CancellationToken ct) => Task.FromResult(true);
}

[ProcessusSentier("processus-di")]
public sealed class ProcessusDi : DefinitionSentier
{
    protected override void Definir(ConstructeurProcessus processus) => processus
        .Systemique<CommandeEnregistree>("a")
        .Fin("fin");
}

/// <summary>Deux gestionnaires de tâches dans l'assembly : le balayage seul ne peut pas choisir.</summary>
public class GestionnaireTachesA : IGestionnaireTachesSentier
{
    public Task<long> CreerTacheAsync(DemandeTacheSentier demande, CancellationToken ct = default) => Task.FromResult(1L);
    public Task<TacheSentier?> ObtenirTacheAsync(long idTache, CancellationToken ct = default) => Task.FromResult<TacheSentier?>(null);
    public Task<IReadOnlyList<TacheSentier>> ObtenirTachesOuvertesAsync(IdInstance idInstance, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<TacheSentier>>([]);
    public Task CompleterTacheAsync(long idTache, IReadOnlyDictionary<string, object?>? donnees, string? utilisateur, CancellationToken ct = default) => Task.CompletedTask;
    public Task AssignerTacheAsync(long idTache, string utilisateur, CancellationToken ct = default) => Task.CompletedTask;
    public Task AnnulerTacheAsync(long idTache, string motif, CancellationToken ct = default) => Task.CompletedTask;
    public Task RenommerNoeudAsync(IdInstance idInstance, string ancienCode, string nouveauCode, CancellationToken ct = default) => Task.CompletedTask;
}

public sealed class GestionnaireTachesB : GestionnaireTachesA;

public sealed class ExecuteurSqlClient : IExecuteurSqlSentier
{
    public Task<int> ExecuterAsync(string sql, object? parametres, CancellationToken ct) => Task.FromResult(0);
    public Task<T?> ExecuterScalaireAsync<T>(string sql, object? parametres, CancellationToken ct) => Task.FromResult<T?>(default);
    public Task<IEnumerable<T>> LireAsync<T>(string sql, object? parametres, CancellationToken ct) => Task.FromResult(Enumerable.Empty<T>());
    public Task<T?> LirePremierOuDefautAsync<T>(string sql, object? parametres, CancellationToken ct) => Task.FromResult<T?>(default);
}

public sealed class SessionClient : ISessionSentier
{
    public System.Data.IDbConnection Connexion => throw new NotSupportedException();
    public System.Data.IDbTransaction? Transaction => null;
}

public sealed class HorlogeFixe : IHorlogeSentier
{
    public DateTime MaintenantUtc => new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
}
