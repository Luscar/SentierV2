using System.Collections.Concurrent;
using System.Data;
using System.Text.RegularExpressions;

namespace Sentier.Persistance;

/// <summary>
/// Accès commun des dépôts à l'exécuteur SQL, au dialecte et aux noms d'objets. Enregistré en Scoped.
/// Les écritures exigent une transaction active (<see cref="IDbTransaction"/> du client, ou transaction ambiante) ;
/// le SQL est traduit dans la syntaxe du dialecte puis confié à <see cref="IExecuteurSqlSentier"/>.
/// </summary>
public sealed partial class ContexteSql
{
    private static readonly ConcurrentDictionary<(TypeBaseDonnees, string), string> Traductions = new();

    private readonly ISessionSentier _session;
    private readonly IExecuteurSqlSentier _executeur;

    /// <summary>Crée le contexte.</summary>
    public ContexteSql(ISessionSentier session, IExecuteurSqlSentier executeur, IDialecteSql dialecte, NomsObjets noms)
    {
        _session = session;
        _executeur = executeur;
        Dialecte = dialecte;
        Noms = noms;
    }

    /// <summary>Dialecte SQL.</summary>
    public IDialecteSql Dialecte { get; }

    /// <summary>Noms des objets.</summary>
    public NomsObjets Noms { get; }

    /// <summary>Écriture (INSERT, UPDATE, DELETE) : transaction obligatoire.</summary>
    public Task<int> ExecuterAsync(string sql, object? parametres, CancellationToken ct)
    {
        ExigerTransactionEcriture();
        return _executeur.ExecuterAsync(Traduire(sql), parametres, ct);
    }

    /// <summary>Lecture de toutes les lignes (transaction facultative).</summary>
    public Task<IEnumerable<T>> LireAsync<T>(string sql, object? parametres, CancellationToken ct)
        => _executeur.LireAsync<T>(Traduire(sql), parametres, ct);

    /// <summary>Lecture de la première ligne (transaction facultative).</summary>
    public Task<T?> LirePremierAsync<T>(string sql, object? parametres, CancellationToken ct)
        => _executeur.LirePremierOuDefautAsync<T>(Traduire(sql), parametres, ct);

    /// <summary>Lecture de la première ligne avec verrouillage (<c>FOR UPDATE</c>) : transaction obligatoire.</summary>
    public Task<T?> VerrouillerPremierAsync<T>(string sql, object? parametres, CancellationToken ct)
    {
        ExigerTransactionEcriture();
        return _executeur.LirePremierOuDefautAsync<T>(Traduire(sql), parametres, ct);
    }

    /// <summary>Lecture d'une valeur (transaction facultative).</summary>
    public Task<T?> LireScalaireAsync<T>(string sql, object? parametres, CancellationToken ct)
        => _executeur.ExecuterScalaireAsync<T>(Traduire(sql), parametres, ct);

    /// <summary>Identifiant suivant d'une séquence.</summary>
    public Task<long> ProchainIdAsync(string nomSequence, CancellationToken ct)
    {
        ExigerTransactionEcriture();
        return Dialecte.ProchainIdentifiantAsync(_executeur, Noms, nomSequence, ct);
    }

    // Transaction ambiante (null) acceptée seulement si le dialecte s'y enrôle ; sinon l'écriture serait validée immédiatement.
    private void ExigerTransactionEcriture()
    {
        var transaction = _session.ExigerTransaction();
        if (transaction is null && !Dialecte.SupporteTransactionAmbiante)
        {
            throw new TransactionRequiseException(
                $"Le dialecte {Dialecte.Type} ne s'enrôle pas dans une transaction ambiante (TransactionScope) : fournir une IDbTransaction via ISessionSentier.");
        }
    }

    /// <summary>Booléen stocké en nombre (<c>NUMBER(1)</c> / <c>INTEGER</c>).</summary>
    public static int Booleen(bool valeur) => valeur ? 1 : 0;

    /// <summary>Date relue de la base, marquée UTC.</summary>
    public static DateTime Utc(DateTime date) => DateTime.SpecifyKind(date, DateTimeKind.Utc);

    /// <summary>Date optionnelle relue de la base, marquée UTC.</summary>
    public static DateTime? Utc(DateTime? date) => date is null ? null : Utc(date.Value);

    /// <summary>Traduit les paramètres <c>@nom</c> dans la syntaxe du dialecte.</summary>
    public string Traduire(string sql)
        => Traductions.GetOrAdd((Dialecte.Type, sql), cle => RegexParametre().Replace(cle.Item2, m => Dialecte.Parametre(m.Groups[1].Value)));

    [GeneratedRegex(@"(?<![@\w])@(\w+)")]
    private static partial Regex RegexParametre();
}
