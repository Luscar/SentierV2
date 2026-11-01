# Intégrer Sentier dans une application client

Ce document liste **tout ce que l'application client doit fournir** à Sentier : les contrats à implémenter
(obligatoires, selon les nœuds utilisés, ou optionnels) et leur enregistrement dans l'injection de dépendances.
Le pas-à-pas complet est dans le [guide de démarrage](demarrage.md) ; les règles détaillées sont dans le [README](../README.md).

## 1. Vue d'ensemble

| Contrat | Quand | Trouvé par | Durée de vie |
|---|---|---|---|
| `IDbConnection` (enregistrement) | **Toujours**, sauf avec un exécuteur SQL du client (§ 3.2) | Enregistrée par le client | Portée |
| `IExecuteurSqlSentier` | Optionnel : faire passer les requêtes de Sentier par la couche d'accès aux données du client (§ 3.2) | `UtiliserExecuteurSql<T>()` | Portée |
| Transaction (écritures) : **au choix** un `TransactionScope` autour de la connexion (Oracle, aucune `IDbTransaction` à fournir), ou une `IDbTransaction` (enregistrée, ou via `IAccesseurTransactionSentier` / `ISessionSentier`) | **Toujours** (écritures) | Ouverte par le client | Portée |
| `DefinitionSentier` + `[ProcessusSentier]` | **Toujours** : un par processus | Balayage | — |
| `ICommandeSentier` / `CommandeSentier<T>` | Nœuds `Systemique`, commandes d'entrée et de sortie, calcul de migration | Balayage | Portée |
| `IRequeteSentier` / `RequeteSentier<T>` | Branches de `Decision` calculées par code | Balayage | Portée |
| `IGestionnaireTachesSentier` | Dès qu'un processus a un nœud `Interactif` (sinon le déploiement échoue) | Balayage, ou `UtiliserGestionnaireTaches<T>()` | Portée |
| `IPlanMigration` | Migration d'instances non compatible avec la nouvelle version | Balayage | — |
| `IContexteUtilisateurSentier` | Optionnel : utilisateur courant pour l'audit | Enregistré par le client | Portée |
| `IHorlogeSentier` | Optionnel : heure courante (tests des échéances) | Enregistrée par le client | Singleton |
| `IIntercepteurCommandeSentier` / `IIntercepteurRequeteSentier` | Optionnel : journalisation, audit, mesure | Enregistrés par le client | Portée |
| `ISerialiseurVariablesSentier` | Optionnel : format de stockage des variables (JSON par défaut) | Enregistré par le client | Singleton |

« Balayage » : les types concrets des assemblys indiqués par `BalayerAssemblyDe<T>()` sont trouvés et enregistrés
par Sentier ; le client n'a rien à enregistrer pour eux.

## 2. Injection de dépendances

### 2.1 Autofac

Il n'y a pas de package Autofac dédié : `AddSentier` passe par `Autofac.Extensions.DependencyInjection`. Sous
ASP.NET Core / Generic Host avec `AutofacServiceProviderFactory`, appeler simplement `services.AddSentier(...)`.
Avec un `ContainerBuilder` seul :

```csharp
var services = new ServiceCollection();
services.AddSentier(o => o
    .UtiliserOracle("SENTIER")                 // préfixe des tables (obligatoire) ; UtiliserSqlite("SENTIER") pour les tests
    .BalayerAssemblyDe<Program>());            // définitions, commandes, requêtes, plans de migration, gestionnaire de tâches
    // .UtiliserGestionnaireTaches<MesTaches>()  // si le balayage ne suffit pas : plusieurs implémentations, ou classe hors des assemblys balayés
    // .UtiliserSession<MaSession>()             // seulement pour une session sur mesure (§ 3.1)

var builder = new ContainerBuilder();
builder.Populate(services);                    // Autofac.Extensions.DependencyInjection

// Optionnels, enregistrés APRÈS Populate pour remplacer ceux de Sentier :
builder.RegisterType<ContexteUtilisateurWeb>().As<IContexteUtilisateurSentier>().InstancePerLifetimeScope();
builder.RegisterType<JournalCommandes>().As<IIntercepteurCommandeSentier>().InstancePerLifetimeScope();
builder.RegisterType<JournalRequetes>().As<IIntercepteurRequeteSentier>().InstancePerLifetimeScope();
builder.RegisterType<HorlogeMetier>().As<IHorlogeSentier>().SingleInstance();

var container = builder.Build();
```

