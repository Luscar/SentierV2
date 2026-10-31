using System.Collections.Concurrent;
using System.Data;
using System.Text.RegularExpressions;
using Dapper;

namespace Sentier.Persistance.Dapper;

/// <summary>
/// Accès commun des dépôts à la session du client, au dialecte et aux noms d'objets. Enregistré en Scoped.
/// La transaction est passée systématiquement à chaque requête ; les écritures l'exigent
/// (<see cref="IDbTransaction"/> du client, ou transaction ambiante : la commande part alors avec <c>transaction: null</c>).
/// </summary>
public sealed partial class ContexteSql
{
    private static readonly ConcurrentDictionary<(DialecteBase, string), string> Traductions = new();

    private readonly ISessionSentier _session;

    /// <summary>Crée le contexte.</summary>
    public ContexteSql(ISessionSentier session, IDialecteSql dialecte, NomsObjets noms)
    {
        _session = session;
        Dialecte = dialecte;
        Noms = noms;
        SentierDapper.EnregistrerGestionnairesTypes();
    }

    /// <summary>Dialecte SQL.</summary>
    public IDialecteSql Dialecte { get; }

    /// <summary>Noms des objets.</summary>
    public NomsObjets Noms { get; }

    /// <summary>Connexion du client.</summary>
    public IDbConnection Connexion => _session.Connexion;

    /// <summary>Commande de lecture (transaction courante si elle existe).</summary>
    public CommandDefinition Lecture(string sql, object? parametres, CancellationToken ct)
        => new(Traduire(sql), parametres, _session.Transaction, cancellationToken: ct);

    /// <summary>Commande d'écriture (transaction du client ou transaction ambiante obligatoire).</summary>
    public CommandDefinition Ecriture(string sql, object? parametres, CancellationToken ct)
        => new(Traduire(sql), parametres, TransactionEcriture(), cancellationToken: ct);

    /// <summary>Identifiant suivant d'une séquence.</summary>
    public Task<long> ProchainIdAsync(string nomSequence, CancellationToken ct)
        => Dialecte.ProchainIdentifiantAsync(Connexion, TransactionEcriture(), Noms, nomSequence, ct);

    // Transaction ambiante (null) acceptée seulement si le dialecte s'y enrôle ; sinon l'écriture serait validée immédiatement.
    private IDbTransaction? TransactionEcriture()
    {
        var transaction = _session.ExigerTransaction();
        if (transaction is null && !Dialecte.SupporteTransactionAmbiante)
        {
            throw new TransactionRequiseException(
                $"Le dialecte {Dialecte.Type} ne s'enrôle pas dans une transaction ambiante (TransactionScope) : fournir une IDbTransaction via ISessionSentier.");
        }

        return transaction;
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
