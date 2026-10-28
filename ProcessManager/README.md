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
  Exemple/             DeclarationOriginale + sous-processus Signature, Inscription (plusieurs attentes)
  Infrastructure/      store transactionnel en mémoire, horloge, fabrique
```

## Utilisation

```csharp
var id = await moteur.Demarrer<DeclarationOriginale>(p => p.Demarrer(aggregateId));
await moteur.Signaler<DeclarationOriginale>(aggregateId.ToString(), new CorrectionSoumise(), "CorrectionIdentification");
await moteur.Abandonner(id, "raison");
await moteur.Tick(); // job périodique
```

## Plusieurs tâches user / attentes dans un même processus

Un processus peut enchaîner autant de suspensions qu'il veut : l'étape courante change à chaque
`TacheUser` / `Attendre`. Chaque tâche se termine par son propre signal (un type d'événement et un
`Handle` par signal). Voir `Exemple/Inscription.cs` : Saisie → Carence → Approbation, retour en Saisie sur refus.

- `DelaiExpire` et `AttenteAbandonnee` portent l'étape : un seul `Handle`, aiguillé par `switch (e.Etape)`.
- `Signaler(..., etape)` et `Abandonner(..., etape)` acceptent l'étape attendue : si l'instance n'est pas
  en attente à cette étape, le déclencheur est ignoré (retour `false`). Sans étape, le signal est livré à
  l'étape courante quelle qu'elle soit.

```csharp
await moteur.Signaler(id, new DossierSoumis(), Inscription.EtapeSaisie);
await moteur.Signaler<Inscription>(dossierId.ToString(), new DemandeApprouvee(), Inscription.EtapeApprobation);
```

Les attentes sont séquentielles : une instance n'attend qu'à une étape à la fois.

## Contrat du store

`Sauvegarder` insère si `Version == 0`, sinon met à jour seulement si la version stockée est égale à
`instance.Version` (sinon exception), puis incrémente `instance.Version`. Le store travaille dans la
transaction de l'unité de travail : le moteur appelle `Commit` une seule fois par point d'entrée.

`Tick` traite toutes les instances échues dans une seule transaction (un seul commit).

## Tests

```
dotnet test
```