Puis, **pour chaque unité de travail** (requête web, message, itération de batch), une portée qui porte la connexion
du client. Avec une transaction ambiante (Oracle), **seule l'`IDbConnection` est enregistrée** :

```csharp
using var portee = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);
using var connexion = fabriqueConnexions.Ouvrir();                           // ouverte DANS la portée : enrôlée

await using var scope = container.BeginLifetimeScope(b =>
    b.RegisterInstance(connexion).As<IDbConnection>().ExternallyOwned());     // le client en reste propriétaire

await scope.Resolve<IServiceSentier>().DemarrerAsync("demande-conge", cleAffaire: 42);
portee.Complete();                                                           // une exception : pas de Complete
```

Si l'application travaille plutôt avec une `IDbTransaction` (tests SQLite, par exemple), elle l'enregistre aussi :

```csharp
using var connexion = fabriqueConnexions.Ouvrir();
using var transaction = connexion.BeginTransaction();

await using var scope = container.BeginLifetimeScope(b =>
{
    b.RegisterInstance(connexion).As<IDbConnection>().ExternallyOwned();
    b.RegisterInstance(transaction).As<IDbTransaction>().ExternallyOwned();
});

await scope.Resolve<IServiceSentier>().DemarrerAsync("demande-conge", cleAffaire: 42);
transaction.Commit();                                                        // une exception : pas de commit
```

### 2.2 Microsoft.Extensions.DependencyInjection

```csharp
services.AddScoped<IDbConnection>(_ => OuvrirConnexion());
services.AddSentier(o => o.UtiliserOracle("SENTIER").BalayerAssemblyDe<Program>());

// Optionnels : IHorlogeSentier et ISerialiseurVariablesSentier AVANT AddSentier (Sentier ne les ajoute que s'ils manquent),
// les autres où l'on veut.
services.AddScoped<IContexteUtilisateurSentier, ContexteUtilisateurWeb>();
services.AddScoped<IIntercepteurCommandeSentier, JournalCommandes>();
```

Par portée, avec une transaction ambiante (Oracle), il suffit d'ouvrir le `TransactionScope` **avant** de résoudre
la connexion (la fabrique `OuvrirConnexion()` l'ouvre alors dans la portée) :

```csharp
using var portee = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);
using var scope = provider.CreateScope();

await scope.ServiceProvider.GetRequiredService<IServiceSentier>().DemarrerAsync("demande-conge", cleAffaire: 42);
portee.Complete();
```

Avec une `IDbTransaction`, l'alimenter dans `IAccesseurTransactionSentier` :

```csharp
using var scope = provider.CreateScope();
var connexion = scope.ServiceProvider.GetRequiredService<IDbConnection>();
using var transaction = connexion.BeginTransaction();
scope.ServiceProvider.GetRequiredService<IAccesseurTransactionSentier>().Transaction = transaction;

await scope.ServiceProvider.GetRequiredService<IServiceSentier>().DemarrerAsync("demande-conge", cleAffaire: 42);
transaction.Commit();
```

### 2.3 Ce que Sentier n'enregistre jamais

- l'`IDbConnection` (et l'`IDbTransaction` s'il en utilise une) : elles appartiennent au client ;
- `IContexteUtilisateurSentier`, les intercepteurs : à enregistrer par le client s'il en veut.

## 3. Contrats obligatoires

### 3.1 Connexion et transaction

Sentier lit la connexion et la transaction par `ISessionSentier` (portée). L'implémentation par défaut prend :

- la connexion : l'`IDbConnection` enregistrée dans la portée (déjà ouverte) ;
- la transaction, **facultative** : celle d'`IAccesseurTransactionSentier` si le client l'a alimentée, sinon
  l'`IDbTransaction` de la portée, sinon aucune (transaction ambiante).

Sentier **n'ouvre, ne valide et n'annule jamais** de transaction. Il en exige une pour écrire : une `IDbTransaction`,
ou à défaut un `TransactionScope` actif (Oracle). Sans l'une ni l'autre, toute écriture lève
`TransactionRequiseException`. Les commandes du client reçoivent la même connexion par injection et écrivent
donc dans la même transaction.

**Application sans `IDbTransaction` (Oracle).** Si l'application n'enregistre que l'`IDbConnection` (ses appels Dapper
passent `transaction: null`) et rend ses écritures atomiques avec un `TransactionScope`, rien d'autre n'est à fournir :
Sentier exécute alors ses requêtes avec `transaction: null` sur la connexion enrôlée (exemple au § 2.1).

