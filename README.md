# Sentier

Moteur de processus (BPM) léger, distribué en packages NuGet pour .NET 10.

Pour une première intégration, suivre le [guide de démarrage](docs/demarrage.md). Le [guide du projet client](docs/guide-projet-client.md) réunit en une page, à copier dans le dépôt de l'application, tout ce qu'il faut écrire, enregistrer et appeler, et les règles d'évolution. La liste de tout ce que l'application client doit implémenter et enregistrer est dans [docs/integration-client.md](docs/integration-client.md).

Les noms des objets de base suivent les mnémoniques de BpmPlus ; voir [docs/mnemoniques.md](docs/mnemoniques.md).

Exemple complet d'un processus parent composé de sous-processus (corrections, autorisation, délai, signaux), piloté par la seule clé d'affaire : [exemples/DeclarationSousProcessus](exemples/DeclarationSousProcessus/README.md).

- **Exécution synchrone en chaîne** : les nœuds s'enchaînent jusqu'à une fin ou un nœud suspensif (attente, tâche, sous-processus).
- **Une seule transaction, celle du client** (`TransactionScope` ou `IDbTransaction`) : Sentier n'ouvre, ne valide et n'annule jamais de transaction.
- **Une instance = un processus + une clé d'affaire** (`long`).
- **Persistance Dapper**, Oracle (production) et SQLite (tests), avec un préfixe configurable pour les objets.

| Package | Contenu |
|---|---|
| `Sentier.Core` | Domaine, constructeur de définitions, moteur, abstractions. Aucune dépendance à une base. |
| `Sentier.Persistance` | Dépôts Dapper, noms préfixés, dialectes Oracle et SQLite (`IDialecteSql`), scripts DDL, installateur de schéma. |
| `Sentier.DependencyInjection` | Extension `AddSentier`. |

## 1. Enregistrement

```csharp
// La connexion appartient au client : Scoped, déjà ouverte au moment des appels.
services.AddScoped<IDbConnection>(_ => OuvrirConnexionOracle());

services.AddSentier(o =>
{
    o.PrefixeTables = "SENTIER";              // obligatoire : lettres, chiffres, '_' (12 caractères max)
    o.UtiliserOracle();                       // ou o.UtiliserSqlite()
    o.BalayerAssemblyDe<Program>();           // commandes, requêtes, définitions, plans de migration, gestionnaire de tâches
    o.MaxEtapesParExecution = 1000;           // protection contre les boucles infinies
});
```

**Autofac** : rien de plus avec `AutofacServiceProviderFactory` ; avec un `ContainerBuilder` seul, `builder.Populate(services)` après `AddSentier` (`Autofac.Extensions.DependencyInjection`). Un service enregistré dans le `ContainerBuilder` après `Populate` remplace celui de Sentier.

`UtiliserOracle(prefixe)` et `UtiliserSqlite(prefixe)` renseignent aussi `PrefixeTables`.

