# Guide de démarrage

> Tous les contrats à implémenter côté client et leur enregistrement sont résumés dans [Intégrer Sentier dans une application client](integration-client.md).

Ce guide mène une application cliente de l'installation au premier processus exécuté. Les détails sont dans le [README](../README.md) ; l'écriture des processus, dans le [guide du constructeur](constructeur.md).

- [1. Ce qu'il faut savoir avant de commencer](#1-ce-quil-faut-savoir-avant-de-commencer)
- [2. Installer les packages](#2-installer-les-packages)
- [3. Créer les tables](#3-créer-les-tables)
- [4. Enregistrer Sentier](#4-enregistrer-sentier)
- [5. Fournir la connexion et la transaction](#5-fournir-la-connexion-et-la-transaction)
- [6. Écrire un premier processus](#6-écrire-un-premier-processus)
- [7. Écrire les commandes et les requêtes](#7-écrire-les-commandes-et-les-requêtes)
- [8. Brancher le gestionnaire de tâches](#8-brancher-le-gestionnaire-de-tâches)
- [9. Déployer au démarrage](#9-déployer-au-démarrage)
- [10. Utiliser Sentier dans l'application](#10-utiliser-sentier-dans-lapplication)
- [11. Traiter les échéances (batch)](#11-traiter-les-échéances-batch)
- [12. Tester avec SQLite](#12-tester-avec-sqlite)
- [Liste de contrôle](#liste-de-contrôle)
- [Erreurs fréquentes](#erreurs-fréquentes)

## 1. Ce qu'il faut savoir avant de commencer

| Principe | Conséquence pour le client |
|---|---|
| **La transaction appartient au client.** Sentier n'ouvre, ne valide et n'annule jamais de transaction. | Chaque appel qui écrit se fait dans une transaction ouverte par l'application ; c'est elle qui fait `Commit` ou `Rollback`. |
| **Exécution synchrone.** Les nœuds s'enchaînent dans l'appel jusqu'à une fin, une attente, une tâche ou un sous-processus. | Au retour de `DemarrerAsync`, l'instance est déjà rendue à son prochain arrêt. |
| **Une instance = un processus + une clé d'affaire** (`long`). | Choisir la clé d'affaire de chaque processus (n° de dossier, de demande…). |
| **Le client garde ses tâches.** Sentier ne stocke aucune tâche utilisateur. | Implémenter `IGestionnaireTaches` dès qu'un processus a un nœud interactif. |
| **Aucun thread.** Sentier ne lance rien en arrière-plan. | Les échéances sont traitées par un batch du client. |

## 2. Installer les packages

| Package | Quand |
|---|---|
| `Sentier.Autofac` | Application sur **Autofac** (inclut `Sentier.DependencyInjection` et les dialectes). |
| `Sentier.DependencyInjection` | Application sur `Microsoft.Extensions.DependencyInjection`. |
| `Microsoft.Data.Sqlite` | Projet de tests. |

Le fournisseur Oracle (ODP.NET) est celui que l'application utilise déjà : Sentier ne travaille qu'avec `IDbConnection`.

## 3. Créer les tables

Choisir un **préfixe** (lettres, chiffres, `_`, 12 caractères au plus) : toutes les tables, vues et séquences le portent (`SENTIER_INSTC_PROCS`…).

- **Oracle** : remettre au DBA le script `src/Sentier.Persistance.Oracle/Scripts/oracle.sql` en remplaçant `{{P}}` par le préfixe, ou le générer avec le bon préfixe :

  ```csharp
  var script = scope.Resolve<InstallateurSchemaSentier>().GenererScript();
  ```

- **SQLite** : créer les tables directement (voir [§ 12](#12-tester-avec-sqlite)) :

  ```csharp
  await scope.Resolve<InstallateurSchemaSentier>().InstallerAsync();
  ```

## 4. Enregistrer Sentier

### Avec Autofac

```csharp
var builder = new ContainerBuilder();

builder.RegisterModule(new ModuleSentier(o => o
    .UtiliserOracle("SENTIER")              // préfixe des tables ; UtiliserSqlite("SENTIER") pour les tests
    .BalayerAssemblyDe<Program>()));        // définitions, commandes, requêtes, gestionnaire de tâches

// Services optionnels, enregistrés APRÈS le module pour remplacer ceux de Sentier :
builder.RegisterType<ContexteUtilisateurWeb>().As<IContexteUtilisateur>().InstancePerLifetimeScope();

_container = builder.Build();
```

### Avec Microsoft.Extensions.DependencyInjection

```csharp
services.AddScoped<IDbConnection>(_ => OuvrirConnexionOracle());
services.AddSentier(o => o
    .UtiliserOracle("SENTIER")
    .BalayerAssemblyDe<Program>());
```

### Ce qui est découvert par balayage

Dans les assemblys indiqués par `BalayerAssemblyDe<T>()` (à appeler une fois par assembly) :

- les définitions de processus (`DefinitionSentier` avec `[ProcessusSentier]`) ;
- les commandes (`ICommandeSentier`) et requêtes (`IRequeteSentier`), enregistrées en Scoped ;
- les plans de migration (`IPlanMigration`) ;
- l'implémentation de `IGestionnaireTaches` (s'il y en a plusieurs : `o.UtiliserGestionnaireTaches<T>()`).

Options utiles : `MaxEtapesParExecution` (1000 par défaut, protège des boucles infinies), `MigrationAutomatique()` (voir le README, § 7).

## 5. Fournir la connexion et la transaction

Sentier trouve la connexion et la transaction **dans la portée DI**. Il suffit d'y enregistrer les deux : aucun appel ne prend de paramètre de connexion.

Le plus simple est de centraliser ce code dans une petite classe de l'application, réutilisée partout (contrôleurs, batch, tests) :

```csharp
public sealed class UniteTravailSentier(ILifetimeScope container, IFabriqueConnexions fabrique)
{
    public async Task<T> ExecuterAsync<T>(Func<ILifetimeScope, Task<T>> action)
    {
        using var connexion = fabrique.Ouvrir();
        using var transaction = connexion.BeginTransaction();

        await using var scope = container.BeginLifetimeScope(b =>
        {
            b.RegisterInstance(connexion).As<IDbConnection>().ExternallyOwned();
            b.RegisterInstance(transaction).As<IDbTransaction>().ExternallyOwned();
        });

        var resultat = await action(scope);   // une exception remonte : la transaction n'est pas validée
        transaction.Commit();
        return resultat;
    }
}
```

Utilisation :

```csharp
var resultat = await _uniteTravail.ExecuterAsync(s =>
    s.Resolve<IServiceSentier>().DemarrerAsync("demande-conge", cleAffaire: 42));
```

Points à retenir :

- `ExternallyOwned()` : la portée ne doit pas disposer la connexion ni la transaction du client.
- Si l'application a déjà une unité de travail (même connexion pour ses propres dépôts), enregistrer **sa** connexion et **sa** transaction : les écritures de Sentier et de l'application sont alors validées ensemble.
- Dans une application web, ce code va dans un middleware ou un filtre qui crée la portée de chaque requête.
- Avec `Microsoft.Extensions.DependencyInjection`, alimenter plutôt `IAccesseurTransaction` dans la portée (voir le README, § 2).

### Variante Oracle : transaction ambiante (`TransactionScope`)

Si l'application n'utilise pas d'`IDbTransaction` (ses appels Dapper passent `transaction: null`) et rend ses
écritures atomiques avec un `TransactionScope`, il suffit d'enregistrer la connexion :

```csharp
using var portee = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);
using var connexion = fabrique.Ouvrir();          // ouverte DANS la portée : elle y est enrôlée

await using var scope = container.BeginLifetimeScope(b =>
    b.RegisterInstance(connexion).As<IDbConnection>().ExternallyOwned());

await scope.Resolve<IServiceSentier>().DemarrerAsync("demande-conge", cleAffaire: 42);
portee.Complete();
```

- Sans `IDbTransaction`, Sentier exécute ses requêtes avec `transaction: null` sur la connexion enrôlée.
- La connexion doit être ouverte **à l'intérieur** de la portée (sinon elle n'y est pas enrôlée), et
  `TransactionScopeAsyncFlowOption.Enabled` est nécessaire avec `async`/`await`.
- **Oracle seulement** : Microsoft.Data.Sqlite ne s'enrôle pas dans un `TransactionScope`. Avec SQLite, Sentier
  refuse d'écrire (`TransactionRequiseException`) plutôt que de valider chaque instruction immédiatement ;
  les tests SQLite fournissent une `IDbTransaction`.
- Sans `IDbTransaction` ni `TransactionScope`, Sentier refuse toujours d'écrire (`TransactionRequiseException`).

## 6. Écrire un premier processus

Une classe par processus. La clé (`"demande-conge"`) est celle utilisée pour démarrer une instance.

```csharp
[ProcessusSentier("demande-conge", Nom = "Demande de congé")]
public sealed class DemandeConge : DefinitionSentier
{
    public static readonly Variable<long> NbJours = Variable.Entier("nbJours").Obligatoire();
    public static readonly Variable<long> Solde = Variable.Entier("solde").ParDefaut(0);

    protected override void Definir(ConstructeurProcessus processus) => processus
        .Systemique<CalculerSolde>("calculer-solde")
        .Decision("solde-suffisant", d => d
            .Si<SoldeSuffisant>(p => p.Variables(NbJours, Solde))
            .Sinon(b => b.Fin("refusee")))
        .Interactif("approbation", "Approuver la demande", i => i.Role("gestionnaire").Donnees(NbJours))
        .Fin("approuvee");
}
```

Lecture : calculer le solde → si le solde suffit, créer une tâche d'approbation, sinon terminer à `refusee` → après l'approbation, terminer à `approuvee`.

Les nœuds disponibles : `Systemique` (une commande), `Decision` (requêtes), `Interactif` (tâche utilisateur), `Attente` (signal et/ou échéance), `SousProcessus`, `Fin`. Tout est détaillé dans le [guide du constructeur](constructeur.md).

## 7. Écrire les commandes et les requêtes

- **Commande** : toute écriture (base métier, variables du processus). Elle peut injecter les services de l'application et travaille dans la même transaction.
- **Requête** : évalue une branche de décision, en lecture seule.

```csharp
public sealed class CalculerSolde(IDepotEmployes employes) : ICommandeSentier
{
    public async Task ExecuterAsync(ContexteCommande contexte, CancellationToken ct)
    {
        var solde = await employes.SoldeAsync(contexte.CleAffaire, ct);
        contexte.Variables.Ecrire(DemandeConge.Solde, solde);
    }
}

public sealed class ParametresSolde { public long NbJours { get; set; } public long Solde { get; set; } }

public sealed class SoldeSuffisant : RequeteSentier<ParametresSolde>
{
    protected override Task<bool> EvaluerAsync(ContexteRequete contexte, ParametresSolde p, CancellationToken ct)
        => Task.FromResult(p.NbJours <= p.Solde);
}
```

Les paramètres d'une requête typée sont liés **par nom** : `p.Variables(NbJours, Solde)` alimente les propriétés `NbJours` et `Solde` (noms des variables `nbJours` et `solde`, sans tenir compte de la casse).

## 8. Brancher le gestionnaire de tâches

Obligatoire dès qu'un processus contient un nœud `Interactif` (sinon le déploiement échoue). Sentier appelle le gestionnaire ; l'application garde ses tâches où elle veut (sa table, un service externe…).

```csharp
public sealed class MesTaches(IDepotTaches depot) : IGestionnaireTaches
{
    public Task<long> CreerTacheAsync(DemandeTacheSentier demande, CancellationToken ct)
        => depot.InsererAsync(demande.IdInstance.Valeur, demande.CodeNoeud, demande.NomTache, demande.Role, ct);

    public Task<TacheSentier?> ObtenirTacheAsync(long idTache, CancellationToken ct)
        => depot.ObtenirAsync(idTache, ct);

    public Task<IReadOnlyList<TacheSentier>> ObtenirTachesOuvertesAsync(IdInstance idInstance, CancellationToken ct)
        => depot.OuvertesAsync(idInstance.Valeur, ct);

    public Task CompleterTacheAsync(long idTache, IReadOnlyDictionary<string, object?>? donnees, string? utilisateur, CancellationToken ct)
        => depot.ChangerStatutAsync(idTache, StatutTacheSentier.Completee, ct);       // idempotent

    public Task AssignerTacheAsync(long idTache, string utilisateur, CancellationToken ct)
        => depot.AssignerAsync(idTache, utilisateur, ct);

    public Task AnnulerTacheAsync(long idTache, string motif, CancellationToken ct)
        => depot.ChangerStatutAsync(idTache, StatutTacheSentier.Annulee, ct);         // idempotent

    public Task RenommerNoeudAsync(IdInstance idInstance, string ancienCode, string nouveauCode, CancellationToken ct)
        => depot.RenommerNoeudAsync(idInstance.Valeur, ancienCode, nouveauCode, ct);
}
```

Exigences : utiliser la **même connexion et transaction** que Sentier si les tâches sont en base (injecter `IDbConnection` / `IDbTransaction` ou `ISessionSentier`), et rendre `CompleterTacheAsync` et `AnnulerTacheAsync` **idempotentes**. Les règles complètes sont au README, § 6.

## 9. Déployer au démarrage

Au démarrage de l'application, une fois, dans une transaction :

```csharp
var rapport = await _uniteTravail.ExecuterAsync(s => s.Resolve<IServiceSentier>().DeployerDefinitionsAsync());

foreach (var d in rapport.Definitions)
{
    _journal.LogInformation("{Cle} v{Version} : {Etat}", d.CleProcessus, d.Version, d.Etat);
    foreach (var avertissement in d.Avertissements) _journal.LogWarning("{Cle} : {Message}", d.CleProcessus, avertissement);
}
```

- Définition inchangée → rien n'est créé. Modifiée → nouvelle version ; les instances en cours gardent la leur.
- Le déploiement **valide tout** (structure, commandes et requêtes enregistrées, gestionnaire de tâches…) : une erreur lève une exception et rien n'est déployé. C'est le meilleur endroit pour découvrir une erreur de définition.

## 10. Utiliser Sentier dans l'application

Toutes les écritures passent par `IServiceSentier`, dans l'unité de travail du [§ 5](#5-fournir-la-connexion-et-la-transaction).

**Démarrer**

```csharp
var resultat = await sentier.DemarrerAsync("demande-conge", cleAffaire: 42,
    ValeursVariables.Avec(DemandeConge.NbJours, 3L));

// resultat.IdInstance, resultat.Statut, resultat.CodeNoeudCourant, resultat.IdTacheCourante, resultat.CodeFin
```

Si une instance est déjà active pour cette clé, le résultat est `SansEffet` (avec `Raison`) et désigne l'instance existante.

**Compléter une tâche** (identifiant donné par votre gestionnaire de tâches)

```csharp
await sentier.CompleterTacheAsync(idTache, new Dictionary<string, object?> { ["decision"] = "approuvee" }, utilisateur: "jdupont");
```

**Envoyer un signal** (nœud `Attente` avec `.Signal("VALIDATION_RH")`)

```csharp
await sentier.EnvoyerSignalAsync("VALIDATION_RH", "demande-conge", cleAffaire: 42);
```

**Annuler**

```csharp
await sentier.AnnulerAsync(idInstance, motif: "Demande retirée");
```

**Consulter** (`ISuiviSentier`, transaction facultative)

```csharp
var suivi = scope.Resolve<ISuiviSentier>();
var instances = await suivi.ObtenirInstancesParCleAffaireAsync(42, ["demande-conge"]);
var historique = await suivi.ObtenirHistoriqueAsync(instances[0].Id);
var diagramme = await suivi.ExporterDiagrammeAsync("demande-conge");   // Mermaid
```

Les cas « rien à faire » (tâche déjà complétée, signal sans destinataire…) retournent `SansEffet` sans lever d'exception. Les vraies erreurs (commande en échec, conflit) sont **propagées** : la transaction du client n'est pas validée.

## 11. Traiter les échéances (batch)

Seulement si un processus utilise `.Echeance(...)`. Le batch du client lit les échéances dues puis traite chacune dans **sa propre transaction** ; en cas d'erreur, l'instance est marquée en erreur dans une autre transaction :

```csharp
var dues = await _uniteTravail.ExecuterAsync(s => s.Resolve<IServiceSentier>().ObtenirEcheancesDuesAsync(tailleMax: 200));

foreach (var echeance in dues)
{
    try
    {
        await _uniteTravail.ExecuterAsync(s => s.Resolve<IServiceSentier>().TraiterEcheanceAsync(echeance.IdAttente));
    }
    catch (Exception ex)
    {
        await _uniteTravail.ExecuterAsync(s => s.Resolve<IServiceSentier>().MarquerEnErreurAsync(echeance.IdInstance, ex.Message));
    }
}
```

Plusieurs batchs peuvent tourner en parallèle. Une instance en erreur se relance avec `ReprendreAsync(idInstance)`.

## 12. Tester avec SQLite

Même code qu'en production, seul le dialecte change. Une base en mémoire partagée vit tant qu'une connexion reste ouverte :

```csharp
var builder = new ContainerBuilder();
builder.RegisterModule(new ModuleSentier(o => o.UtiliserSqlite("SENTIER").BalayerAssemblyDe<DemandeConge>()));
builder.RegisterType<TachesEnMemoire>().As<IGestionnaireTaches>().InstancePerLifetimeScope();
var container = builder.Build();

using var connexion = new SqliteConnection("Data Source=tests;Mode=Memory;Cache=Shared");
connexion.Open();

async Task<T> EnTransaction<T>(Func<ILifetimeScope, Task<T>> action)
{
    using var transaction = connexion.BeginTransaction();
    await using var scope = container.BeginLifetimeScope(b =>
    {
        b.RegisterInstance(connexion).As<IDbConnection>().ExternallyOwned();
        b.RegisterInstance(transaction).As<IDbTransaction>().ExternallyOwned();
    });
    var resultat = await action(scope);
    transaction.Commit();
    return resultat;
}

await EnTransaction(async s => { await s.Resolve<InstallateurSchemaSentier>().InstallerAsync(); return 0; });
await EnTransaction(s => s.Resolve<IServiceSentier>().DeployerDefinitionsAsync());

var resultat = await EnTransaction(s => s.Resolve<IServiceSentier>()
    .DemarrerAsync("demande-conge", 42, ValeursVariables.Avec(DemandeConge.NbJours, 3L)));

Assert.Equal("approbation", resultat.CodeNoeudCourant);
```

Les dates SQLite sont stockées en texte et les clés étrangères ne sont vérifiées qu'avec `PRAGMA foreign_keys = ON`.

## Liste de contrôle

- [ ] Tables créées avec le bon préfixe (script DBA ou `InstallerAsync`).
- [ ] `ModuleSentier` (ou `AddSentier`) enregistré avec le même préfixe et les bons assemblys.
- [ ] Une unité de travail qui enregistre `IDbConnection` et `IDbTransaction` dans la portée, puis valide.
- [ ] Une classe `DefinitionSentier` par processus, avec `[ProcessusSentier("cle")]`.
- [ ] Commandes et requêtes dans un assembly balayé.
- [ ] `IGestionnaireTaches` implémenté si un processus a un nœud interactif.
- [ ] `DeployerDefinitionsAsync` appelé au démarrage et son rapport journalisé.
- [ ] Batch des échéances planifié si un processus a une échéance.
- [ ] (Facultatif) `IContexteUtilisateur` pour l'audit.

## Erreurs fréquentes

| Symptôme | Cause | Correction |
|---|---|---|
| `TransactionRequiseException` | Aucune transaction dans la portée, ou déjà validée. | Enregistrer `IDbTransaction` dans la portée (ou alimenter `IAccesseurTransaction`) avant d'appeler le service. |
| « Aucune IDbConnection n'est enregistrée » | Service résolu depuis le conteneur racine. | Résoudre `IServiceSentier` depuis la portée de l'unité de travail. |
| `DefinitionInvalideException` | Définition incohérente (code en double, nœud inatteignable, décision sans « sinon »…). | Lire la liste d'erreurs de l'exception. |
| Le déploiement échoue sur une commande ou une requête inconnue | Classe hors des assemblys balayés. | Ajouter `BalayerAssemblyDe<UneClasseDeCetAssembly>()`. |
| Le déploiement échoue sur un nœud interactif | Aucun `IGestionnaireTaches`. | L'implémenter dans un assembly balayé, ou `UtiliserGestionnaireTaches<T>()`. |
| `VariableInvalideException` au démarrage | Variable passée au démarrage mais non déclarée dans la définition. | La déclarer comme champ `Variable<T>` statique. |
| Démarrage `SansEffet` | Instance déjà active pour ce processus et cette clé. | Normal ; `InstancesMultiples()` dans la définition pour lever la règle. |
| Connexion ou transaction disposée à la fin de la portée | Enregistrée sans `ExternallyOwned()` : Autofac la dispose avec la portée. | Ajouter `ExternallyOwned()`. |