- La connexion doit être ouverte **à l'intérieur** de la portée ; `TransactionScopeAsyncFlowOption.Enabled` est nécessaire avec `async`.
- **Oracle seulement** : Microsoft.Data.Sqlite ne s'enrôle pas dans un `TransactionScope`. Avec SQLite, Sentier refuse
  d'écrire (`TransactionRequiseException`) plutôt que de valider chaque instruction immédiatement ; les tests SQLite
  fournissent une `IDbTransaction`.

**Unité de travail existante.** Pour brancher une unité de travail qui expose déjà sa connexion et, le cas échéant, sa transaction :

```csharp
public sealed class MaSession(UniteDeTravail uow) : ISessionSentier
{
    public IDbConnection Connexion => uow.Connexion;
    public IDbTransaction? Transaction => uow.Transaction;    // null avec un TransactionScope
}
```

Enregistrement : `o.UtiliserSession<MaSession>()`.

### 3.2 Passer par sa propre couche d'accès aux données — `IExecuteurSqlSentier`

Par défaut, Sentier exécute ses requêtes avec Dapper sur l'`IDbConnection` de la portée. Une application qui fait
tous ses appels par un wrapper maison autour de Dapper (par exemple `IAccesBDPlus`, qui regroupe
`IAccesBDEcriturePlus` et `IAccesBDLecturePlus`) peut y faire passer Sentier aussi. Il suffit d'un adaptateur de quatre méthodes :

```csharp
public sealed class ExecuteurSqlAccesBDPlus(IAccesBDPlus accesBD) : IExecuteurSqlSentier
{
    public Task<int> ExecuterAsync(string sql, object? parametres, CancellationToken ct)
        => accesBD.ExecuteNonQueryAsync(sql, parametres, ct);

    public Task<T?> ExecuterScalaireAsync<T>(string sql, object? parametres, CancellationToken ct)
        => accesBD.ExecuteScalarAsync<T>(sql, parametres, ct);

    public Task<IEnumerable<T>> LireAsync<T>(string sql, object? parametres, CancellationToken ct)
        => accesBD.QueryAsync<T>(sql, parametres, ct);

    public Task<T?> LirePremierOuDefautAsync<T>(string sql, object? parametres, CancellationToken ct)
        => accesBD.QueryFirstOrDefaultAsync<T>(sql, parametres, ct);
}
```

Enregistrement (Autofac ou `Microsoft.Extensions.DependencyInjection`, même option) :

```csharp
services.AddSentier(o => o
    .UtiliserOracle("SENTIER")
    .BalayerAssemblyDe<Program>()
    .UtiliserExecuteurSql<ExecuteurSqlAccesBDPlus>());
```

L'unité de travail n'enregistre alors **plus rien pour Sentier** : ni `IDbConnection`, ni `IDbTransaction` (inutile : le
wrapper passe `transaction: null`).
`IAccesBDPlus` reste résolu comme d'habitude dans la portée, et le `TransactionScope` rend l'ensemble atomique :

```csharp
using var portee = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);
await using var scope = container.BeginLifetimeScope();

await scope.Resolve<IServiceSentier>().DemarrerAsync("demande-conge", cleAffaire: 42);
portee.Complete();
```

Ce que l'adaptateur reçoit et doit respecter :

- **Le SQL est déjà dans la syntaxe du dialecte** : paramètres `:nom` sous Oracle, `@nom` sous SQLite.
- **Les paramètres sont des objets Dapper** : objets anonymes, `DynamicParameters`, et listes développées par Dapper
  (`STAT IN :statuts`). Le wrapper doit les transmettre tels quels à Dapper.
- **Les lignes sont mappées par nom de colonne** (`QueryAsync<T>` de Dapper).
- **Aucune `IDbTransaction`** : l'interface ne transporte pas de transaction, le wrapper appelle Dapper avec
  `transaction: null`. Le `TransactionScope` est la seule transaction : la connexion du wrapper doit être ouverte
  **dans** la portée pour y être enrôlée. Sentier vérifie avant d'écrire qu'un `TransactionScope` est actif
  (sinon `TransactionRequiseException`).
