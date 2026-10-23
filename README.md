# Sentier

Moteur de processus (BPM) léger, distribué en packages NuGet pour .NET 10.

- **Exécution synchrone en chaîne** : les nœuds s'enchaînent jusqu'à une fin ou un nœud suspensif (attente, tâche, sous-processus).
- **Une seule transaction, celle du client** : Sentier n'ouvre, ne valide et n'annule jamais de transaction.
- **Une instance = un processus + une clé d'affaire** (`long`).
- **Persistance Dapper**, Oracle (production) et SQLite (tests), avec un préfixe configurable pour les objets.

| Package | Contenu |
|---|---|
| `Sentier.Core` | Domaine, constructeur de définitions, moteur, abstractions. Aucune dépendance à une base. |
| `Sentier.Persistance.Dapper` | Dépôts Dapper, noms préfixés, abstraction `IDialecteSql`, installateur de schéma. |
| `Sentier.Persistance.Oracle` | Dialecte Oracle et script DDL (tables + séquences). |
| `Sentier.Persistance.Sqlite` | Dialecte SQLite et script DDL. |
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

`AddSentier` n'enregistre **jamais** d'`IDbConnection`. Services optionnels à enregistrer par le client : `IContexteUtilisateur` (utilisateur courant, pour l'audit), `IIntercepteurCommande`, `IIntercepteurRequete`, et un `IHorloge` ou `ISerialiseurVariables` personnalisé.

Le schéma peut être créé par `InstallateurSchemaSentier.InstallerAsync()` (pratique avec SQLite) ou remis à un DBA via `GenererScript()`. Les scripts sont aussi dans `src/Sentier.Persistance.*/Scripts`.

## 2. Connexion et transaction

Sentier obtient la connexion et la transaction par `ISessionSentier` (Scoped). L'implémentation par défaut résout l'`IDbConnection` de la DI et lit la transaction dans `IAccesseurTransaction`, que le client alimente :

```csharp
using var scope = provider.CreateScope();
var connexion = scope.ServiceProvider.GetRequiredService<IDbConnection>();
using var transaction = connexion.BeginTransaction();
scope.ServiceProvider.GetRequiredService<IAccesseurTransaction>().Transaction = transaction;

var resultat = await scope.ServiceProvider.GetRequiredService<IServiceSentier>()
    .DemarrerAsync("demande-conge", cleAffaire: 42, ValeursVariables.Avec(DemandeConge.NbJours, 3L));

transaction.Commit();
```

Pour brancher Sentier sur une unité de travail existante, fournir sa propre implémentation : `o.UtiliserSession<MaSession>()`.

Les actions qui écrivent lèvent `TransactionRequiseException` sans transaction active. Une exception (commande, requête, conflit…) est **propagée** : c'est le client qui annule sa transaction.

## 3. Écrire un processus

Une classe par processus. Les variables sont déclarées une fois, en constantes typées, puis le déroulement se lit de haut en bas. Enchaîner deux nœuds crée la transition ; le nœud de début est implicite.

Chaque méthode a une forme courte pour le cas courant ; on n'écrit une option que pour s'écarter du défaut. Les options passent par un bloc (`p => …`, `i => …`), jamais par une liste de paramètres. Une branche se déclare, puis sa destination s'écrit juste après : `.Alors(b => …)` pour un bloc **imbriqué**, `.Vers("code")` pour un lien **à plat**, rien pour aller au nœud suivant. `Sinon`, `SiFin` et `SiAnnule`, qui n'ont pas d'options, acceptent aussi la destination en argument : `.Sinon("refusee")`, `.Sinon(b => b.Fin("refusee"))`.

```csharp
public sealed class DemandeConge : IDefinitionSentier
{
    public static readonly Variable<long> NbJours = Variable.Entier("nbJours").Obligatoire();
    public static readonly Variable<long> Solde = Variable.Entier("solde").ParDefaut(0);
    public static readonly Variable<string> Decision = Variable.Texte("decision");

    public DefinitionProcessus Definir() => ConstructeurProcessus.Nouveau("demande-conge", "Demande de congé")
        .Variables(NbJours, Solde, Decision)
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
        .SousProcessus("archivage", "archivage")
        .Fin("approuvee")
        .Fin("refusee-gestionnaire")
        .Construire();
}
```

Règles de sortie d'un nœud, dans l'ordre : lien explicite ; sinon le nœud suivant du même bloc ; sinon, en fin de bloc imbriqué, le nœud qui suit le nœud à plusieurs sorties. Un nœud placé après une fin ou un lien n'est relié que par un lien ou une branche. Raccourcis : `AttendreDelai(code, durée)` et `AttendreSignal(code, nom)`.

| Défaut | Pour s'en écarter |
|---|---|
| Début implicite, code `debut` | `.Debut("autre-code")` en premier |
| Nom de la tâche = code du nœud | `.Interactif(code, "Nom de la tâche")` |
| Sortie d'une branche = nom de la requête (`non-…` pour `SiPas`), `sinon`, `echeance` ou nom du signal | `.Nommee("oui")` après la branche |
| Charge utile du signal fusionnée, sans corrélation | `.Signal(nom, s => s.Correlation(Variable).SansFusion())` |
| Sous-processus sur la clé d'affaire du parent, fins vers le nœud suivant | `s => s.CleAffaire(Variable).SiFin("refuse", "…")` |
| Paramètre nommé comme sa variable | `p.Variable("autre-nom", Variable)` |

Les codes de nœuds, eux, restent toujours explicites : ils sont le contrat avec les instances en cours.

`Construire()` valide la définition (codes uniques, un début, au moins une fin, liens existants, nœuds atteignables, décision avec branche conditionnelle et « sinon », attente avec au moins un déclencheur et au plus une échéance, variables référencées déclarées et typées) et lève `DefinitionInvalideException` avec la liste des erreurs.

`ISuiviSentier.ExporterDiagrammeAsync` (ou `ExportateurMermaid.Exporter`) produit un diagramme **Mermaid** de la définition.

## 4. Commandes et requêtes

Toute mise à jour métier passe par une **commande** ; chaque branche conditionnelle d'une décision est évaluée par une **requête** (lecture seule). Les deux sont découvertes par balayage d'assemblys, enregistrées en Scoped, référencées par nom (celui de la classe, ou de l'attribut `[CommandeSentier("nom")]` / `[RequeteSentier("nom")]`) et peuvent injecter leurs propres services : elles travaillent dans la même transaction.

```csharp
public sealed class CalculerSolde(IDepotEmployes employes) : ICommandeSentier
{
    public async Task ExecuterAsync(ContexteCommande contexte, CancellationToken ct)
    {
        var solde = await employes.SoldeAsync(contexte.Parametres.Lire<long>("employe"), ct);
        contexte.Variables.Ecrire(DemandeConge.Solde, solde);   // variable déclarée et du bon type, sinon erreur
    }
}

public sealed class ParametresSolde { public long Jours { get; set; } public long Solde { get; set; } }

public sealed class SoldeSuffisant : RequeteSentier<ParametresSolde>   // paramètres liés par nom de propriété
{
    protected override Task<bool> EvaluerAsync(ContexteRequete contexte, ParametresSolde p, CancellationToken ct)
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

Comme les définitions, les commandes et les requêtes, les tâches sont l'affaire du client : il implémente `IGestionnaireTaches`, injecté en Scoped. Sentier **ne stocke aucune tâche** (pas de table `_TACHE`) ; il délègue au gestionnaire chaque étape du cycle de vie (`TacheUtilisateur` : identifiant `long`, instance, nœud, statut).

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
| `AnnulerTacheAsync(id, motif)` | annulation de l'instance (et de ses enfants), migration avec reprise à un autre nœud |
| `RenommerNoeudAsync(idInstance, ancien, nouveau)` | migration qui renomme le nœud interactif courant |

```csharp
public sealed class MesTaches(IDepotTachesClient depot) : IGestionnaireTaches
{
    public Task<long> CreerTacheAsync(DemandeTacheUtilisateur demande, CancellationToken ct)
        => depot.InsererAsync(demande.IdInstance, demande.CodeNoeud, demande.Nom, demande.Role, demande.Donnees, ct);

    public Task CompleterTacheAsync(long idTache, IReadOnlyDictionary<string, object?>? donnees, string? utilisateur, CancellationToken ct)
        => depot.ChangerStatutAsync(idTache, StatutTache.Completee, utilisateur, ct);

    // ObtenirTacheAsync, ObtenirTachesOuvertesAsync, AnnulerTacheAsync, RenommerNoeudAsync…
}
```

L'implémentation est choisie dans cet ordre : `o.UtiliserGestionnaireTaches<MesTaches>()` ; sinon un `IGestionnaireTaches` déjà enregistré par le client avant `AddSentier` ; sinon l'unique implémentation trouvée par balayage (les décorateurs, dont un constructeur reçoit un `IGestionnaireTaches`, sont ignorés). Le déploiement échoue si une définition contient un nœud interactif sans gestionnaire enregistré.

Exigences sur l'implémentation :

- **Même transaction** que Sentier si elle est en base (même session). Sinon, les cas ci-dessus restent cohérents, mais une tâche orpheline reste visible chez le client jusqu'à la sortie du nœud.
- **Idempotence** de `CompleterTacheAsync` et `AnnulerTacheAsync` : elles peuvent être rappelées pour une tâche déjà complétée ou annulée.
- `ObtenirTachesOuvertesAsync` est aussi appelée par `ISuiviSentier`, éventuellement sans transaction. Le client complète une tâche par `IServiceSentier.CompleterTacheAsync(idTache, données, utilisateur)` : les données sont fusionnées pour les seules variables déclarées, les commandes de sortie s'exécutent, puis Sentier demande au gestionnaire de marquer la tâche complétée.

### Mise à jour d'une base existante

Les bases créées avant ce changement ont une table `_TACHE` que Sentier ne lit plus. Le script `Scripts/mise-a-jour-gestionnaire-taches.sql` (Oracle et SQLite, préfixe `{{P}}` à remplacer) ajoute `ID_TACHE_COURANTE` à partir des tâches ouvertes, recrée la vue, liste les tâches ouvertes à reprendre dans le stockage du client (`ID_TACHE_EXTERNE` devient l'identifiant) et, en commentaire, supprime `_TACHE` une fois la reprise vérifiée. À exécuter application arrêtée, avant le déploiement.

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

Déroulement : `CompterInstancesActivesAsync` → `SimulerMigrationAsync` (rapport par instance, rien n'est écrit) → `MigrerAsync`, par lots, une transaction par lot. Aucune commande d'entrée n'est réexécutée ; chaque migration est tracée dans `_MIGRATION`.

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
    scope.ServiceProvider.GetRequiredService<IAccesseurTransaction>().Transaction = transaction;
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

`ReprendreAsync` relance une instance `EnErreur` : si elle attend encore (attente, tâche, enfant), elle redevient `Suspendue` et une échéance déjà atteinte est traitée tout de suite ; sinon son nœud courant est réexécuté. Toutes les dates sont en UTC (`IHorloge`).

## 9. Idempotence

Ces cas retournent un résultat `SansEffet` avec sa raison, sans lever d'erreur : tâche déjà complétée ou annulée (ou identifiant inconnu), échéance déjà traitée ou gagnée par un signal, signal sans instance en attente, démarrage d'un processus déjà actif pour la clé, annulation d'une instance terminée ou annulée.

## 10. Choix d'implémentation

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

Hors périmètre de cette itération : tests, passerelles parallèles, événements de bordure, compensation, escalade des tâches, éditeur graphique et import BPMN.