`AddSentier` n'enregistre **jamais** d'`IDbConnection`. Services optionnels à enregistrer par le client : `IContexteUtilisateurSentier` (utilisateur courant, pour l'audit), `IIntercepteurCommandeSentier`, `IIntercepteurRequeteSentier`, et un `IHorlogeSentier` ou `ISerialiseurVariablesSentier` personnalisé.

Le schéma peut être créé par `InstallateurSchemaSentier.InstallerAsync()` (pratique avec SQLite) ou remis à un DBA via `GenererScript()`. Les scripts sont aussi dans `src/Sentier.Persistance/Scripts/Oracle` et `src/Sentier.Persistance/Scripts/Sqlite`.

## 2. Connexion et transaction

Sentier obtient la connexion et la transaction par `ISessionSentier` (Scoped). L'implémentation par défaut résout l'`IDbConnection` de la portée et, **si le client en a une**, l'`IDbTransaction` (alimentée dans `IAccesseurTransactionSentier`, sinon enregistrée dans la portée). Les appels aux services ne prennent aucun paramètre de connexion.

Le client **doit** enregistrer l'`IDbConnection`. Pour la transaction, deux façons de faire sont équivalentes ; il suffit d'en utiliser une :

| Mode | Le client fournit | Base |
|---|---|---|
| Transaction ambiante | `IDbConnection` seule, ouverte dans un `TransactionScope` | Oracle |
| Transaction explicite | `IDbConnection` + `IDbTransaction` | Oracle, SQLite |

**Transaction ambiante (`TransactionScope`).** C'est le cas d'une application dont les appels Dapper passent `transaction: null` : elle n'enregistre que sa connexion, aucune `IDbTransaction`. Sentier exécute alors ses requêtes avec `transaction: null` sur la connexion enrôlée.

```csharp
using var portee = new TransactionScope(TransactionScopeAsyncFlowOption.Enabled);
using var connexion = _fabriqueConnexions.Ouvrir();                           // ouverte DANS la portée : enrôlée

using var scope = _container.BeginLifetimeScope(b =>
    b.RegisterInstance(connexion).As<IDbConnection>().ExternallyOwned());       // le client en reste propriétaire

var serviceSentier = scope.Resolve<IServiceSentier>();
await serviceSentier.DemarrerAsync("demande-conge", cleAffaire: 42, ValeursVariables.Avec(DemandeConge.NbJours, 3L));

portee.Complete();
```

La connexion doit être ouverte **à l'intérieur** de la portée, et `TransactionScopeAsyncFlowOption.Enabled` est nécessaire avec `async`. **Oracle seulement** : Microsoft.Data.Sqlite ne s'enrôle pas dans un `TransactionScope` ; avec SQLite, Sentier refuse d'écrire plutôt que de valider chaque instruction immédiatement.

**Transaction explicite (`IDbTransaction`).** Si l'application ouvre elle-même une transaction (c'est le cas des tests SQLite), elle l'enregistre dans la portée avec la connexion :

```csharp
using var connexion = _fabriqueConnexions.Ouvrir();
using var transaction = connexion.BeginTransaction();

using var scope = _container.BeginLifetimeScope(b =>
{
    b.RegisterInstance(connexion).As<IDbConnection>().ExternallyOwned();
    b.RegisterInstance(transaction).As<IDbTransaction>().ExternallyOwned();
});

await scope.Resolve<IServiceSentier>().DemarrerAsync("demande-conge", cleAffaire: 42, ValeursVariables.Avec(DemandeConge.NbJours, 3L));

transaction.Commit();
```

Avec `Microsoft.Extensions.DependencyInjection`, la transaction explicite s'alimente dans `IAccesseurTransactionSentier` (inutile avec un `TransactionScope`) :

```csharp
using var scope = provider.CreateScope();
var connexion = scope.ServiceProvider.GetRequiredService<IDbConnection>();
using var transaction = connexion.BeginTransaction();
scope.ServiceProvider.GetRequiredService<IAccesseurTransactionSentier>().Transaction = transaction;

var resultat = await scope.ServiceProvider.GetRequiredService<IServiceSentier>()
    .DemarrerAsync("demande-conge", cleAffaire: 42, ValeursVariables.Avec(DemandeConge.NbJours, 3L));

transaction.Commit();
```

Dans une application web, ce code va dans un middleware ou une fabrique qui crée la portée pour chaque requête. Avec SQLite, le schéma se crée de la même façon : `await scope.Resolve<InstallateurSchemaSentier>().InstallerAsync();`.

Pour brancher Sentier sur une unité de travail existante, fournir sa propre implémentation : `o.UtiliserSession<MaSession>()`.

**Couche d'accès aux données du client.** Si l'application passe par un wrapper maison autour de Dapper, Sentier peut l'utiliser aussi : implémenter `IExecuteurSqlSentier` (quatre méthodes : `ExecuterAsync`, `ExecuterScalaireAsync<T>`, `LireAsync<T>`, `LirePremierOuDefautAsync<T>`) en déléguant au wrapper, puis `o.UtiliserExecuteurSql<MonAdaptateur>()`. Sentier n'a alors besoin ni d'`IDbConnection` ni d'`IDbTransaction` dans la portée : le wrapper passe `transaction: null` et le `TransactionScope` (Oracle) porte la transaction. Le SQL arrive dans la syntaxe du dialecte et les paramètres sont des objets Dapper, à transmettre tels quels. Exemple complet : [docs/integration-client.md](docs/integration-client.md), § 3.2.