- `GetSequenceAsync` n'est pas nécessaire : Sentier lit ses séquences par `ExecuterScalaireAsync<long>`
  (`SELECT … .NEXTVAL FROM DUAL`).
- `InstallateurSchemaSentier` passe aussi par l'exécuteur.

### 3.3 Définitions de processus

Une classe par processus, dérivée de `DefinitionSentier`, avec la clé du processus :

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

Déploiement, une fois au démarrage, dans une transaction : `IServiceSentier.DeployerDefinitionsAsync()`. Une nouvelle
version n'est créée que si la définition a changé. Les nœuds sont décrits dans le [guide du constructeur](constructeur.md).

## 4. Contrats selon les nœuds utilisés

### 4.1 Commandes — `ICommandeSentier`

Toute écriture (base métier, variables du processus). Injection par constructeur, même transaction que Sentier.

```csharp
public interface ICommandeSentier
{
    Task ExecuterAsync(ContexteCommandeSentier contexte, CancellationToken ct);
}
```

`ContexteCommandeSentier` : `Instance`, `IdInstance`, `CleProcessus`, `CleAffaire`, `CodeNoeud`, `Moment` (`Execution`, `Entree`,
`Sortie`, `Migration`), `Parametres`, `Variables` (lecture et écriture), `DonneesTache` (commande de sortie), `Utilisateur`.

Avec paramètres typés, dériver de `CommandeSentier<TParametres>` (liaison par nom de propriété) :

```csharp
public sealed class CalculerSolde(IDepotEmployes employes) : ICommandeSentier
{
    public async Task ExecuterAsync(ContexteCommandeSentier contexte, CancellationToken ct)
        => contexte.Variables.Ecrire(DemandeConge.Solde, await employes.SoldeAsync(contexte.CleAffaire, ct));
}

public sealed class NotifierEmploye(INotifications notifications) : CommandeSentier<ParametresNotification>
{
    protected override Task ExecuterAsync(ContexteCommandeSentier contexte, ParametresNotification p, CancellationToken ct)
        => notifications.EnvoyerAsync(contexte.CleAffaire, p.Message, ct);
}
```

Nom de la commande : celui de la classe. Après un renommage, `[CommandeSentier("AncienNom")]` garde l'ancien nom pour les versions déjà déployées ; le déploiement échoue si une version suivie par des instances actives cite une commande introuvable. Deux commandes du même nom font échouer le démarrage.

### 4.2 Requêtes — `IRequeteSentier`

Évalue une branche de `Decision`, en lecture seule.

```csharp
public interface IRequeteSentier
{
    Task<bool> EvaluerAsync(ContexteRequeteSentier contexte, CancellationToken ct);
}
```

`ContexteRequeteSentier` : `Instance`, `IdInstance`, `CleProcessus`, `CleAffaire`, `CodeNoeud`, `Parametres`, `Variables` (lecture seule).
Avec paramètres typés : `RequeteSentier<TParametres>`.

```csharp
public sealed class ParametresSolde { public long NbJours { get; set; } public long Solde { get; set; } }

public sealed class SoldeSuffisant : RequeteSentier<ParametresSolde>
{
    protected override Task<bool> EvaluerAsync(ContexteRequeteSentier contexte, ParametresSolde p, CancellationToken ct)
        => Task.FromResult(p.NbJours <= p.Solde);
}
```

Nom : celui de la classe ; `[RequeteSentier("AncienNom")]` après un renommage, comme pour les commandes. Une décision sur une simple variable ne demande aucune requête.

### 4.3 Tâches utilisateurs — `IGestionnaireTachesSentier`

Obligatoire dès qu'un processus a un nœud `Interactif`. Sentier **ne stocke aucune tâche** : il délègue chaque étape
au gestionnaire du client et garde seulement l'identifiant de la tâche attendue par l'instance.

| Méthode | Appelée par Sentier | Exigence |
|---|---|---|
| `Task<long> CreerTacheAsync(DemandeTacheSentier demande, ct)` | Entrée dans un nœud interactif, après les commandes d'entrée | Retourner l'identifiant de la tâche chez le client |
| `Task<TacheSentier?> ObtenirTacheAsync(long idTache, ct)` | Début de `CompleterTacheAsync` et `AssignerTacheAsync` | `null` si inconnue |
| `Task<IReadOnlyList<TacheSentier>> ObtenirTachesOuvertesAsync(IdInstance, ct)` | Annulation, reprise, migration, suivi | Peut être appelée sans transaction |
| `Task CompleterTacheAsync(long idTache, donnees, utilisateur, ct)` | Complétion, après les commandes de sortie | **Idempotente** |
| `Task AssignerTacheAsync(long idTache, string utilisateur, ct)` | `IServiceSentier.AssignerTacheAsync`, seulement pour la tâche attendue | — |
| `Task AnnulerTacheAsync(long idTache, string motif, ct)` | Annulation de l'instance, tâche orpheline, migration | **Idempotente** |
| `Task RenommerNoeudAsync(IdInstance, ancienCode, nouveauCode, ct)` | Migration qui renomme le nœud interactif | — |

