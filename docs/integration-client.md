# Intégrer Sentier dans une application client

Ce document liste **tout ce que l'application client doit fournir** à Sentier : les contrats à implémenter
(obligatoires, selon les nœuds utilisés, ou optionnels) et leur enregistrement dans l'injection de dépendances.
Le pas-à-pas complet est dans le [guide de démarrage](demarrage.md) ; les règles détaillées sont dans le [README](../README.md).

## 1. Vue d'ensemble

| Contrat | Quand | Trouvé par | Durée de vie |
|---|---|---|---|
| `IDbConnection` (enregistrement) | **Toujours** | Enregistrée par le client | Portée |
| Transaction : `IDbTransaction`, `IAccesseurTransaction` ou `ISessionSentier` | **Toujours** (écritures) | Enregistrée ou alimentée par le client | Portée |
| `DefinitionSentier` + `[ProcessusSentier]` | **Toujours** : un par processus | Balayage | — |
| `ICommandeSentier` / `CommandeSentier<T>` | Nœuds `Systemique`, commandes d'entrée et de sortie, calcul de migration | Balayage | Portée |
| `IRequeteSentier` / `RequeteSentier<T>` | Branches de `Decision` calculées par code | Balayage | Portée |
| `IGestionnaireTaches` | Dès qu'un processus a un nœud `Interactif` (sinon le déploiement échoue) | Balayage, ou `UtiliserGestionnaireTaches<T>()` | Portée |
| `IPlanMigration` | Migration d'instances non compatible avec la nouvelle version | Balayage | — |
| `IContexteUtilisateur` | Optionnel : utilisateur courant pour l'audit | Enregistré par le client | Portée |
| `IHorloge` | Optionnel : heure courante (tests des échéances) | Enregistrée par le client | Singleton |
| `IIntercepteurCommande` / `IIntercepteurRequete` | Optionnel : journalisation, audit, mesure | Enregistrés par le client | Portée |
| `ISerialiseurVariables` | Optionnel : format de stockage des variables (JSON par défaut) | Enregistré par le client | Singleton |

« Balayage » : les types concrets des assemblys indiqués par `BalayerAssemblyDe<T>()` sont trouvés et enregistrés
par Sentier ; le client n'a rien à enregistrer pour eux.

## 2. Injection de dépendances

### 2.1 Autofac

```csharp
var builder = new ContainerBuilder();

builder.RegisterModule(new ModuleSentier(o => o
    .UtiliserOracle("SENTIER")                 // préfixe des tables (obligatoire) ; UtiliserSqlite("SENTIER") pour les tests
    .BalayerAssemblyDe<Program>()));           // définitions, commandes, requêtes, plans de migration, gestionnaire de tâches
    // .UtiliserGestionnaireTaches<MesTaches>()  // si le balayage ne suffit pas : plusieurs implémentations, ou classe hors des assemblys balayés
    // .UtiliserSession<MaSession>()             // seulement pour une session sur mesure (§ 3.1)

// Optionnels, enregistrés APRÈS le module pour remplacer ceux de Sentier :
builder.RegisterType<ContexteUtilisateurWeb>().As<IContexteUtilisateur>().InstancePerLifetimeScope();
builder.RegisterType<JournalCommandes>().As<IIntercepteurCommande>().InstancePerLifetimeScope();
builder.RegisterType<JournalRequetes>().As<IIntercepteurRequete>().InstancePerLifetimeScope();
builder.RegisterType<HorlogeMetier>().As<IHorloge>().SingleInstance();

var container = builder.Build();
```

Puis, **pour chaque unité de travail** (requête web, message, itération de batch), une portée qui porte la connexion
et la transaction du client :