Les actions qui écrivent lèvent `TransactionRequiseException` sans transaction active (ni `IDbTransaction`, ni `TransactionScope`). Une exception (commande, requête, conflit…) est **propagée** : c'est le client qui annule sa transaction.

## 3. Écrire un processus

Une classe par processus, dérivée de `DefinitionSentier` : la clé vient de l'attribut, les champs `Variable<T>` statiques sont déclarés automatiquement, le début est implicite, et le déroulement se lit de haut en bas. Enchaîner deux nœuds crée la transition ; une branche se déclare puis sa destination s'écrit juste après : `.Alors(b => …)` (imbriqué), `.Vers("code")` (à plat), rien (nœud suivant).

```csharp
[ProcessusSentier("demande-conge", Nom = "Demande de congé")]
public sealed class DemandeConge : DefinitionSentier
{
    public static readonly Variable<long> NbJours = Variable.Entier("nbJours").Obligatoire();
    public static readonly Variable<long> Solde = Variable.Entier("solde").ParDefaut(0);
    public static readonly Variable<string> Decision = Variable.Texte("decision");

    protected override void Definir(ConstructeurProcessus processus) => processus
        .Systemique<CalculerSolde>("calculer-solde", p => p.CleAffaire("employe"))
        .Decision("solde-suffisant", d => d
            .Si<SoldeSuffisant>(p => p.Variables(NbJours, Solde))      // sortie « SoldeSuffisant » : nœud suivant
            .Sinon(b => b.Fin("refusee")))                               // style imbriqué
        .Interactif("approbation", "Approuver la demande", i => i
            .Role("gestionnaire")
            .Donnees(NbJours)
            .CommandeSortie<Journaliser>())
        .Decision("est-approuvee", d => d
            .Si<EstApprouvee>()
            .Sinon("refusee-gestionnaire"))                              // style plat
        .Attente("attendre-rh", a => a
            .Signal("VALIDATION_RH")
            .Echeance(TimeSpan.FromDays(2)).Alors(b => b
                .Systemique<Relancer>("relancer").Vers("attendre-rh")))  // boucle
        .SousProcessus<Archivage>("archivage")
        .Fin("approuvee")
        .Fin("refusee-gestionnaire");
}
```

Le guide complet du constructeur (nœuds, branches, paramètres, tableau des défauts, aide-mémoire) est dans [docs/constructeur.md](docs/constructeur.md).

`Construire()` valide la définition (codes uniques, un début, au moins une fin, liens existants, nœuds atteignables, décision avec branche conditionnelle et « sinon », attente avec au moins un déclencheur et au plus une échéance, variables référencées déclarées et typées) et lève `DefinitionInvalideException` avec la liste des erreurs.

`ISuiviSentier.ExporterDiagrammeAsync` (ou `ExportateurMermaid.Exporter`) produit un diagramme **Mermaid** de la définition.

## 4. Commandes et requêtes