`DemandeTacheSentier` : `IdInstance`, `CleProcessus`, `CleAffaire`, `CodeNoeud`, `NomTache`, `Role`, `Assigne` (assignation automatique),
`DateEcheance`, `Donnees`. `TacheSentier` (retournée au moteur) : `Id`, `IdInstance`, `CodeNoeud`,
`Statut` (`Ouverte`, `Completee`, `Annulee`).

```csharp
public sealed class MesTaches(IDepotTaches depot) : IGestionnaireTachesSentier
{
    public Task<long> CreerTacheAsync(DemandeTacheSentier demande, CancellationToken ct)
        => depot.InsererAsync(demande.IdInstance.Valeur, demande.CodeNoeud, demande.NomTache, demande.Role, demande.Assigne, ct);

    public Task<TacheSentier?> ObtenirTacheAsync(long idTache, CancellationToken ct)
        => depot.ObtenirAsync(idTache, ct);

    public Task<IReadOnlyList<TacheSentier>> ObtenirTachesOuvertesAsync(IdInstance idInstance, CancellationToken ct)
        => depot.OuvertesAsync(idInstance.Valeur, ct);

    public Task CompleterTacheAsync(long idTache, IReadOnlyDictionary<string, object?>? donnees, string? utilisateur, CancellationToken ct)
        => depot.ChangerStatutAsync(idTache, StatutTacheSentier.Completee, ct);      // idempotent

    public Task AssignerTacheAsync(long idTache, string utilisateur, CancellationToken ct)
        => depot.AssignerAsync(idTache, utilisateur, ct);

    public Task AnnulerTacheAsync(long idTache, string motif, CancellationToken ct)
        => depot.ChangerStatutAsync(idTache, StatutTacheSentier.Annulee, ct);        // idempotent

    public Task RenommerNoeudAsync(IdInstance idInstance, string ancienCode, string nouveauCode, CancellationToken ct)
        => depot.RenommerNoeudAsync(idInstance.Valeur, ancienCode, nouveauCode, ct);
}
```

Si les tâches sont en base, utiliser la **même connexion et transaction** que Sentier (injecter `IDbConnection`
ou `ISessionSentier`). Le balayage ignore les décorateurs (classes dont un constructeur reçoit un
`IGestionnaireTachesSentier`). Sans indication, l'unique implémentation trouvée est enregistrée automatiquement ;
`UtiliserGestionnaireTaches<T>()` est nécessaire s'il en reste plusieurs (sinon erreur au démarrage)
ou si la classe est dans un assembly non balayé.

### 4.4 Plans de migration — `IPlanMigration`

Seulement pour migrer des instances vers une version **non compatible** (la migration automatique couvre les ajouts).

```csharp
public sealed class PlanCongeV3 : IPlanMigration
{
    public string CleProcessus => "demande-conge";
    public int VersionSourceMin => 1;
    public int VersionSourceMax => 2;

    public void Definir(ConstructeurPlanMigration plan) => plan
        .Noeud("attendre-rh", "attente-rh", r => r.ConserverAttentes())
        .PourNoeud("ancienne-tache", r => r.PuisReprendreA("nouvelle-tache"))
        .RenommerVariable("ancien", "nouveau")
        .ValeurVariable(DemandeConge.Solde, 0L)
        .CalculerVariables<CalculerVariablesMigration>();
}
```

