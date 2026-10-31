# Écrire un processus avec le constructeur fluide

Ce guide décrit comment écrire une définition de processus Sentier. Le principe : **on écrit le cas courant en une ligne, et une option seulement pour s'écarter du défaut.**

- [1. La classe du processus](#1-la-classe-du-processus)
- [2. Variables](#2-variables)
- [3. Enchaîner les nœuds](#3-enchaîner-les-nœuds)
- [4. Les nœuds](#4-les-nœuds)
- [5. Branches : imbriquées ou à plat](#5-branches--imbriquées-ou-à-plat)
- [6. Paramètres des commandes et requêtes](#6-paramètres-des-commandes-et-requêtes)
- [7. Défauts et options](#7-défauts-et-options)
- [8. Validation](#8-validation)
- [9. Bonnes pratiques](#9-bonnes-pratiques)
- [Aide-mémoire](#aide-mémoire)

## 1. La classe du processus

Une classe par processus, qui dérive de `DefinitionSentier`. On y écrit les variables, puis le déroulement de haut en bas.

```csharp
[ProcessusSentier("demande-conge", Nom = "Demande de congé")]
public sealed class DemandeConge : DefinitionSentier
{
    public static readonly Variable<long> NbJours = Variable.Entier("nbJours").Obligatoire();
    public static readonly Variable<long> Solde = Variable.Entier("solde").ParDefaut(0);

    protected override void Definir(ConstructeurProcessus processus) => processus
        .Systemique<CalculerSolde>("calculer-solde", p => p.CleAffaire("employe"))
        .Decision("solde-suffisant", d => d
            .Si<SoldeSuffisant>(p => p.Variables(NbJours, Solde))
            .Sinon(b => b.Fin("refusee")))
        .Interactif("approbation", "Approuver la demande", i => i.Role("gestionnaire").Donnees(NbJours))
        .Fin("approuvee");
}
```

Sentier fait le reste :

| Quoi | D'où |
|---|---|
| Clé du processus | `[ProcessusSentier("cle")]` ; sans attribut, le nom de la classe |
| Nom lisible | `Nom = "…"` dans l'attribut ; sinon, la clé |
| Variables | les champs statiques `Variable<T>` de la classe, dans leur ordre d'écriture |
| Nœud de début | implicite (`debut`) |
| `Construire()` | appelé par Sentier, au déploiement |

La classe est découverte par le balayage d'assemblys (`o.BalayerAssemblyDe<…>()`). Elle peut injecter des services par son constructeur.

> **Fixer la clé par l'attribut.** La clé est un contrat avec les instances en base et avec les appels `DemarrerAsync("cle", …)` du client. Sans attribut, renommer la classe changerait la clé.

Options du processus, à écrire en tête du déroulement :

```csharp
protected override void Definir(ConstructeurProcessus processus) => processus
    .InstancesMultiples()        // plusieurs instances actives par clé d'affaire
    .MigrationAutomatique()      // migre les instances au déploiement si le changement est compatible
    .Systemique<…>(…)
```

`IDefinitionSentier` reste disponible pour construire une définition à la main (`ConstructeurProcessus.Nouveau(cle, nom)…Construire()`), par exemple à partir de données.

## 2. Variables

Les variables sont déclarées une fois, en constantes typées, puis réutilisées partout : paramètres, données de tâche, mappages, lecture et écriture dans les commandes.

```csharp
public static readonly Variable<long>     NbJours  = Variable.Entier("nbJours").Obligatoire();
public static readonly Variable<long>     Solde    = Variable.Entier("solde").ParDefaut(0);
public static readonly Variable<string>   Decision = Variable.Texte("decision");
public static readonly Variable<DateTime> Limite   = Variable.Date("limite");
public static readonly Variable<Adresse>  Adresse  = Variable.Json<Adresse>("adresse");
```

| Fabrique | Type CLR |
|---|---|
| `Texte` | `string` |
| `Entier` | `long` |
| `Decimal` | `decimal` |
| `Booleen` | `bool` |
| `Date` | `DateTime` (UTC) |
| `Json<T>` | objet libre, sans garantie de type |

- `.Obligatoire()` : la variable doit être fournie au démarrage, ou avoir une valeur par défaut.
- `.ParDefaut(valeur)` : valeur initiale si elle n'est pas fournie.
- Le **nom** (`"nbJours"`) est stocké en base : on ne le change pas une fois des instances démarrées.
- Les noms commençant par `_` sont réservés (`_declencheur`, `_finEnfant`).

## 3. Enchaîner les nœuds

Enchaîner deux nœuds crée la transition entre eux. On n'écrit jamais une transition simple.

```csharp
.Systemique<A>("a")
.Systemique<B>("b")       // a → b
.Fin("fin")               // b → fin
```

Chaque nœud a un **code** explicite, unique dans le processus. C'est le contrat avec les instances en cours (migration, historique) : il n'est jamais généré.

`.Vers("code")` remplace la sortie implicite du dernier nœud par un lien, vers un nœud déclaré avant (boucle) ou après :

```csharp
.Systemique<Relancer>("relancer").Vers("attendre-rh")
```

Après une `Fin` ou un `Vers`, le nœud suivant n'est **pas** relié automatiquement : il doit être atteint par un lien ou une branche.

`.Libelle("…")` donne un libellé lisible au dernier nœud (diagramme).

## 4. Les nœuds

### Systémique : exécute une commande

```csharp
.Systemique<CalculerSolde>("calculer-solde")
.Systemique<CalculerSolde>("calculer-solde", p => p.CleAffaire("employe"))
```

### Interactif : crée une tâche

```csharp
.Interactif("approbation")                                       // tâche nommée « approbation »
.Interactif("approbation", "Approuver la demande")
.Interactif("approbation", "Approuver la demande", i => i
    .Role("gestionnaire")                // ou .Assigne("jdoe") / .Assigne(VariableAssigne)
    .Echeance(TimeSpan.FromDays(3))      // indicative ; ou .Echeance(VariableDate)
    .Donnees(NbJours, Solde)             // ou .Donnees(p => p.Variable("jours", NbJours))
    .CommandeEntree<CalculerAssigne>()   // avant la création de la tâche
    .CommandeSortie<Journaliser>())      // après la fusion des données de la tâche
```

### Décision : choisit une branche

```csharp
.Decision("est-approuvee", d => d
    .Si<EstApprouvee>()
    .SiPas<EstComplet>().Vers("completer")
    .Sinon("refusee"))
```

Une branche peut aussi tester **une variable directement**, sans écrire de requête :

```csharp
.Decision("aiguiller", d => d
    .Si(Montant).SuperieurA(10_000m).Vers("approbation-direction")   // valeur fixe
    .Si(Urgent).EstVrai().Vers("traitement-urgent")
    .Si(Decision).EgalA("refusee").Alors(b => b.Fin("refusee"))
    .Si(NbJours).SuperieurA(Solde).Nommee("solde-insuffisant")       // autre variable
    .Si(Commentaire).EstRenseignee().Vers("relire")
    .Sinon())
```

| Opérateur | Types acceptés |
|---|---|
| `EgalA`, `DifferentDe` | tous sauf JSON |
| `SuperieurA`, `SuperieurOuEgalA`, `InferieurA`, `InferieurOuEgalA` | entier, décimal, date, texte (ordre ordinal) |
| `Contient`, `CommencePar` | texte (sensible à la casse) |
| `EstVrai`, `EstFaux` | booléen |
| `EstRenseignee`, `EstVide` | tous |

Un entier et un décimal se comparent entre eux. Une variable sans valeur ne satisfait que `EstVide` et `DifferentDe`. `EgalA(null)` équivaut à `EstVide()`. Sans le constructeur typé : `.SiVariable("montant", OperateurComparaison.Superieur, 10_000m)`.

La sortie d'une condition porte son expression (`montant > 10000`), visible dans l'historique et le diagramme ; `.Nommee(…)` lui donne un nom stable, à préférer si la valeur peut changer d'une version à l'autre.

Requêtes et conditions sont évaluées dans l'ordre de déclaration ; la première vraie gagne, sinon `Sinon`. Au moins une branche conditionnelle (`Si`/`SiPas`/condition sur variable) et exactement un `Sinon`.

### Attente : échéance et/ou signaux

```csharp
.AttendreDelai("pause", TimeSpan.FromHours(1))                    // raccourci
.AttendreSignal("attendre-paiement", "PAIEMENT_RECU")             // raccourci

.Attente("attendre-rh", a => a
    .CommandeEntree<CalculerLimite>()
    .Signal("VALIDATION_RH")
    .Signal("REFUS_RH").Vers("refusee")
    .Echeance(Limite).Alors(b => b.Systemique<Relancer>("relancer").Vers("attendre-rh")))
```

Échéance : `Echeance(TimeSpan)` (relative à l'entrée), `Echeance(DateTime)` (absolue, UTC) ou `Echeance(Variable<DateTime>)`. Au plus une par attente.

Options d'un signal :

```csharp
.Signal("REPONSE", s => s.Correlation(NumeroDossier).SansFusion())
```

Le premier déclencheur survenu gagne. Pour aiguiller selon le *contenu* d'un signal, placer une décision juste après l'attente.

### Sous-processus : démarre une instance enfant

```csharp
.SousProcessus<Archivage>("archivage")                            // clé tirée de la classe
.SousProcessus<Verification>("verifier", s => s
    .CleAffaire(IdLigne)                       // défaut : clé d'affaire du parent
    .Entree(Verification.Montant, Montant)     // parent → enfant, au démarrage
    .EntreeValeur(Verification.Niveau, 2L)
    .EntreeCleAffaire(Verification.IdDemande)
    .Sortie(Verification.Resultat, Resultat)   // enfant → parent, à la fin
    .SiFin("refusee", "refusee")               // une branche par code de fin
    .SiAnnule(b => b.Fin("annulee")))          // sans elle, le parent est annulé aussi
```

`SousProcessus("code", "cle-processus")` reste possible pour un enfant défini hors de `DefinitionSentier`.

### Fin

```csharp
.Fin("approuvee")       // le code est aussi le code de fin de l'instance
```

## 5. Branches : imbriquées ou à plat

Décision, attente et sous-processus ont plusieurs sorties. Une branche se **déclare**, puis sa **destination** s'écrit juste après :

| Écriture | Destination |
|---|---|
| rien | le nœud qui suit le nœud à plusieurs sorties |
| `.Alors(b => …)` | un bloc imbriqué ; à sa fin, il rejoint le nœud qui suit |
| `.Vers("code")` | un nœud déclaré ailleurs |

`Sinon`, `SiFin` et `SiAnnule`, qui n'ont pas d'options, acceptent aussi la destination en argument :

```csharp
.Sinon("refusee")                       // = .Sinon().Vers("refusee")
.Sinon(b => b.Fin("refusee"))           // = .Sinon().Alors(b => b.Fin("refusee"))
```

**Style imbriqué** : l'indentation montre la structure, les branches convergent seules.

```csharp
.Decision("montant-eleve", d => d
    .Si<MontantEleve>().Alors(b => b
        .Interactif("approbation-direction", "Approbation de la direction"))
    .Sinon())
.Systemique<Payer>("payer")             // les deux branches arrivent ici
```

**Style plat** : chaque branche pointe vers un code ; idéal pour les boucles et les jonctions éloignées.

```csharp
.Decision("est-complet", d => d
    .Si<EstComplet>().Vers("traiter")
    .Sinon("completer"))
.Interactif("completer", "Compléter le dossier").Vers("est-complet")    // boucle
.Systemique<Traiter>("traiter")
```

Les deux styles se mélangent librement. Une branche trop longue ou trop imbriquée : passer au style plat ou en faire un sous-processus.

## 6. Paramètres des commandes et requêtes

Chaque appel déclare ses paramètres dans un bloc `p => …` :

| Méthode | Valeur passée |
|---|---|
| `Variable(V)` | la variable `V`, sous son nom |
| `Variable("nom", V)` | la variable `V`, sous un autre nom |
| `Variables(V1, V2, …)` | plusieurs variables, chacune sous son nom |
| `VariableOptionnelle(V)` / `("nom", V)` / `("nom", V, defaut)` | absente (ou le défaut) si la variable n'a pas de valeur |
| `Valeur("nom", valeur)` | valeur fixe stockée dans la définition |
| `CleAffaire("nom")` | la clé d'affaire de l'instance |

Avec `CommandeSentier<TParametres>` / `RequeteSentier<TParametres>`, les paramètres sont liés aux propriétés par nom.

Les commandes et requêtes sont désignées par leur **classe** ; leur nom est celui de la classe, ou celui de `[CommandeSentier("nom")]` / `[RequeteSentier("nom")]`. Les variantes par nom (`Systemique("code", "NomCommande")`, `Si("NomRequete")`) existent pour les cas sans classe.

## 7. Défauts et options

| Défaut | Pour s'en écarter |
|---|---|
| Clé du processus = nom de la classe | `[ProcessusSentier("cle")]` |
| Nom lisible = clé | `[ProcessusSentier("cle", Nom = "…")]` |
| Variables = champs statiques de la classe | `.Variables(…)` pour en ajouter |
| Une instance active par clé d'affaire | `.InstancesMultiples()` |
| Pas de migration automatique | `.MigrationAutomatique()` |
| Début implicite, code `debut` | `.Debut("autre")` en premier |
| Sortie d'un nœud = nœud suivant | `.Vers("code")` |
| Branche sans destination = nœud qui suit | `.Alors(…)` / `.Vers(…)` |
| Nom de la tâche = code du nœud | `.Interactif(code, "Nom")` |
| Nom de sortie = nom de la requête (`non-…` pour `SiPas`), `sinon`, `echeance`, nom du signal | `.Nommee("oui")` après la branche |
| Charge utile du signal fusionnée, sans corrélation | `s => s.Correlation(…).SansFusion()` |
| Enfant sur la clé d'affaire du parent | `s => s.CleAffaire(Variable)` |
| Enfant terminé → nœud suivant | `s => s.SiFin("code", …)` |
| Paramètre nommé comme sa variable | `p.Variable("autre", V)` |

Les noms de sortie sont enregistrés dans l'historique et servent à la migration : `.Nommee(…)` permet de les garder stables si une requête est renommée.

## 8. Validation

La construction vérifie toute la définition et lève `DefinitionInvalideException` avec **la liste complète** des erreurs :

- codes de nœuds uniques, un début, au moins une fin ;
- liens vers des codes existants, tous les nœuds atteignables, chaque nœud a une sortie ;
- décision : au moins une branche conditionnelle et exactement un « sinon » ;
- condition sur variable : variable (et variable de comparaison) déclarée, opérateur applicable au type, valeur du même type (entier et décimal se mélangent) ;
- attente : au moins un déclencheur, au plus une échéance ;
- noms de sortie uniques par nœud ;
- branche avec deux destinations, `Alors`/`Vers`/`Nommee` sans branche avant ;
- variables référencées déclarées, variable déclarée deux fois différemment.

Le déploiement vérifie en plus que les commandes et requêtes sont enregistrées et que les sous-processus et leurs mappages sont cohérents.

Un test qui construit toutes les définitions détecte ces erreurs avant le déploiement :

```csharp
foreach (var type in typeof(DemandeConge).Assembly.GetTypes().Where(t => typeof(DefinitionSentier).IsAssignableFrom(t) && !t.IsAbstract))
{
    ((IDefinitionSentier)Activator.CreateInstance(type)!).Definir();
}
```

Pour relire un processus : `ExportateurMermaid.Exporter(definition)` produit un diagramme Mermaid.

## 9. Bonnes pratiques

- **Codes ciblés par un lien en constantes**, comme les variables, pour éviter les fautes de frappe :

  ```csharp
  private const string AttenteRh = "attendre-rh";
  // …
  .Attente(AttenteRh, a => a.Echeance(TimeSpan.FromDays(2)).Alors(b => b.Systemique<Relancer>("relancer").Vers(AttenteRh)))
  ```

- **Codes en kebab-case**, au sens métier (`valider-solde`, pas `etape3`).
- **Ne jamais renommer** un code de nœud, une variable ou un nom de sortie où des instances peuvent se trouver sans prévoir un plan de migration.
- **Chemin principal à gauche** : les cas d'exception dans les branches, le cas normal dans la suite du déroulement.
- **Une décision lit, une commande écrit** : pas d'effet métier dans une requête.

## Aide-mémoire

```csharp
[ProcessusSentier("cle", Nom = "Nom lisible")]
public sealed class MonProcessus : DefinitionSentier
{
    public static readonly Variable<long> X = Variable.Entier("x").Obligatoire();

    protected override void Definir(ConstructeurProcessus processus) => processus
        // options : .InstancesMultiples() .MigrationAutomatique()
        .Systemique<Commande>("code"[, p => …])
        .Interactif("code"[, "Nom de la tâche"][, i => i.Role(…).Assigne(…).Echeance(…).Donnees(…).CommandeEntree<…>().CommandeSortie<…>()])
        .Decision("code", d => d
            .Si<Requete>([p => …])[.Nommee("…")][.Alors(b => …) | .Vers("code")]
            .SiPas<Requete>([p => …])…
            .Si(Variable).EgalA(v | V2) | DifferentDe | SuperieurA | SuperieurOuEgalA | InferieurA | InferieurOuEgalA
                         | Contient("…") | CommencePar("…") | EstVrai() | EstFaux() | EstRenseignee() | EstVide()…
            .Sinon([b => … | "code"]))
        .Attente("code", a => a
            [.CommandeEntree<…>()]
            .Signal("NOM"[, s => s.Correlation(…).SansFusion()])[.Alors(…) | .Vers(…)]
            .Echeance(durée | date | variable)[.Alors(…) | .Vers(…)])
        .AttendreDelai("code", durée)
        .AttendreSignal("code", "NOM")
        .SousProcessus<Enfant>("code"[, s => s.CleAffaire(…).Entree(…).Sortie(…).SiFin("fin"[, …]).SiAnnule([…])])
        .Vers("code")
        .Fin("code");
}
```