Toute mise à jour métier passe par une **commande** ; chaque branche conditionnelle d'une décision est évaluée par une **requête** (lecture seule), ou directement par une condition sur variable (`.Si(Montant).SuperieurA(1000m)`, voir [docs/constructeur.md](docs/constructeur.md)). Commandes et requêtes sont découvertes par balayage d'assemblys, enregistrées en Scoped, désignées dans les définitions par leur classe et stockées par nom (celui de la classe ; `[CommandeSentier("AncienNom")]` / `[RequeteSentier("AncienNom")]` garde l'ancien nom après un renommage, ce que le déploiement vérifie) et peuvent injecter leurs propres services : elles travaillent dans la même transaction.

```csharp
public sealed class CalculerSolde(IDepotEmployes employes) : ICommandeSentier
{
    public async Task ExecuterAsync(ContexteCommandeSentier contexte, CancellationToken ct)
    {
        var solde = await employes.SoldeAsync(contexte.Parametres.Lire<long>("employe"), ct);
        contexte.Variables.Ecrire(DemandeConge.Solde, solde);   // variable déclarée et du bon type, sinon erreur
    }
}

public sealed class ParametresSolde { public long Jours { get; set; } public long Solde { get; set; } }

public sealed class SoldeSuffisant : RequeteSentier<ParametresSolde>   // paramètres liés par nom de propriété
{
    protected override Task<bool> EvaluerAsync(ContexteRequeteSentier contexte, ParametresSolde p, CancellationToken ct)
        => Task.FromResult(p.Jours <= p.Solde);
}
```

Paramètres d'un appel : `Valeur(nom, valeur)` (statique), `Variable([nom, ]variable)`, `Variables(v1, v2, …)`, `VariableOptionnelle([nom, ]variable[, défaut])`, `CleAffaire(nom)`. `SiPas<TRequete>` inverse le résultat d'une requête. Points d'accroche : nœud systémique (1 commande), entrée d'attente, entrée et sortie d'interactif. Le contexte d'une requête est en lecture seule.

## 5. Clé d'affaire

- La clé d'affaire est un `long` qui n'a de sens qu'avec la clé du processus. Elle est fixée au démarrage et ne change jamais.
- Par défaut, **une seule instance active** (`EnCours`, `Suspendue`, `EnErreur`) par processus et par clé : un second démarrage retourne `SansEffet` avec l'instance existante. La règle est garantie par un **index unique partiel** en base ; en cas de démarrages simultanés, le second échoue sur l'index et sa transaction doit être annulée. `InstancesMultiples()` lève la règle.
- Signaux : processus + clé d'affaire, clé de corrélation, ou les deux. Recherches : clé d'affaire + une ou plusieurs clés de processus.
- Sentier ne vérifie pas que la clé existe chez le client (à faire, au besoin, dans une commande en début de processus).

## 6. Gestion des tâches

Comme les définitions, les commandes et les requêtes, les tâches sont l'affaire du client : il implémente `IGestionnaireTachesSentier`, injecté en Scoped. Sentier **ne stocke aucune tâche** (pas de table `_TACHE`) ; il délègue au gestionnaire chaque étape du cycle de vie (`TacheSentier` : identifiant `long`, instance, nœud, statut).

Sentier garde un seul lien : l'identifiant de la tâche attendue, `ID_TACHE_COURANTE` sur l'instance (`EtatInstance.IdTacheCourante`), écrit dans la transaction de Sentier. **Seule cette tâche reprend l'instance**, quel que soit le statut tenu par le client :

- une tâche orpheline (créée chez le client, transaction de Sentier annulée) ou périmée est `SansEffet` ; les tâches orphelines encore ouvertes sont annulées quand l'instance quitte le nœud ;
- une tâche marquée complétée chez le client alors que la transaction de Sentier a échoué peut être complétée de nouveau : l'instance repart ;
- une tâche annulée chez le client alors que l'instance l'attend est `SansEffet` (l'instance reste en attente ; l'annuler par `AnnulerAsync`).

| Méthode | Appelée par Sentier |
|---|---|
| `CreerTacheAsync(demande)` → identifiant | à l'entrée d'un nœud interactif, après les commandes d'entrée |
| `ObtenirTacheAsync(id)` | au début de `CompleterTacheAsync` (tâche inconnue ou non ouverte → `SansEffet`) |
| `ObtenirTachesOuvertesAsync(idInstance)` | annulation, reprise d'une instance en erreur, migration, suivi |
| `CompleterTacheAsync(id, données, utilisateur)` | après la fusion des données et les commandes de sortie |
| `AssignerTacheAsync(id, utilisateur)` | `IServiceSentier.AssignerTacheAsync`, seulement pour la tâche que l'instance attend (sinon `SansEffet`) |
| `AnnulerTacheAsync(id, motif)` | annulation de l'instance (et de ses enfants), migration avec reprise à un autre nœud |
| `RenommerNoeudAsync(idInstance, ancien, nouveau)` | migration qui renomme le nœud interactif courant |

```csharp
public sealed class MesTaches(IDepotTachesClient depot) : IGestionnaireTachesSentier
{
    public Task<long> CreerTacheAsync(DemandeTacheSentier demande, CancellationToken ct)
        => depot.InsererAsync(demande.IdInstance, demande.CodeNoeud, demande.NomTache, demande.Role, demande.Donnees, ct);

    public Task CompleterTacheAsync(long idTache, IReadOnlyDictionary<string, object?>? donnees, string? utilisateur, CancellationToken ct)
        => depot.ChangerStatutAsync(idTache, StatutTacheSentier.Completee, utilisateur, ct);

    // ObtenirTacheAsync, ObtenirTachesOuvertesAsync, AssignerTacheAsync, AnnulerTacheAsync, RenommerNoeudAsync…
}
```

L'implémentation est choisie dans cet ordre : `o.UtiliserGestionnaireTaches<MesTaches>()` ; sinon un `IGestionnaireTachesSentier` déjà enregistré par le client avant `AddSentier` ; sinon l'unique implémentation trouvée par balayage (les décorateurs, dont un constructeur reçoit un `IGestionnaireTachesSentier`, sont ignorés). Le déploiement échoue si une définition contient un nœud interactif sans gestionnaire enregistré.

Exigences sur l'implémentation :

- **Même transaction** que Sentier si elle est en base (même session). Sinon, les cas ci-dessus restent cohérents, mais une tâche orpheline reste visible chez le client jusqu'à la sortie du nœud.
- **Idempotence** de `CompleterTacheAsync` et `AnnulerTacheAsync` : elles peuvent être rappelées pour une tâche déjà complétée ou annulée.
- L'assignation automatique (`.Assigne(...)`) est dans la demande de création (`Role`, `Assigne`) : `CreerTacheAsync` crée la tâche déjà assignée. Sentier ne conserve pas l'assigné : réassigner ou libérer une tâche peut se faire directement dans le bassin de tâches, tant que la tâche garde son identifiant et reste ouverte. `IServiceSentier.AssignerTacheAsync(idTache, utilisateur)` fait la même chose en vérifiant d'abord que l'instance attend bien cette tâche.
- `ObtenirTachesOuvertesAsync` est aussi appelée par `ISuiviSentier`, éventuellement sans transaction. Le client complète une tâche par `IServiceSentier.CompleterTacheAsync(idTache, données, utilisateur)` : les données sont fusionnées pour les seules variables déclarées, les commandes de sortie s'exécutent, puis Sentier demande au gestionnaire de marquer la tâche complétée.

### Mise à jour d'une base existante

Les bases créées avant ce changement ont une table `_TACHE` que Sentier ne lit plus. Le script `mise-a-jour-gestionnaire-taches.sql` (`src/Sentier.Persistance/Scripts/Oracle` et `.../Sqlite`, préfixe `{{P}}` à remplacer) ajoute `ID_TACHE_COURANTE` à partir des tâches ouvertes, recrée la vue, liste les tâches ouvertes à reprendre dans le stockage du client (`ID_TACHE_EXTERNE` devient l'identifiant) et, en commentaire, supprime `_TACHE` une fois la reprise vérifiée. À exécuter application arrêtée, avant le déploiement.

## 7. Déploiement et migration

```csharp
// Au démarrage de l'application, dans une transaction du client :
var rapport = await serviceSentier.DeployerDefinitionsAsync();
```

Pour chaque `IDefinitionSentier`, Sentier valide tout (structure, commandes et requêtes enregistrées, sous-processus et mappages) puis compare l'empreinte SHA-256 du JSON canonique à la dernière version : identique → rien n'est créé ; différente → nouvelle version active. Une erreur empêche tout le déploiement. Les instances gardent leur version d'origine. Le rapport avertit quand un nœud disparaît alors que des instances s'y trouvent.

Avec `MigrationAutomatique()`, les instances au repos des anciennes versions sont migrées au déploiement si le changement est **compatible** pour elles (nouveaux nœuds ou branches, nouvelle variable optionnelle ou avec défaut, nouveau déclencheur — l'attente manquante est créée). Sinon, un **plan** est nécessaire :

```csharp
public sealed class PlanCongeV3 : IPlanMigration
{
    public string CleProcessus => "demande-conge";
    public int VersionSourceMin => 1;
    public int VersionSourceMax => 2;

    public void Definir(ConstructeurPlanMigration plan) => plan
        .Noeud("attendre-rh", "attente-rh", r => r.ConserverAttentes())
        .PourNoeud("ancienne-tache", r => r.PuisReprendreA("nouvelle-tache"))   // annule tâches/attentes, puis exécute le nœud
        .RenommerVariable("ancien", "nouveau")
        .ValeurVariable(DemandeConge.Solde, 0L)
        .CalculerVariables<CalculerVariablesMigration>();
}
```

Déroulement : `CompterInstancesActivesAsync` → `SimulerMigrationAsync` (rapport par instance, rien n'est écrit) → `MigrerAsync`, par lots, une transaction par lot. Aucune commande d'entrée n'est réexécutée ; chaque migration est tracée dans `_MIGRA_PROCS`.

## 8. Batch externe des échéances

Sentier ne lance aucun thread. Le batch du client applique ce cycle ; une erreur ne bloque qu'une instance, et plusieurs batchs peuvent tourner en parallèle (`FOR UPDATE SKIP LOCKED` sous Oracle + verrou optimiste de l'instance).

```csharp
public async Task TraiterEcheancesAsync(IServiceProvider provider, CancellationToken ct)
{
    IReadOnlyList<EcheanceDue> dues;
    using (var scope = provider.CreateScope())
    {
        dues = await scope.ServiceProvider.GetRequiredService<IServiceSentier>().ObtenirEcheancesDuesAsync(tailleMax: 200, ct);
    }

    foreach (var echeance in dues)
    {
        try
        {
            await EnTransactionAsync(provider, s => s.TraiterEcheanceAsync(echeance.IdAttente, ct));
        }
        catch (Exception ex)
        {
            // La transaction qui a échoué est annulée ; l'erreur est marquée dans une transaction séparée.
            await EnTransactionAsync(provider, s => s.MarquerEnErreurAsync(echeance.IdInstance, ex.Message, ct));
        }
    }
}

private static async Task EnTransactionAsync(IServiceProvider provider, Func<IServiceSentier, Task> action)
{
    using var scope = provider.CreateScope();
    var connexion = scope.ServiceProvider.GetRequiredService<IDbConnection>();
    using var transaction = connexion.BeginTransaction();
    scope.ServiceProvider.GetRequiredService<IAccesseurTransactionSentier>().Transaction = transaction;
    try
    {
        await action(scope.ServiceProvider.GetRequiredService<IServiceSentier>());
        transaction.Commit();
    }
    catch
    {
        transaction.Rollback();
        throw;
    }
}
```

`ReprendreAsync` relance une instance `EnErreur` : si elle attend encore (attente, tâche, enfant), elle redevient `Suspendue` et une échéance déjà atteinte est traitée tout de suite ; sinon son nœud courant est réexécuté. Toutes les dates sont en UTC (`IHorlogeSentier`).

## 9. Variables : recherche et correction

**Rechercher** les instances d'un processus par la valeur de leurs variables (toutes versions, des plus récentes aux plus anciennes) :

```csharp
var suivi = scope.Resolve<ISuiviSentier>();

// Égalité simple
var instances = await suivi.RechercherParVariableAsync("demande-conge", DemandeConge.Decision, "refusee");

// Plusieurs filtres (ET), statuts et taille maximale
var longues = await suivi.RechercherParVariablesAsync(new RechercheParVariables
{
    CleProcessus = "demande-conge",
    Filtres = [FiltreVariable.SuperieurOuEgal(DemandeConge.NbJours, 10L), FiltreVariable.EstVide(DemandeConge.Decision)],
    Statuts = [StatutInstance.Suspendue],
    TailleMax = 50,
});
```

Les opérateurs sont ceux des décisions (`Egal`, `Different`, `Superieur`…, `Contient`, `CommencePar`, `EstRenseignee`, `EstVide`) ; la valeur est convertie dans le type déclaré par la version de chaque instance, et un filtre incompatible lève `ArgumentException`. Les variables étant stockées en JSON (`VAR_JSON`), Sentier préfiltre en base par `LIKE` (avec le sérialiseur par défaut), puis applique le filtre exact en lisant les instances par lots : la recherche reste bornée par `TailleMax`, mais un filtre peu sélectif sur un gros processus parcourt beaucoup de lignes. Filtrer par statut aide.

**Corriger** des variables d'une instance suspendue ou en erreur, sans la faire avancer :

```csharp
await serviceSentier.ModifierVariablesAsync(idInstance, ValeursVariables.Avec(DemandeConge.NbJours, 5L), utilisateur: "jdoe");
transaction.Commit();
```

Chaque variable doit être déclarée et du bon type ; `null` efface une variable non obligatoire. L'instance est mise à jour sous verrou optimiste et la modification est journalisée (`ILogger`). Les valeurs déjà calculées à l'entrée du nœud courant (échéance, clé de corrélation, données de la tâche) ne sont pas recalculées : la nouvelle valeur sert aux nœuds suivants, ou au nœud courant s'il est relancé (`ReprendreAsync` d'une instance en erreur). Instance terminée, annulée ou introuvable : `SansEffet`.

## 10. Idempotence

Ces cas retournent un résultat `SansEffet` avec sa raison, sans lever d'erreur : tâche déjà complétée ou annulée (ou identifiant inconnu), échéance déjà traitée ou gagnée par un signal, signal sans instance en attente, démarrage d'un processus déjà actif pour la clé, annulation d'une instance terminée ou annulée, modification des variables d'une instance terminée ou annulée.

## 11. Choix d'implémentation

Points non tranchés par la spécification, résolus au plus simple :

- **Cible .NET 10** (`net10.0`) à la place de .NET 8.
- **Lectures sans transaction** : `ISuiviSentier` et `ObtenirEcheancesDuesAsync` acceptent l'absence de transaction (elles utilisent la transaction courante si elle existe). Les écritures l'exigent.
- **Identifiant de tâche** : `long`, attribué par le gestionnaire de tâches du client ; une instance attend au plus une tâche (`ID_TACHE_COURANTE`).
- **Code de fin** : le code du nœud de fin atteint.
- **Lien `Vers` sur un nœud à plusieurs sorties** : il désigne le point de convergence de ses branches.
- **Signal par processus + clé d'affaire sans corrélation** : il atteint les attentes de ce signal quelle que soit leur clé de corrélation ; avec une corrélation, elle doit être égale.
- **Instances en erreur** : elles restent au repos sur leur nœud. Elles peuvent encore recevoir un signal ou une complétion de tâche, mais le batch des échéances les ignore jusqu'à `ReprendreAsync`.
- **Variables au démarrage** : une clé non déclarée est une erreur (`VariableInvalideException`). Les données de tâche et les charges utiles de signal, elles, ignorent les clés non déclarées.
- **Sous-processus à instance unique déjà actif** pour la clé visée : `SentierException`.
- **Migration** : seules les instances `Suspendue` ou `EnErreur` sont migrées ; une variable absente de la version cible est abandonnée (tracé dans le détail).
- **Oracle** : limitation par `ROWNUM` (compatible avec les versions antérieures à 12c), noms d'objets ≤ 30 caractères, booléens en `NUMBER(1)`.
- **SQLite** : dates stockées en texte, séquences émulées par `{Prefixe}_SEQUENCE`, clés étrangères actives seulement avec `PRAGMA foreign_keys = ON`.
- **Cache** : les versions de définitions sont gardées en mémoire par identifiant, sans invalidation (singleton par conteneur DI).

Hors périmètre de cette itération : passerelles parallèles, événements de bordure, compensation, escalade des tâches, éditeur graphique et import BPMN.

## 12. Tests

```bash
dotnet test                                   # toute la solution
dotnet test tests/Sentier.Integration.Tests   # un seul projet
```

| Projet | Nature | Ce qui est couvert |
|---|---|---|
| `Sentier.Core.Tests` | unitaire, sans base | variables (conversion, comparaison, sérialisation JSON), constructeur fluide et validation des définitions, export Mermaid, options |
| `Sentier.DependencyInjection.Tests` | unitaire, sans base | `AddSentier` : services enregistrés, balayage, choix du gestionnaire de tâches, remplacement de l'exécuteur SQL, de la session et de l'horloge |
| `Sentier.Persistance.Tests` | unitaire + garde SQLite | dialectes Oracle/SQLite, noms d'objets, découpage des scripts, compatibilité avec un traducteur Oracle → SQLite |
| `Sentier.Integration.Tests` | intégré, de bout en bout | vrai conteneur, vrais dépôts Dapper, base SQLite en mémoire et transactions du client : tâches, décisions, signaux et corrélation, échéances et batch, erreurs et reprise, sous-processus, annulations, versions de définitions, suivi |

Les tests intégrés partent chacun d'une base vide (`Infrastructure/EnvironnementSentier`). Le gestionnaire de tâches de
test écrit dans une table de la même base et dans la transaction de Sentier, comme une application réelle ; l'horloge est
pilotée par le test (`HorlogeTest.Avancer`) pour déclencher les échéances.