Utilisation : `SimulerMigrationAsync` (rapport, rien n'est écrit), puis `MigrerAsync`.

## 5. Contrats optionnels

| Contrat | Signature | Usage |
|---|---|---|
| `IContexteUtilisateurSentier` | `string? Utilisateur { get; }` | Auteur par défaut des actions (audit) quand l'appel n'en précise pas |
| `IHorlogeSentier` | `DateTime MaintenantUtc { get; }` | Heure courante (UTC) ; une horloge simulée permet de tester les échéances |
| `IIntercepteurCommandeSentier` | `Task ExecuterAsync(string nomCommande, ContexteCommandeSentier contexte, Func<Task> suivant, ct)` | Code autour de chaque commande ; appeler `suivant()` |
| `IIntercepteurRequeteSentier` | `Task<bool> EvaluerAsync(string nomRequete, ContexteRequeteSentier contexte, Func<Task<bool>> suivant, ct)` | Code autour de chaque requête ; retourner le résultat de `suivant()` |
| `ISerialiseurVariablesSentier` | `string Serialiser(valeurs)` ; `Dictionary<string, object?> Deserialiser(json, types)` | Remplacer le stockage JSON des variables |
| `ISessionSentier` | `IDbConnection Connexion` ; `IDbTransaction? Transaction` | Session sur mesure (§ 3.1), par `UtiliserSession<T>()` |
| `IExecuteurSqlSentier` | `ExecuterAsync` ; `ExecuterScalaireAsync<T>` ; `LireAsync<T>` ; `LirePremierOuDefautAsync<T>` | Couche d'accès aux données du client (§ 3.2), par `UtiliserExecuteurSql<T>()` |

Plusieurs intercepteurs peuvent être enregistrés : ils s'enchaînent dans l'ordre d'enregistrement.

## 6. Appels de l'application

Tous les appels se font dans une portée qui porte la connexion et la transaction (§ 2).

| Besoin | Appel |
|---|---|
| Démarrer une instance | `IServiceSentier.DemarrerAsync(cleProcessus, cleAffaire, variables)` |
| Compléter une tâche | `IServiceSentier.CompleterTacheAsync(idTache, donnees, utilisateur)` |
| Réassigner une tâche | `IServiceSentier.AssignerTacheAsync(idTache, utilisateur)` |
| Envoyer un signal | `IServiceSentier.EnvoyerSignalAsync(nomSignal, cleProcessus, cleAffaire)` ou `EnvoyerSignalCorrelationAsync` |
| Annuler une instance | `IServiceSentier.AnnulerAsync(idInstance, motif)` |
| Corriger des variables | `IServiceSentier.ModifierVariablesAsync(idInstance, valeurs, utilisateur)` (instance suspendue ou en erreur) |
| Rechercher par variables | `ISuiviSentier.RechercherParVariablesAsync(recherche)` ou `RechercherParVariableAsync(cleProcessus, variable, valeur)` |
| Batch des échéances | `ObtenirEcheancesDuesAsync`, puis `TraiterEcheanceAsync` par échéance, chacune dans sa transaction ; `MarquerEnErreurAsync` en cas d'échec |
| Consulter | `ISuiviSentier` : instance, recherche par clé d'affaire ou par variables, historique, attentes, tâches ouvertes, instances en erreur, diagramme |

Les cas « rien à faire » (tâche déjà complétée, signal sans destinataire…) retournent `SansEffet` sans exception.
Les vraies erreurs sont propagées : la transaction du client n'est pas validée.

## 7. Liste de contrôle

- [ ] `AddSentier` avec préfixe, dialecte et `BalayerAssemblyDe<T>()` pour chaque assembly concerné.
- [ ] Une portée par unité de travail, avec l'`IDbConnection` ouverte dans un `TransactionScope` (Oracle), ou avec une `IDbTransaction` (§ 2, § 3.1) — ou, avec un wrapper maison, un adaptateur `IExecuteurSqlSentier` et un `TransactionScope`, sans `IDbConnection` ni `IDbTransaction` (§ 3.2).
- [ ] Une `DefinitionSentier` + `[ProcessusSentier]` par processus ; `DeployerDefinitionsAsync()` au démarrage.
- [ ] Une `ICommandeSentier` par commande référencée ; une `IRequeteSentier` par décision calculée.
- [ ] `IGestionnaireTachesSentier` (7 méthodes) si un processus a un nœud interactif ; `CompleterTacheAsync` et `AnnulerTacheAsync` idempotentes.
- [ ] Un batch des échéances si un processus utilise `.Echeance(...)` ou une attente temporelle.
- [ ] `IPlanMigration` pour chaque migration non compatible.
- [ ] (Optionnel) `IContexteUtilisateurSentier`, intercepteurs, `IHorlogeSentier`, `ISerialiseurVariablesSentier`.
