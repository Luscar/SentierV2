# Intégrer Sentier dans une application cliente

Ce guide répond à deux questions :

1. **Qu'est-ce que le client doit obligatoirement faire ?** (liste de contrôle, puis exemple complet)
2. **À quoi servent les autres abstractions** (`IHorloge`, `IContexteUtilisateur`, intercepteurs…) et quand faut-il s'en occuper ?

En résumé : l'enregistrement tient en quelques lignes, et toutes les abstractions de la section 3 sont **facultatives**. Sentier fournit déjà une implémentation par défaut pour chacune.

- [1. Liste de contrôle](#1-liste-de-contrôle)
- [2. Exemple minimal complet](#2-exemple-minimal-complet)
- [3. Abstractions facultatives : à quoi elles servent](#3-abstractions-facultatives--à-quoi-elles-servent)
- [4. Pièges courants](#4-pièges-courants)

## 1. Liste de contrôle

### Toujours obligatoire

| # | À faire | Pourquoi |
|---|---|---|
| 1 | Enregistrer une `IDbConnection` **Scoped, déjà ouverte** | Sentier n'ouvre jamais de connexion : il utilise celle du client. |
| 2 | Appeler `AddSentier` avec `PrefixeTables`, le dialecte et l'assembly à balayer | Le balayage trouve les définitions, commandes, requêtes, plans de migration et le gestionnaire de tâches. |
| 3 | Créer le schéma une fois (`InstallateurSchemaSentier`, ou script remis au DBA) | Tables, vue et séquences préfixées. |
| 4 | Ouvrir une transaction pour chaque unité de travail et la donner à `IAccesseurTransaction` | Toute écriture sans transaction lève `TransactionRequiseException`. Le client valide (`Commit`) ou annule (`Rollback`) lui-même. |
| 5 | Appeler `DeployerDefinitionsAsync()` au démarrage, dans une transaction | Enregistre une nouvelle version d'une définition quand elle a changé. Un processus non déployé ne peut pas démarrer. |
| 6 | Écrire au moins une définition (classe `DefinitionSentier`) et les commandes et requêtes qu'elle utilise | Le déploiement échoue si une commande ou une requête référencée n'est pas trouvée. |

### Obligatoire selon les nœuds utilisés

| Si les définitions contiennent… | À faire |
|---|---|
| un nœud **`Interactif`** (tâche humaine) | Implémenter `IGestionnaireTaches`, sinon le déploiement échoue. Une seule implémentation est trouvée par balayage. S'il y en a plusieurs, choisir avec `o.UtiliserGestionnaireTaches<T>()`. |
| une **échéance** (`Echeance`, `AttendreDelai`) | Planifier un batch qui appelle `ObtenirEcheancesDuesAsync` puis `TraiterEcheanceAsync`. Sentier ne lance aucun thread (voir le README, section 8). |
| une attente de **signal** | Appeler `EnvoyerSignalAsync` (ou `EnvoyerSignalCorrelationAsync`) quand l'événement métier se produit. |

Rien d'autre n'est requis.

## 2. Exemple minimal complet

### Enregistrement (`Program.cs`)

```csharp
using System.Data;
using Oracle.ManagedDataAccess.Client;
using Sentier;
using Sentier.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

// (1) La connexion appartient au client : Scoped, ouverte.
builder.Services.AddScoped<IDbConnection>(_ =>
{
    var connexion = new OracleConnection(builder.Configuration.GetConnectionString("Oracle"));
    connexion.Open();
    return connexion;
});

// (2) Sentier.
builder.Services.AddSentier(o =>
{
    o.PrefixeTables = "SENTIER";
    o.UtiliserOracle().BalayerAssemblyDe<Program>();
});

var app = builder.Build();

// (3) + (5) Au démarrage : schéma (si besoin) et déploiement des définitions.
using (var scope = app.Services.CreateScope())
{
    var sp = scope.ServiceProvider;
    using var transaction = sp.GetRequiredService<IDbConnection>().BeginTransaction();
    sp.GetRequiredService<IAccesseurTransaction>().Transaction = transaction;

    // await sp.GetRequiredService<Sentier.Persistance.Dapper.InstallateurSchemaSentier>().InstallerAsync(); // SQLite / premier lancement
    await sp.GetRequiredService<IServiceSentier>().DeployerDefinitionsAsync();

    transaction.Commit();
}

app.Run();
```

### (4) Une unité de travail

Le plus simple est une petite méthode utilitaire, écrite une fois dans le client :

```csharp
public sealed class UniteDeTravail(IDbConnection connexion, IAccesseurTransaction accesseur)
{
    public async Task<T> ExecuterAsync<T>(Func<Task<T>> action)
    {
        using var transaction = connexion.BeginTransaction();
        accesseur.Transaction = transaction;
        try
        {
            var resultat = await action();
            transaction.Commit();
            return resultat;
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
        finally
        {
            accesseur.Transaction = null;
        }
    }
}

// builder.Services.AddScoped<UniteDeTravail>();
```

Utilisation dans un contrôleur ou un service :

```csharp
public sealed class ServiceConges(UniteDeTravail uow, IServiceSentier sentier)
{
    public Task<ResultatExecution> DemanderAsync(long idEmploye, long nbJours)
        => uow.ExecuterAsync(() => sentier.DemarrerAsync(
            "demande-conge", cleAffaire: idEmploye,
            ValeursVariables.Avec(DemandeConge.NbJours, nbJours)));

    public Task<ResultatExecution> ApprouverAsync(long idTache, string decision)
        => uow.ExecuterAsync(() => sentier.CompleterTacheAsync(
            idTache, new Dictionary<string, object?> { ["decision"] = decision }));
}
```

> Si l'application a déjà sa propre unité de travail (repository maison, EF Core…), il n'est pas nécessaire d'utiliser `IAccesseurTransaction` : voir `ISessionSentier`, section 3.

### (6) Définition, commande et requête

```csharp
[ProcessusSentier("demande-conge", Nom = "Demande de congé")]
public sealed class DemandeConge : DefinitionSentier
{
    public static readonly Variable<long> NbJours = Variable.Entier("nbJours").Obligatoire();
    public static readonly Variable<long> Solde = Variable.Entier("solde").ParDefaut(0);
    public static readonly Variable<string> Decision = Variable.Texte("decision");

    protected override void Definir(ConstructeurProcessus processus) => processus
        .Systemique<CalculerSolde>("calculer-solde")
        .Interactif("approbation", "Approuver la demande", i => i.Role("gestionnaire").Donnees(NbJours))
        .Decision("est-approuvee", d => d
            .Si<EstApprouvee>(p => p.Variable("decision", Decision))
            .Sinon("refusee"))
        .Fin("approuvee")
        .Fin("refusee");
}

// Commande : peut injecter les services du client, travaille dans la même transaction.
public sealed class CalculerSolde(IDepotEmployes employes) : ICommandeSentier
{
    public async Task ExecuterAsync(ContexteCommande contexte, CancellationToken ct)
        => contexte.Variables.Ecrire(DemandeConge.Solde, await employes.SoldeAsync(contexte.CleAffaire, ct));
}

// Requête : lecture seule, retourne un booléen.
public sealed class ParametresDecision { public string? Decision { get; set; } }

public sealed class EstApprouvee : RequeteSentier<ParametresDecision>
{
    protected override Task<bool> EvaluerAsync(ContexteRequete contexte, ParametresDecision p, CancellationToken ct)
        => Task.FromResult(p.Decision == "approuvee");
}
```

### Gestionnaire de tâches (nœud `Interactif` présent)

Les six méthodes doivent exister, mais elles se résument souvent à une ligne vers la table de tâches du client :

```csharp
public sealed class GestionnaireTaches(IDepotTaches depot) : IGestionnaireTaches
{
    public Task<long> CreerTacheAsync(DemandeTacheUtilisateur d, CancellationToken ct)
        => depot.InsererAsync(d.IdInstance.Valeur, d.CodeNoeud, d.Nom, d.Role, d.Utilisateur, ct);

    public Task<TacheUtilisateur?> ObtenirTacheAsync(long idTache, CancellationToken ct)
        => depot.ObtenirAsync(idTache, ct);

    public Task<IReadOnlyList<TacheUtilisateur>> ObtenirTachesOuvertesAsync(IdInstance idInstance, CancellationToken ct)
        => depot.OuvertesAsync(idInstance.Valeur, ct);

    // Idempotentes : peuvent être rappelées sur une tâche déjà complétée ou annulée.
    public Task CompleterTacheAsync(long idTache, IReadOnlyDictionary<string, object?>? donnees, string? utilisateur, CancellationToken ct)
        => depot.ChangerStatutAsync(idTache, StatutTache.Completee, utilisateur, ct);

    public Task AnnulerTacheAsync(long idTache, string motif, CancellationToken ct)
        => depot.ChangerStatutAsync(idTache, StatutTache.Annulee, motif, ct);

    // Appelée seulement par une migration qui renomme un nœud interactif.
    public Task RenommerNoeudAsync(IdInstance idInstance, string ancienCode, string nouveauCode, CancellationToken ct)
        => depot.RenommerNoeudAsync(idInstance.Valeur, ancienCode, nouveauCode, ct);
}
```

Elle est trouvée automatiquement par le balayage : aucun enregistrement à ajouter.

## 3. Abstractions facultatives : à quoi elles servent

Chacune a une implémentation par défaut (ou est simplement ignorée si elle est absente). On ne l'écrit que lorsqu'un besoin précis se présente.

| Abstraction | Défaut | À quoi elle sert | Quand l'implémenter |
|---|---|---|---|
| `IHorloge` | `HorlogeSysteme` (`DateTime.UtcNow`) | Unique source de « maintenant » du moteur : dates de début et de fin, calcul des échéances (`Echeance(TimeSpan.FromDays(2))`), échéances dues, historique. | **Tests** : figer ou avancer le temps pour vérifier qu'une échéance de 2 jours se déclenche, sans attendre 2 jours. Rarement utile en production. |
| `IContexteUtilisateur` | absent → utilisateur `null` | Donne le nom de l'utilisateur courant pour l'**audit** : historique des passages, `ContexteCommande.Utilisateur`, auteur d'une migration. Un paramètre `utilisateur` explicite (ex. `DemarrerAsync(…, utilisateur: "jdoe")`) a priorité. | Quand on veut savoir *qui* a fait avancer un processus sans passer l'utilisateur à chaque appel. Typiquement une ligne : `HttpContext.User.Identity?.Name`. |
| `IIntercepteurCommande` / `IIntercepteurRequete` | aucun | Code exécuté **autour** de chaque commande ou requête, comme un middleware : journalisation, mesure de durée, traçage OpenTelemetry, gestion d'erreurs commune. Plusieurs peuvent s'enchaîner. | Pour un comportement transversal à *toutes* les commandes, sans le répéter dans chacune. |
| `ISerialiseurVariables` | `SerialiseurVariablesJson` (System.Text.Json) | Convertit les variables d'une instance vers et depuis le JSON stocké en base. | Presque jamais : seulement pour imposer d'autres options JSON (convertisseurs, format des dates) aux variables `Json<T>`. **Ne pas changer** une fois des instances en base. |
| `ISessionSentier` (`o.UtiliserSession<T>()`) | `SessionSentierParDefaut` : connexion de la DI + transaction de `IAccesseurTransaction` | Indique à Sentier quelle connexion et quelle transaction utiliser. | Quand l'application a **déjà** une unité de travail qui porte la transaction : on la branche directement et `IAccesseurTransaction` n'est plus utilisé. |
| `o.UtiliserGestionnaireTaches<T>()` | implémentation trouvée par balayage | Choisit le gestionnaire de tâches explicitement. | Seulement s'il existe plusieurs implémentations, ou pour éviter de dépendre du balayage. |
| `o.MaxEtapesParExecution` | 1000 | Nombre maximal de nœuds exécutés en un seul appel : protège contre une boucle infinie dans une définition. | Rarement : une très longue boucle légitime. |

### Exemples courts

```csharp
// Audit : utilisateur courant depuis ASP.NET Core.
public sealed class UtilisateurHttp(IHttpContextAccessor http) : IContexteUtilisateur
{
    public string? Utilisateur => http.HttpContext?.User.Identity?.Name;
}
// builder.Services.AddHttpContextAccessor();
// builder.Services.AddScoped<IContexteUtilisateur, UtilisateurHttp>();

// Journalisation de toutes les commandes.
public sealed class JournalCommandes(ILogger<JournalCommandes> log) : IIntercepteurCommande
{
    public async Task ExecuterAsync(string nomCommande, ContexteCommande contexte, Func<Task> suivant, CancellationToken ct)
    {
        var chrono = Stopwatch.StartNew();
        await suivant();
        log.LogInformation("{Commande} sur l'instance {Id} : {Duree} ms", nomCommande, contexte.IdInstance, chrono.ElapsedMilliseconds);
    }
}
// builder.Services.AddScoped<IIntercepteurCommande, JournalCommandes>();

// Tests : horloge réglable.
public sealed class HorlogeTest : IHorloge
{
    public DateTime MaintenantUtc { get; set; } = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
}
// services.AddSingleton<IHorloge>(horlogeTest);   // avant AddSentier
// horlogeTest.MaintenantUtc = horlogeTest.MaintenantUtc.AddDays(3);  // puis lancer le batch des échéances

// Unité de travail existante.
public sealed class SessionUniteDeTravail(IUniteDeTravail uow) : ISessionSentier
{
    public IDbConnection Connexion => uow.Connexion;
    public IDbTransaction? Transaction => uow.TransactionCourante;
}
// o.UtiliserSession<SessionUniteDeTravail>();
```

Les services `IHorloge`, `ISerialiseurVariables` et `IAccesseurTransaction` sont enregistrés par Sentier avec `TryAdd` : un enregistrement du client fait **avant** `AddSentier` les remplace.

## 4. Pièges courants

- **Oublier de donner la transaction** à `IAccesseurTransaction` : `TransactionRequiseException` à la première écriture. Les lectures (`ISuiviSentier`) fonctionnent sans transaction.
- **Résoudre les services Sentier hors d'un scope** : la connexion et la transaction sont Scoped. Utiliser le même scope pour la transaction et pour `IServiceSentier`.
- **Avaler une exception et valider quand même** : Sentier propage toute erreur. C'est au client d'annuler sa transaction, sinon l'instance resterait à moitié avancée.
- **Changer une clé de processus, un code de nœud ou un nom de variable** alors que des instances existent : ce sont des contrats avec la base. Passer par un plan de migration (README, section 7).
- **Changer le préfixe des tables** après l'installation : Sentier ne trouverait plus ses tables.
- **Traiter un `SansEffet` comme une erreur** : c'est un résultat normal (tâche déjà complétée, signal sans destinataire, processus déjà actif…). Lire `Raison` pour décider.
