# Utiliser Sentier dans un projet client

Guide de référence pour l'équipe d'une application qui utilise Sentier. Il peut être copié tel quel dans le
dépôt du projet client (`docs/sentier.md`, par exemple) : il couvre ce qu'il faut **écrire**, **enregistrer** et
**appeler**, et les règles à respecter pour ne pas casser les instances en cours.

Sommaire

1. [Ce que fait Sentier, ce que fait le client](#1-ce-que-fait-sentier-ce-que-fait-le-client)
2. [Installation et schéma](#2-installation-et-schéma)
3. [Enregistrement dans l'injection de dépendances](#3-enregistrement-dans-linjection-de-dépendances)
4. [Unité de travail : connexion et transaction](#4-unité-de-travail--connexion-et-transaction)
5. [Organisation du projet](#5-organisation-du-projet)
6. [Définir un processus](#6-définir-un-processus)
7. [Commandes et requêtes](#7-commandes-et-requêtes)
8. [Gestionnaire de tâches (bassin de tâches)](#8-gestionnaire-de-tâches-bassin-de-tâches)
9. [Déploiement au démarrage](#9-déploiement-au-démarrage)
10. [Appels de l'application](#10-appels-de-lapplication)
11. [Batch des échéances](#11-batch-des-échéances)
12. [Tests avec SQLite](#12-tests-avec-sqlite)
13. [Règles de nommage et d'évolution](#13-règles-de-nommage-et-dévolution)
14. [Liste de contrôle](#14-liste-de-contrôle)
15. [Erreurs fréquentes](#15-erreurs-fréquentes)

---

## 1. Ce que fait Sentier, ce que fait le client

| Sentier | Le client |
|---|---|
| Stocke les définitions (versionnées), les instances, leurs variables, l'historique des nœuds et les attentes. | Fournit la connexion **et la transaction**, valide (`Commit`) ou annule. |
| Exécute les nœuds de façon **synchrone**, dans l'appel, jusqu'au prochain arrêt (attente, tâche, sous-processus, fin). | Écrit les processus, les commandes (écritures métier) et les requêtes (lectures pour les décisions). |
| Délègue chaque étape de vie d'une tâche au gestionnaire du client. | Garde les tâches dans son **bassin de tâches** (`IGestionnaireTachesSentier`). |
| Ne lance aucun thread. | Planifie le **batch des échéances** si un processus en utilise. |

Une **instance** = un processus (clé texte, ex. `"demande-conge"`) + une **clé d'affaire** (`long`, ex. n° de dossier).
Par défaut, une seule instance active par processus et par clé d'affaire.

Tous les types que le client implémente ou reçoit dans ses implémentations portent le suffixe **`Sentier`**
(`ICommandeSentier`, `ContexteCommandeSentier`, `IGestionnaireTachesSentier`, `DemandeTacheSentier`, `TacheSentier`,
`IHorlogeSentier`…) : ils ne se confondent pas avec les classes du client ni avec celles de son bassin de tâches.

## 2. Installation et schéma

| Package | Quand |
|---|---|
| `Sentier.DependencyInjection` | Toute application, sur `Microsoft.Extensions.DependencyInjection` ou Autofac. |
| `Sentier.Persistance` | Dépôts et dialectes (Oracle en production, SQLite pour les tests) ; amené par `Sentier.DependencyInjection`. |
| `Microsoft.Data.Sqlite` | Projet de tests. |

Sentier ne travaille qu'avec `IDbConnection` : le fournisseur Oracle est celui que l'application utilise déjà.

**Préfixe** : lettres, chiffres et `_`, 12 caractères au plus. Toutes les tables, la vue et les séquences le portent
(`SENTIER_DEFIN_PROCS`, `SENTIER_INSTC_PROCS`…).

- **Oracle** : remettre au DBA `Scripts/oracle.sql` (jeton `{{P}}` à remplacer par le préfixe), ou le générer :
  `scope.Resolve<InstallateurSchemaSentier>().GenererScript()`.
- **SQLite** : `await scope.Resolve<InstallateurSchemaSentier>().InstallerAsync();`

## 3. Enregistrement dans l'injection de dépendances

```csharp
services.AddScoped<IDbConnection>(_ => fabrique.Ouvrir());
services.AddSentier(o => o
    .UtiliserOracle("SENTIER")                 // UtiliserSqlite("SENTIER") pour les tests
    .BalayerAssemblyDe<DemandeConge>());       // une fois par assembly contenant processus, commandes, requêtes, bassin
services.AddScoped<IContexteUtilisateurSentier, UtilisateurWeb>();      // optionnel
services.AddScoped<IIntercepteurCommandeSentier, JournalCommandes>();   // optionnel
```

**Autofac** : rien de plus avec `AutofacServiceProviderFactory` ; avec un `ContainerBuilder` seul, `builder.Populate(services)` après `AddSentier` (`Autofac.Extensions.DependencyInjection`). Un service enregistré dans le `ContainerBuilder` après `Populate` remplace celui de Sentier.

### Ce que le balayage trouve (rien à enregistrer pour eux)

| Type | Rôle |
|---|---|
| `DefinitionSentier` + `[ProcessusSentier("cle")]` | Un processus. |
| `ICommandeSentier` / `CommandeSentier<T>` | Commandes, enregistrées en Scoped. |
| `IRequeteSentier` / `RequeteSentier<T>` | Requêtes de décision, enregistrées en Scoped. |
| `IGestionnaireTachesSentier` | Le bassin de tâches (si plusieurs : `o.UtiliserGestionnaireTaches<T>()`). |
| `IPlanMigration` | Plans de migration d'instances. |

### Contrats optionnels (à enregistrer soi-même)

| Contrat | Usage |
|---|---|
| `IContexteUtilisateurSentier` | Utilisateur courant, pour l'audit (`utilisateur` par défaut des appels). |
| `IIntercepteurCommandeSentier` / `IIntercepteurRequeteSentier` | Code **autour** de toutes les commandes / requêtes (journal, mesure). Facultatif : il n'en faut pas un par commande. |
| `IHorlogeSentier` | Heure courante (tests des échéances). |
| `ISerialiseurVariablesSentier` | Format de stockage des variables (JSON par défaut). |

`AddSentier` n'enregistre **jamais** d'`IDbConnection`.

## 4. Unité de travail : connexion et transaction

Sentier lit la connexion **dans la portée DI** ; aucun appel ne prend de connexion en paramètre. Le client
n'enregistre que son `IDbConnection` : l'`IDbTransaction` est facultative. Sentier n'ouvre, ne valide et n'annule
**jamais** de transaction, mais en exige une pour écrire. Centraliser ce code dans une classe réutilisée par les
contrôleurs, le batch et les tests. Pour une application Oracle dont les appels Dapper passent `transaction: null` :

```csharp
public sealed class UniteTravailSentier(ILifetimeScope container, IFabriqueConnexions fabrique)
{
    public async Task<T> ExecuterAsync<T>(Func<ILifetimeScope, Task<T>> action)
    {
        using var portee = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);
        using var connexion = fabrique.Ouvrir();                                       // ouverte DANS la portée : enrôlée

        await using var scope = container.BeginLifetimeScope(b =>
            b.RegisterInstance(connexion).As<IDbConnection>().ExternallyOwned());      // le client en reste propriétaire

        var resultat = await action(scope);   // une exception remonte : pas de Complete, tout est annulé
        portee.Complete();
        return resultat;
    }
}
```

- La connexion est ouverte **dans** la portée (sinon elle n'y est pas enrôlée) ; `TransactionScopeAsyncFlowOption.Enabled`
  est nécessaire avec `async`. Sentier exécute alors ses requêtes avec `transaction: null` sur la connexion enrôlée.
- Si l'application a déjà son unité de travail, y enregistrer **sa** connexion : écritures métier et écritures de
  Sentier sont validées ensemble.
- **Variante `IDbTransaction`** (application qui ouvre elle-même sa transaction, ou SQLite, qui ne s'enrôle pas dans
  un `TransactionScope`) : enregistrer aussi `b.RegisterInstance(transaction).As<IDbTransaction>().ExternallyOwned();`
  et appeler `transaction.Commit()` ; avec `Microsoft.Extensions.DependencyInjection`,
  `scope.ServiceProvider.GetRequiredService<IAccesseurTransactionSentier>().Transaction = transaction;`.
- **Wrapper maison autour de Dapper** (ex. `IAccesBDPlus`) : un adaptateur `IExecuteurSqlSentier` et
  `o.UtiliserExecuteurSql<T>()` font passer les requêtes de Sentier par ce wrapper ; plus aucune `IDbConnection` à
  enregistrer (voir [integration-client.md](integration-client.md), § 3.2).
- Toute écriture sans transaction (ni `TransactionScope`, ni `IDbTransaction`) lève `TransactionRequiseException`.
  Les lectures (`ISuiviSentier`) acceptent l'absence de transaction.

## 5. Organisation du projet

Une organisation qui fonctionne bien, par processus :

```
Processus/
  DemandeConge/
    DemandeConge.cs              // DefinitionSentier : variables + déroulement
    Commandes/CalculerSolde.cs   // ICommandeSentier
    Requetes/DossierComplet.cs   // IRequeteSentier (seulement si une condition sur variable ne suffit pas)
    Migrations/PlanCongeV3.cs    // IPlanMigration, au besoin
Sentier/
  UniteTravailSentier.cs
  BassinTachesSentier.cs         // IGestionnaireTachesSentier
  UtilisateurWeb.cs              // IContexteUtilisateurSentier
```

## 6. Définir un processus

Une classe par processus. Les champs `Variable<T>` statiques sont les variables déclarées ; le début est implicite ;
enchaîner deux nœuds crée la transition.

```csharp
[ProcessusSentier("demande-conge", Nom = "Demande de congé")]
public sealed class DemandeConge : DefinitionSentier
{
    public static readonly Variable<long> NbJours = Variable.Entier("nbJours").Obligatoire();
    public static readonly Variable<long> Solde = Variable.Entier("solde").ParDefaut(0);
    public static readonly Variable<string> Gestionnaire = Variable.Texte("gestionnaire");
    public static readonly Variable<string> Decision = Variable.Texte("decision");

    private const string AttenteRh = "attendre-rh";

    protected override void Definir(ConstructeurProcessus processus) => processus
        .Systemique<CalculerSolde>("calculer-solde", p => p.CleAffaire("employe"))
        .Decision("solde-suffisant", d => d
            .Si(NbJours).SuperieurA(Solde).Nommee("solde-insuffisant").Vers("refusee")
            .Sinon())
        .Interactif("approbation", "Approuver la demande", i => i
            .Role("gestionnaire")
            .Assigne(Gestionnaire)                       // assignation automatique, lue à la création de la tâche
            .Donnees(NbJours)
            .CommandeSortie<Journaliser>())
        .Decision("est-approuvee", d => d
            .Si(Decision).EgalA("approuvee")
            .Sinon("refusee"))
        .Attente(AttenteRh, a => a
            .Signal("VALIDATION_RH")
            .Echeance(TimeSpan.FromDays(2)).Alors(b => b.Systemique<Relancer>("relancer").Vers(AttenteRh)))
        .Fin("approuvee")
        .Fin("refusee");
}
```

### Les nœuds

| Nœud | Rôle |
|---|---|
| `.Systemique<TCommande>(code[, p => …])` | Exécute une commande. |
| `.Decision(code, d => …)` | Choisit une branche : conditions sur variable ou requêtes, dans l'ordre ; puis `Sinon`. |
| `.Interactif(code, nomTache, i => …)` | Crée une tâche dans le bassin du client et attend sa complétion. |
| `.Attente(code, a => …)` / `.AttendreSignal(…)` / `.AttendreDelai(…)` | Attend un ou plusieurs signaux et/ou une échéance. |
| `.SousProcessus<TEnfant>(code, s => …)` | Démarre une instance enfant et attend sa fin. |
| `.Fin(code)` | Termine l'instance (le code de fin est consultable). |

Destination d'une branche : `.Alors(b => …)` (bloc imbriqué), `.Vers("code")` (lien), rien (nœud suivant).

### Décisions : condition sur variable ou requête

Pour comparer une variable, **pas besoin de requête** :

| Opérateur | Types |
|---|---|
| `EgalA`, `DifferentDe` | tous sauf JSON |
| `SuperieurA`, `SuperieurOuEgalA`, `InferieurA`, `InferieurOuEgalA` | entier, décimal, date, texte |
| `Contient`, `CommencePar` | texte (sensible à la casse) |
| `EstVrai`, `EstFaux` | booléen |
| `EstRenseignee`, `EstVide` | tous |

La comparaison se fait avec une valeur fixe (`.SuperieurA(1000m)`) ou une autre variable (`.SuperieurA(Solde)`).
Une requête (`.Si<DossierComplet>(p => …)`) reste nécessaire quand la décision lit autre chose que les variables
(base métier, service).

### Paramètres des commandes et requêtes

`p.Valeur("nom", valeur)` (fixe), `p.Variable(V)` / `p.Variables(V1, V2)`, `p.VariableOptionnelle(V[, défaut])`,
`p.CleAffaire("nom")`.

`Construire()` (et le déploiement) valide toute la définition et lève `DefinitionInvalideException` avec la liste
complète des erreurs. `ISuiviSentier.ExporterDiagrammeAsync("cle")` produit un diagramme Mermaid.

## 7. Commandes et requêtes

Elles sont **désignées par leur classe** dans les définitions (`Systemique<T>`, `Si<T>`, `CommandeEntree<T>`…) :
aucune chaîne à écrire. Elles peuvent injecter les services de l'application et travaillent dans la même transaction.

```csharp
public sealed class CalculerSolde(IDepotEmployes employes) : ICommandeSentier
{
    public async Task ExecuterAsync(ContexteCommandeSentier contexte, CancellationToken ct)
    {
        var solde = await employes.SoldeAsync(contexte.Parametres.Lire<long>("employe"), ct);
        contexte.Variables.Ecrire(DemandeConge.Solde, solde);     // variable déclarée et du bon type, sinon erreur
    }
}

public sealed class ParametresDossier { public long Dossier { get; set; } }

public sealed class DossierComplet(IDepotDossiers dossiers) : RequeteSentier<ParametresDossier>
{
    protected override async Task<bool> EvaluerAsync(ContexteRequeteSentier contexte, ParametresDossier p, CancellationToken ct)
        => (await dossiers.ObtenirAsync(p.Dossier, ct)).EstComplet;
}
```

| `ContexteCommandeSentier` | |
|---|---|
| `IdInstance`, `CleProcessus`, `CleAffaire`, `CodeNoeud` | Où l'on est. |
| `Parametres` | `Lire<T>`, `LireOuDefaut<T>`, `Contient`, `Lier<T>()` (par nom de propriété, sans casse). |
| `Variables` | `Lire(V)`, `TryLire`, `LireOuDefaut`, `Ecrire(V, valeur)`, `Effacer`. |
| `Moment` | `Execution`, `Entree`, `Sortie`, `Migration`. |
| `DonneesTache` | Données de complétion (commande de sortie d'un interactif). |
| `Utilisateur` | Auteur de l'action. |

`ContexteRequeteSentier` : la même chose en lecture seule, sans `Moment`, `DonneesTache` ni `Utilisateur`.

Règles :

- une **requête ne modifie rien** ;
- une commande peut être **rappelée** (relance d'une instance en erreur) : la rendre idempotente si possible ;
- une exception est **propagée** au client, qui annule sa transaction.

## 8. Gestionnaire de tâches (bassin de tâches)

Obligatoire dès qu'un processus a un nœud `Interactif` (sinon le déploiement échoue). **Sentier ne stocke aucune
tâche** : il garde seulement l'identifiant de la tâche attendue par l'instance et délègue tout le reste au bassin.

```csharp
public sealed class BassinTachesSentier(IDepotTaches depot) : IGestionnaireTachesSentier
{
    public Task<long> CreerTacheAsync(DemandeTacheSentier demande, CancellationToken ct)
        => depot.InsererAsync(new Tache
        {
            IdInstance = demande.IdInstance.Valeur,
            CodeNoeud = demande.CodeNoeud,
            Titre = demande.NomTache,
            Role = demande.Role,
            Assigne = demande.Assigne,          // assignation automatique ; null : tâche non assignée
            Echeance = demande.DateEcheance,
        }, ct);

    public Task<TacheSentier?> ObtenirTacheAsync(long idTache, CancellationToken ct) => depot.ObtenirAsync(idTache, ct);

    public Task<IReadOnlyList<TacheSentier>> ObtenirTachesOuvertesAsync(IdInstance idInstance, CancellationToken ct)
        => depot.OuvertesAsync(idInstance.Valeur, ct);

    public Task CompleterTacheAsync(long idTache, IReadOnlyDictionary<string, object?>? donnees, string? utilisateur, CancellationToken ct)
        => depot.ChangerStatutAsync(idTache, StatutTacheSentier.Completee, ct);    // idempotente

    public Task AssignerTacheAsync(long idTache, string utilisateur, CancellationToken ct)
        => depot.AssignerAsync(idTache, utilisateur, ct);

    public Task AnnulerTacheAsync(long idTache, string motif, CancellationToken ct)
        => depot.ChangerStatutAsync(idTache, StatutTacheSentier.Annulee, ct);      // idempotente

    public Task RenommerNoeudAsync(IdInstance idInstance, string ancienCode, string nouveauCode, CancellationToken ct)
        => depot.RenommerNoeudAsync(idInstance.Valeur, ancienCode, nouveauCode, ct);
}
```

### Les deux types échangés

| | Sens | Contenu |
|---|---|---|
| `DemandeTacheSentier` | Sentier → bassin, une fois, à `CreerTacheAsync` | `IdInstance`, `CleProcessus`, `CleAffaire`, `CodeNoeud`, `NomTache`, `Role`, `Assigne`, `DateEcheance`, `Donnees` |
| `TacheSentier` | bassin → Sentier, à chaque lecture | `Id`, `IdInstance`, `CodeNoeud`, `Statut` (`Ouverte`, `Completee`, `Annulee`) |

### Cycle de vie

| Étape | Qui | Comment |
|---|---|---|
| Création | Sentier | Entrée du nœud interactif → `CreerTacheAsync` (après les commandes d'entrée). |
| Assignation automatique | Sentier | `.Assigne("jdoe")` ou `.Assigne(variable)` → `demande.Assigne`, à la création seulement. |
| Réassignation, libération | **Le bassin**, directement | Sentier ne garde pas l'assigné. Ou `IServiceSentier.AssignerTacheAsync(idTache, utilisateur)`, qui vérifie d'abord que l'instance attend cette tâche. |
| Complétion | L'application | **Toujours** `IServiceSentier.CompleterTacheAsync(idTache, données, utilisateur)` : fusion des données, commandes de sortie, puis `CompleterTacheAsync` du bassin, et l'instance avance. |
| Annulation | Sentier | Annulation de l'instance, relance, migration → `AnnulerTacheAsync`. |

À ne **pas** faire directement dans le bassin : compléter la tâche (l'instance n'avancerait pas), l'annuler
(l'instance resterait bloquée), la recréer sous un autre identifiant (l'instance attend l'ancien).

Exigences : même connexion et même transaction que Sentier si les tâches sont en base (injecter `IDbConnection`
ou `ISessionSentier`) ; `CompleterTacheAsync` et `AnnulerTacheAsync` idempotentes ; `ObtenirTachesOuvertesAsync`
doit fonctionner sans transaction.

## 9. Déploiement au démarrage

Une fois au démarrage de l'application, dans une transaction :

```csharp
var rapport = await uniteTravail.ExecuterAsync(s => s.Resolve<IServiceSentier>().DeployerDefinitionsAsync());
foreach (var d in rapport.Definitions)
{
    journal.LogInformation("{Cle} v{Version} : {Etat}", d.CleProcessus, d.Version, d.Etat);
    foreach (var a in d.Avertissements) journal.LogWarning("{Cle} : {Message}", d.CleProcessus, a);
}
```

- Définition inchangée : rien n'est créé. Modifiée : **nouvelle version** ; les instances en cours gardent la leur
  (sauf `MigrationAutomatique()` pour les changements compatibles, ou un plan de migration).
- Le déploiement **valide tout** et ne déploie rien en cas d'erreur : structure des définitions, commandes et
  requêtes enregistrées, bassin de tâches présent, sous-processus cohérents, **et** commandes et requêtes des
  versions antérieures encore suivies par des instances actives (voir § 13).

## 10. Appels de l'application

Écritures par `IServiceSentier`, dans l'unité de travail ; lectures par `ISuiviSentier` (transaction facultative).

```csharp
var sentier = scope.Resolve<IServiceSentier>();
var suivi = scope.Resolve<ISuiviSentier>();
```

| Besoin | Appel |
|---|---|
| Démarrer | `sentier.DemarrerAsync("demande-conge", cleAffaire: 42, ValeursVariables.Avec(DemandeConge.NbJours, 3L))` |
| Compléter une tâche | `sentier.CompleterTacheAsync(idTache, new Dictionary<string, object?> { ["decision"] = "approuvee" }, "jdoe")` |
| Réassigner (avec vérification) | `sentier.AssignerTacheAsync(idTache, "jdoe")` |
| Envoyer un signal | `sentier.EnvoyerSignalAsync("VALIDATION_RH", "demande-conge", cleAffaire: 42[, chargeUtile])` ou `EnvoyerSignalCorrelationAsync(nom, cle)` |
| Corriger des variables | `sentier.ModifierVariablesAsync(idInstance, ValeursVariables.Avec(DemandeConge.NbJours, 5L), "jdoe")` |
| Annuler | `sentier.AnnulerAsync(idInstance, "Demande retirée")` |
| Relancer une instance en erreur | `sentier.ReprendreAsync(idInstance)` |
| État d'une instance | `suivi.ObtenirInstanceAsync(idInstance)` |
| Instances d'une clé d'affaire | `suivi.ObtenirInstancesParCleAffaireAsync(42, ["demande-conge"])` |
| Recherche par variable | `suivi.RechercherParVariableAsync("demande-conge", DemandeConge.Decision, "refusee")` |
| Recherche multicritère | `suivi.RechercherParVariablesAsync(new RechercheParVariables { CleProcessus = "demande-conge", Filtres = [FiltreVariable.SuperieurOuEgal(DemandeConge.NbJours, 10L)], Statuts = [StatutInstance.Suspendue] })` |
| Historique, attentes, tâches ouvertes | `ObtenirHistoriqueAsync`, `ObtenirAttentesAsync`, `ObtenirTachesOuvertesAsync` |
| Instances en erreur | `suivi.ObtenirInstancesEnErreurAsync(page, taille)` |
| Diagramme | `suivi.ExporterDiagrammeAsync("demande-conge")` |

**Résultat.** Les écritures retournent un `ResultatExecution` : `IdInstance`, `Statut`, `CodeNoeudCourant`,
`IdTacheCourante`, `CodeFin`, et `SansEffet` + `Raison` quand il n'y avait rien à faire (tâche déjà complétée,
signal sans destinataire, instance déjà active pour la clé, instance terminée…). Les vraies erreurs sont des
exceptions : la transaction ne doit pas être validée.

**Corriger des variables** : instance suspendue ou en erreur seulement ; variables déclarées et du bon type ; `null`
efface une variable non obligatoire. Les valeurs déjà calculées à l'entrée du nœud courant (échéance, clé de
corrélation, données de la tâche) ne sont pas recalculées.

**Recherche par variables** : filtres combinés en ET, évalués avec le type déclaré par la version de chaque instance,
des plus récentes aux plus anciennes, bornés par `TailleMax` (100 par défaut). Les variables sont stockées en JSON :
filtrer aussi par statut sur les gros volumes.

## 11. Batch des échéances

Seulement si un processus utilise une échéance. Chaque échéance dans **sa propre transaction** ; un échec marque
l'instance en erreur dans une autre transaction :

```csharp
var dues = await uniteTravail.ExecuterAsync(s => s.Resolve<IServiceSentier>().ObtenirEcheancesDuesAsync(tailleMax: 200));
foreach (var echeance in dues)
{
    try
    {
        await uniteTravail.ExecuterAsync(s => s.Resolve<IServiceSentier>().TraiterEcheanceAsync(echeance.IdAttente));
    }
    catch (Exception ex)
    {
        await uniteTravail.ExecuterAsync(s => s.Resolve<IServiceSentier>().MarquerEnErreurAsync(echeance.IdInstance, ex.Message));
    }
}
```

Plusieurs batchs peuvent tourner en parallèle (verrouillage sans attente sous Oracle).

## 12. Tests avec SQLite

Même code qu'en production ; seuls le dialecte et le bassin changent.

```csharp
var services = new ServiceCollection();
services.AddSentier(o => o.UtiliserSqlite("SENTIER").BalayerAssemblyDe<DemandeConge>());
var builder = new ContainerBuilder();
builder.Populate(services);
builder.RegisterType<BassinTachesMemoire>().As<IGestionnaireTachesSentier>().InstancePerLifetimeScope();
builder.RegisterInstance(horloge).As<IHorlogeSentier>();      // horloge de test pour avancer le temps
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
var r = await EnTransaction(s => s.Resolve<IServiceSentier>().DemarrerAsync("demande-conge", 42, ValeursVariables.Avec(DemandeConge.NbJours, 3L)));
Assert.Equal("approbation", r.CodeNoeudCourant);
```

Un test qui construit toutes les définitions attrape leurs erreurs avant tout déploiement :

```csharp
foreach (var type in typeof(DemandeConge).Assembly.GetTypes().Where(t => typeof(DefinitionSentier).IsAssignableFrom(t) && !t.IsAbstract))
{
    ((IDefinitionSentier)Activator.CreateInstance(type)!).Definir();
}
```

## 13. Règles de nommage et d'évolution

Ce qui est **enregistré en base** est un contrat avec les instances en cours :

| Élément | Stocké sous | Renommer sans précaution |
|---|---|---|
| Clé du processus | `[ProcessusSentier("cle")]` | Nouveau processus ; les appels `DemarrerAsync("cle")` et les instances ne suivent plus. **Toujours** fixer la clé par l'attribut. |
| Code d'un nœud | `"approbation"` | Les instances arrêtées sur l'ancien code ne le trouvent plus : plan de migration. |
| Nom d'une variable | `Variable.Texte("decision")` | Valeur perdue pour les instances en cours : plan de migration (`RenommerVariable`). |
| Nom d'une sortie | `.Nommee(…)`, sinon calculé | Historique et règles de migration. |
| Classe de commande ou de requête | Nom de la classe | Les versions déjà déployées citent l'ancien nom. |

**Renommer une classe de commande ou de requête** : ajouter l'ancien nom à la classe renommée, tant que des instances
actives suivent les anciennes versions :

```csharp
[CommandeSentier("CalculerSolde")]          // ancien nom de la classe
public sealed class CalculerSoldeConge : ICommandeSentier { … }
```

Le déploiement le vérifie : s'il manque, il échoue en indiquant le processus, la version, le nombre d'instances et
l'attribut à ajouter. Deux classes du même nom (espaces de noms différents) font échouer le démarrage.

Bonnes pratiques : codes de nœuds en kebab-case au sens métier, en constantes lorsqu'ils sont la cible d'un lien ;
chemin principal dans la suite du déroulement, exceptions dans les branches ; « une décision lit, une commande écrit ».

## 14. Liste de contrôle

- [ ] Tables créées avec le bon préfixe (script DBA, ou `InstallerAsync` en test).
- [ ] `AddSentier` avec le même préfixe et `BalayerAssemblyDe<…>()` pour chaque assembly concerné.
- [ ] Une unité de travail qui enregistre l'`IDbConnection` (`ExternallyOwned`), ouverte dans un `TransactionScope` (Oracle) ou avec une `IDbTransaction`, et valide — ou, avec un wrapper maison, un adaptateur `IExecuteurSqlSentier` (`UtiliserExecuteurSql<T>()`) et un `TransactionScope`.
- [ ] Une `DefinitionSentier` par processus, clé fixée par `[ProcessusSentier("cle")]`.
- [ ] Commandes et requêtes dans un assembly balayé.
- [ ] `IGestionnaireTachesSentier` si un nœud interactif existe : lit `demande.Assigne`, même transaction, complétion et annulation idempotentes.
- [ ] Complétion des tâches toujours par `IServiceSentier.CompleterTacheAsync`.
- [ ] `DeployerDefinitionsAsync` au démarrage, rapport journalisé.
- [ ] Batch des échéances planifié si un processus a une échéance.
- [ ] (Optionnel) `IContexteUtilisateurSentier`, intercepteurs, `IHorlogeSentier` de test.

## 15. Erreurs fréquentes

| Symptôme | Cause | Correction |
|---|---|---|
| `TransactionRequiseException` | Ni `TransactionScope` actif ni `IDbTransaction`, ou transaction déjà validée ; ou `TransactionScope` avec SQLite. | Oracle : ouvrir la connexion dans un `TransactionScope`. Sinon : enregistrer une `IDbTransaction` (ou alimenter `IAccesseurTransactionSentier`) avant l'appel. |
| « Aucune IDbConnection n'est enregistrée » | Service résolu depuis le conteneur racine, ou application qui passe par son propre wrapper. | Résoudre depuis la portée de l'unité de travail ; avec un wrapper maison, `UtiliserExecuteurSql<T>()`. |
| Connexion disposée à la fin de la portée | Enregistrée sans `ExternallyOwned()`. | Ajouter `ExternallyOwned()`. |
| `DefinitionInvalideException` à la construction | Définition incohérente. | Lire la liste complète des erreurs de l'exception. |
| Déploiement refusé : commande ou requête non enregistrée | Classe hors des assemblys balayés, ou renommée alors qu'une ancienne version l'utilise. | `BalayerAssemblyDe<…>()`, ou `[CommandeSentier("AncienNom")]` / `[RequeteSentier("AncienNom")]`. |
| Déploiement refusé : nœud interactif | Aucun `IGestionnaireTachesSentier`. | L'implémenter dans un assembly balayé, ou `UtiliserGestionnaireTaches<T>()`. |
| `VariableInvalideException` | Variable non déclarée, mauvais type, ou obligatoire manquante/effacée. | Déclarer le champ `Variable<T>` ; respecter le type. |
| Démarrage `SansEffet` | Instance déjà active pour ce processus et cette clé. | Normal ; `InstancesMultiples()` pour lever la règle. |
| Complétion `SansEffet` « l'instance n'attend pas la tâche » | Tâche recréée sous un autre identifiant, ou périmée. | Garder l'identifiant retourné par `CreerTacheAsync`. |
| Instance bloquée sur un nœud interactif | Tâche annulée ou complétée directement dans le bassin. | Passer par `CompleterTacheAsync` / `AnnulerAsync` de Sentier. |
| L'assigné automatique n'apparaît pas | `CreerTacheAsync` ignore `demande.Assigne`. | Créer la tâche déjà assignée à `demande.Assigne`. |
| `ConflitConcurrenceException` | Deux transactions ont modifié la même instance. | Annuler et réessayer l'opération. |
