using System.Data;
using System.Diagnostics.CodeAnalysis;

namespace Sentier.Tests.Unitaires.Support;

/// <summary>Horloge déplacée à la main.</summary>
public sealed class HorlogeTest : IHorloge
{
    public static readonly DateTime Origine = new(2026, 3, 2, 9, 0, 0, DateTimeKind.Utc);

    public DateTime MaintenantUtc { get; set; } = Origine;

    public void Avancer(TimeSpan duree) => MaintenantUtc += duree;
}

/// <summary>Utilisateur courant fixe.</summary>
public sealed class ContexteUtilisateurTest(string? utilisateur) : IContexteUtilisateur
{
    public string? Utilisateur { get; } = utilisateur;
}

/// <summary>Connexion factice : les dépôts en mémoire ne s'en servent jamais.</summary>
public sealed class ConnexionFactice : IDbConnection
{
    [AllowNull]
    public string ConnectionString { get; set; } = "";

    public int ConnectionTimeout => 0;

    public string Database => "memoire";

    public ConnectionState State => ConnectionState.Open;

    public IDbTransaction BeginTransaction() => new TransactionFactice(this);

    public IDbTransaction BeginTransaction(IsolationLevel il) => new TransactionFactice(this);

    public void ChangeDatabase(string databaseName) => throw new NotSupportedException();

    public void Close()
    {
    }

    public IDbCommand CreateCommand() => throw new NotSupportedException("Les tests unitaires n'exécutent aucun SQL.");

    public void Open()
    {
    }

    public void Dispose()
    {
    }
}

/// <summary>Transaction factice ; validée ou annulée, elle n'a plus de connexion (comme ADO.NET).</summary>
public sealed class TransactionFactice(IDbConnection connexion) : IDbTransaction
{
    public IDbConnection? Connection { get; private set; } = connexion;

    public IsolationLevel IsolationLevel => IsolationLevel.ReadCommitted;

    public void Commit() => Connection = null;

    public void Rollback() => Connection = null;

    public void Dispose() => Connection = null;
}

/// <summary>Journal des appels de commandes et de requêtes, partagé par l'hôte.</summary>
public sealed class Journal
{
    private readonly List<string> _entrees = [];

    public IReadOnlyList<string> Entrees
    {
        get
        {
            lock (_entrees)
            {
                return _entrees.ToList();
            }
        }
    }

    public void Ecrire(string entree)
    {
        lock (_entrees)
        {
            _entrees.Add(entree);
        }
    }
}

/// <summary>Interrupteurs partagés pour piloter les commandes de test.</summary>
public sealed class Interrupteurs
{
    /// <summary>Si vrai, la commande <see cref="CommandeInstable"/> échoue.</summary>
    public bool Echouer { get; set; }
}
