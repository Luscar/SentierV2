# ProcessManager

Process manager léger et autonome, implémentant [SPEC.md](SPEC.md) (aucune dépendance hors BCL, `System.Text.Json` pour l'état).

```
src/ProcessManager/
  ProcessusBase.cs     ProcessusBase, Processus<TEtat> : Raise, Correler, TacheUser, Attendre, Appeler, Terminer
  MoteurProcessus.cs   Demarrer, Signaler (id ou type + clé), Abandonner, Tick
  Handlers.cs          résolution des Handle par réflexion (cache par couple processus/événement)
  InstanceProcessus.cs instance persistée + statuts
  Evenements.cs        DelaiExpire, AttenteAbandonnee, SousProcessusTermine, ProcessusException
  Ports.cs             IHorloge, IUniteDeTravail, IFabriqueProcessus, IStoreInstances
tests/ProcessManager.Tests/
  Exemple/             DeclarationOriginale (plusieurs tâches/attentes) + sous-processus Signature
  Infrastructure/      store transactionnel en mémoire, horloge, fabrique
```

## Utilisation

```csharp
var id = await moteur.Demarrer<DeclarationOriginale>(p => p.Demarrer(aggregateId));
await moteur.Signaler<DeclarationOriginale>(aggregateId.ToString(), new CorrectionSoumise(), DeclarationOriginale.EtapeCorrectionIdentification);
await moteur.Abandonner(id, "raison");
await moteur.Tick(); // job périodique
```

## Exemple : déclaration originale

```
Identification ──rejet──► [CorrectionIdentification] ──CorrectionSoumise──► Identification
      │
      ▼
Validation ──────rejet──► [CorrectionValidation] ─────CorrectionSoumise──► Validation
      │
      ▼
[AttenteComptabilisation] (date fixe) ──DelaiExpire──► Comptabilisation ──► Termine « comptabilisée »
                                                            │
                                                          rejet
                                                            ▼
                                     [AutorisationRejet] ──RejetAutorise──► Termine « rejetée »
                                                         └─RejetRefuse────► Comptabilisation
```

Les étapes entre crochets sont des points de suspension (commit) ; identification, validation et
comptabilisation sont des étapes systémiques + décisions exécutées dans la même transaction.
Sur chaque tâche utilisateur : `DelaiExpire` relance (compteur remis à zéro à chaque nouvelle tâche),
au-delà de 2 relances → « expirée » ; `AttenteAbandonnee` → « abandonnée ».

## Plusieurs tâches user / attentes dans un même processus

L'étape courante change à chaque `TacheUser` / `Attendre`. Chaque tâche se termine par son signal
(un type d'événement et un `Handle` par signal) ; un même signal peut servir plusieurs étapes et être
aiguillé par `Etape` (ex. `CorrectionSoumise`).

- `DelaiExpire` et `AttenteAbandonnee` portent l'étape : un seul `Handle`, aiguillé par `e.Etape`.
- `Signaler(..., etape)` et `Abandonner(..., etape)` acceptent l'étape attendue : si l'instance n'est pas
  en attente à cette étape, le déclencheur est ignoré (retour `false`). Sans étape, le signal est livré à
  l'étape courante quelle qu'elle soit.
- Les attentes sont séquentielles : une instance n'attend qu'à une étape à la fois.

## Contrat du store

`Sauvegarder` insère si `Version == 0`, sinon met à jour seulement si la version stockée est égale à
`instance.Version` (sinon exception), puis incrémente `instance.Version`. Le store travaille dans la
transaction de l'unité de travail : le moteur appelle `Commit` une seule fois par point d'entrée.

`Tick` traite toutes les instances échues dans une seule transaction (un seul commit).

## Tests

```
dotnet test
```