```csharp
using var connexion = fabriqueConnexions.Ouvrir();
using var transaction = connexion.BeginTransaction();

await using var scope = container.BeginLifetimeScope(b =>
{
    b.RegisterInstance(connexion).As<IDbConnection>().ExternallyOwned();     // le client en reste propriétaire
    b.RegisterInstance(transaction).As<IDbTransaction>().ExternallyOwned();
});

await scope.Resolve<IServiceSentier>().DemarrerAsync("demande-conge", cleAffaire: 42);
transaction.Commit();                                                        // une exception : pas de commit
```

### 2.2 Microsoft.Extensions.DependencyInjection

```csharp
services.AddScoped<IDbConnection>(_ => OuvrirConnexion());
services.AddSentier(o => o.UtiliserOracle("SENTIER").BalayerAssemblyDe<Program>());

// Optionnels : IHorloge et ISerialiseurVariables AVANT AddSentier (Sentier ne les ajoute que s'ils manquent),
// les autres où l'on veut.
services.AddScoped<IContexteUtilisateur, ContexteUtilisateurWeb>();
services.AddScoped<IIntercepteurCommande, JournalCommandes>();
```

Par portée, alimenter la transaction :

```csharp
using var scope = provider.CreateScope();
var connexion = scope.ServiceProvider.GetRequiredService<IDbConnection>();
using var transaction = connexion.BeginTransaction();
scope.ServiceProvider.GetRequiredService<IAccesseurTransaction>().Transaction = transaction;

await scope.ServiceProvider.GetRequiredService<IServiceSentier>().DemarrerAsync("demande-conge", cleAffaire: 42);
transaction.Commit();
```

### 2.3 Ce que Sentier n'enregistre jamais

- l'`IDbConnection` et l'`IDbTransaction` : elles appartiennent au client ;
- `IContexteUtilisateur`, les intercepteurs : à enregistrer par le client s'il en veut.

## 3. Contrats obligatoires

### 3.1 Connexion et transaction

Sentier lit la connexion et la transaction par `ISessionSentier` (portée). L'implémentation par défaut prend :

- la connexion : l'`IDbConnection` enregistrée dans la portée (déjà ouverte) ;
- la transaction : celle d'`IAccesseurTransaction` si le client l'a alimentée, sinon l'`IDbTransaction` de la portée.

Sentier **n'ouvre, ne valide et n'annule jamais** de transaction. Toute écriture sans transaction active lève
`TransactionRequiseException`. Les commandes du client reçoivent la même connexion par injection et écrivent
donc dans la même transaction.

Pour brancher une unité de travail existante qui expose déjà sa connexion et sa transaction :

```csharp
public sealed class MaSession(UniteDeTravail uow) : ISessionSentier
{
    public IDbConnection Connexion => uow.Connexion;
    public IDbTransaction? Transaction => uow.Transaction;
}
```

Enregistrement : `o.UtiliserSession<MaSession>()`.

### 3.2 Définitions de processus

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
    Task ExecuterAsync(ContexteCommande contexte, CancellationToken ct);
}
```

`ContexteCommande` : `Instance`, `IdInstance`, `CleProcessus`, `CleAffaire`, `CodeNoeud`, `Moment` (`Execution`, `Entree`,
`Sortie`, `Migration`), `Parametres`, `Variables` (lecture et écriture), `DonneesTache` (commande de sortie), `Utilisateur`.

Avec paramètres typés, dériver de `CommandeSentier<TParametres>` (liaison par nom de propriété) :

```csharp
public sealed class CalculerSolde(IDepotEmployes employes) : ICommandeSentier
{
    public async Task ExecuterAsync(ContexteCommande contexte, CancellationToken ct)
        => contexte.Variables.Ecrire(DemandeConge.Solde, await employes.SoldeAsync(contexte.CleAffaire, ct));
}

