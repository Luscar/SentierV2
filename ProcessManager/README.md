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
  Exemple/             DeclarationOriginale + sous-processus Signature
  Infrastructure/      store transactionnel en mémoire, horloge, fabrique
```

## Utilisation

```csharp
var id = await moteur.Demarrer<DeclarationOriginale>(p => p.Demarrer(aggregateId));
await moteur.Signaler<DeclarationOriginale>(aggregateId.ToString(), new CorrectionSoumise());
await moteur.Abandonner(id, "raison");
await moteur.Tick(); // job périodique
```

## Contrat du store

`Sauvegarder` insère si `Version == 0`, sinon met à jour seulement si la version stockée est égale à
`instance.Version` (sinon exception), puis incrémente `instance.Version`. Le store travaille dans la
transaction de l'unité de travail : le moteur appelle `Commit` une seule fois par point d'entrée.

`Tick` traite toutes les instances échues dans une seule transaction (un seul commit).

## Tests

```
dotnet test
```