public sealed class NotifierEmploye(INotifications notifications) : CommandeSentier<ParametresNotification>
{
    protected override Task ExecuterAsync(ContexteCommande contexte, ParametresNotification p, CancellationToken ct)
        => notifications.EnvoyerAsync(contexte.CleAffaire, p.Message, ct);
}
```

Nom de la commande : celui de la classe, ou `[CommandeSentier("nom")]`. Deux commandes du même nom font échouer le démarrage.

### 4.2 Requêtes — `IRequeteSentier`

Évalue une branche de `Decision`, en lecture seule.

```csharp
public interface IRequeteSentier
{
    Task<bool> EvaluerAsync(ContexteRequete contexte, CancellationToken ct);
}
```

`ContexteRequete` : `Instance`, `IdInstance`, `CleProcessus`, `CleAffaire`, `CodeNoeud`, `Parametres`, `Variables` (lecture seule).
Avec paramètres typés : `RequeteSentier<TParametres>`.

```csharp
public sealed class ParametresSolde { public long NbJours { get; set; } public long Solde { get; set; } }

public sealed class SoldeSuffisant : RequeteSentier<ParametresSolde>
{
    protected override Task<bool> EvaluerAsync(ContexteRequete contexte, ParametresSolde p, CancellationToken ct)
        => Task.FromResult(p.NbJours <= p.Solde);
}
```

Nom : celui de la classe, ou `[RequeteSentier("nom")]`. Une décision sur une simple variable ne demande aucune requête.

### 4.3 Tâches utilisateurs — `IGestionnaireTaches`

Obligatoire dès qu'un processus a un nœud `Interactif`. Sentier **ne stocke aucune tâche** : il délègue chaque étape
au gestionnaire du client et garde seulement l'identifiant de la tâche attendue par l'instance.

| Méthode | Appelée par Sentier | Exigence |
|---|---|---|
| `Task<long> CreerTacheAsync(DemandeTacheUtilisateur demande, ct)` | Entrée dans un nœud interactif, après les commandes d'entrée | Retourner l'identifiant de la tâche chez le client |
| `Task<TacheUtilisateur?> ObtenirTacheAsync(long idTache, ct)` | Début de `CompleterTacheAsync` et `AssignerTacheAsync` | `null` si inconnue |
| `Task<IReadOnlyList<TacheUtilisateur>> ObtenirTachesOuvertesAsync(IdInstance, ct)` | Annulation, reprise, migration, suivi | Peut être appelée sans transaction |
| `Task CompleterTacheAsync(long idTache, donnees, utilisateur, ct)` | Complétion, après les commandes de sortie | **Idempotente** |
| `Task AssignerTacheAsync(long idTache, string utilisateur, ct)` | `IServiceSentier.AssignerTacheAsync`, seulement pour la tâche attendue | — |
| `Task AnnulerTacheAsync(long idTache, string motif, ct)` | Annulation de l'instance, tâche orpheline, migration | **Idempotente** |
| `Task RenommerNoeudAsync(IdInstance, ancienCode, nouveauCode, ct)` | Migration qui renomme le nœud interactif | — |

`DemandeTacheUtilisateur` : `IdInstance`, `CleProcessus`, `CleAffaire`, `CodeNoeud`, `Nom`, `Role`, `Utilisateur`,
`DateEcheance`, `Donnees`. `TacheUtilisateur` (retournée au moteur) : `Id`, `IdInstance`, `CodeNoeud`,
`Statut` (`Ouverte`, `Completee`, `Annulee`).

```csharp
public sealed class MesTaches(IDepotTaches depot) : IGestionnaireTaches
{
    public Task<long> CreerTacheAsync(DemandeTacheUtilisateur demande, CancellationToken ct)
        => depot.InsererAsync(demande.IdInstance.Valeur, demande.CodeNoeud, demande.Nom, demande.Role, demande.Utilisateur, ct);

    public Task<TacheUtilisateur?> ObtenirTacheAsync(long idTache, CancellationToken ct)
        => depot.ObtenirAsync(idTache, ct);

    public Task<IReadOnlyList<TacheUtilisateur>> ObtenirTachesOuvertesAsync(IdInstance idInstance, CancellationToken ct)
        => depot.OuvertesAsync(idInstance.Valeur, ct);

    public Task CompleterTacheAsync(long idTache, IReadOnlyDictionary<string, object?>? donnees, string? utilisateur, CancellationToken ct)
        => depot.ChangerStatutAsync(idTache, StatutTache.Completee, ct);      // idempotent

    public Task AssignerTacheAsync(long idTache, string utilisateur, CancellationToken ct)
        => depot.AssignerAsync(idTache, utilisateur, ct);

    public Task AnnulerTacheAsync(long idTache, string motif, CancellationToken ct)
        => depot.ChangerStatutAsync(idTache, StatutTache.Annulee, ct);        // idempotent

    public Task RenommerNoeudAsync(IdInstance idInstance, string ancienCode, string nouveauCode, CancellationToken ct)
        => depot.RenommerNoeudAsync(idInstance.Valeur, ancienCode, nouveauCode, ct);
}
```

Si les tâches sont en base, utiliser la **même connexion et transaction** que Sentier (injecter `IDbConnection`
ou `ISessionSentier`). Le balayage ignore les décorateurs (classes dont un constructeur reçoit un
`IGestionnaireTaches`). Sans indication, l'unique implémentation trouvée est enregistrée automatiquement ;
`UtiliserGestionnaireTaches<T>()` est nécessaire s'il en reste plusieurs (sinon erreur au démarrage, même avec Autofac)
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
| `IContexteUtilisateur` | `string? Utilisateur { get; }` | Auteur par défaut des actions (audit) quand l'appel n'en précise pas |
| `IHorloge` | `DateTime MaintenantUtc { get; }` | Heure courante (UTC) ; une horloge simulée permet de tester les échéances |
| `IIntercepteurCommande` | `Task ExecuterAsync(string nomCommande, ContexteCommande contexte, Func<Task> suivant, ct)` | Code autour de chaque commande ; appeler `suivant()` |
| `IIntercepteurRequete` | `Task<bool> EvaluerAsync(string nomRequete, ContexteRequete contexte, Func<Task<bool>> suivant, ct)` | Code autour de chaque requête ; retourner le résultat de `suivant()` |
| `ISerialiseurVariables` | `string Serialiser(valeurs)` ; `Dictionary<string, object?> Deserialiser(json, types)` | Remplacer le stockage JSON des variables |
| `ISessionSentier` | `IDbConnection Connexion` ; `IDbTransaction? Transaction` | Session sur mesure (§ 3.1), par `UtiliserSession<T>()` |

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
| Batch des échéances | `ObtenirEcheancesDuesAsync`, puis `TraiterEcheanceAsync` par échéance, chacune dans sa transaction ; `MarquerEnErreurAsync` en cas d'échec |
| Consulter | `ISuiviSentier` : instance, historique, attentes, tâches ouvertes, instances en erreur, diagramme |

Les cas « rien à faire » (tâche déjà complétée, signal sans destinataire…) retournent `SansEffet` sans exception.
Les vraies erreurs sont propagées : la transaction du client n'est pas validée.

## 7. Liste de contrôle

- [ ] `ModuleSentier` (ou `AddSentier`) avec préfixe, dialecte et `BalayerAssemblyDe<T>()` pour chaque assembly concerné.
- [ ] Une portée par unité de travail, avec `IDbConnection` ouverte et la transaction (§ 2).
- [ ] Une `DefinitionSentier` + `[ProcessusSentier]` par processus ; `DeployerDefinitionsAsync()` au démarrage.
- [ ] Une `ICommandeSentier` par commande référencée ; une `IRequeteSentier` par décision calculée.
- [ ] `IGestionnaireTaches` (7 méthodes) si un processus a un nœud interactif ; `CompleterTacheAsync` et `AnnulerTacheAsync` idempotentes.
- [ ] Un batch des échéances si un processus utilise `.Echeance(...)` ou une attente temporelle.
- [ ] `IPlanMigration` pour chaque migration non compatible.
- [ ] (Optionnel) `IContexteUtilisateur`, intercepteurs, `IHorloge`, `ISerialiseurVariables`.
