# Exemple : `IServiceProcessusAffaires` sur Sentier

`ServiceProcessusAffairesSentier.cs` remplace l'appel à l'agent OX (BPM externe) par Sentier, sans changer l'interface utilisée par l'application. Ce fichier n'est pas compilé avec la solution : il dépend des types AT99 / OX02 et se copie dans `AT99.AgentService.OX`.

## Enregistrement

```csharp
services.AddScoped<IDbConnection>(_ => OuvrirConnexionOracle());
services.AddSentier(o =>
{
    o.PrefixeTables = "AT_PROC";
    o.UtiliserOracle().BalayerAssemblyDe<Program>();      // définitions, commandes, requêtes, GestionnaireTachesOX
});
services.AddScoped<IServiceProcessusAffaires, ServiceProcessusAffairesSentier>();
```

Le gestionnaire de tâches (`IGestionnaireTaches`) s'appuie sur `IAgentServiceOXTache` : le bassin de tâches OX reste en place.

## Correspondances à respecter dans les définitions Sentier

| Ancien BPM | Sentier |
|---|---|
| `TypeProcessus.Code`, `TypeSousProcessus.Code` | clé du processus (`ConstructeurProcessus.Nouveau(code)`) |
| Numéro d'identification système (`NomVariableIdentificationSysteme`) | clé d'affaire |
| `processus.VariablesProcessus` (fabrique) | variables déclarées, mêmes noms (une variable non déclarée est refusée au démarrage) |
| Sous-processus `TACHE_EN_ATTENTE` | nœud `SousProcessus` ; l'enfant garde la clé d'affaire du parent et déclare `InstancesMultiples()` si besoin |
| `NUMERO_PERIODE_DECLARATION` | corrélation des signaux : `.Signal(nom, options: s => s.Correlation(NumeroPeriodeDeclaration))` |
| Code de tâche OX (`CodeProchaineTache`) | code du nœud interactif |
| Propriétés de `contenu` (tâche, signal) | variables déclarées du même nom ; les autres sont ignorées |

## Différences de comportement

- **Synchrone** : après un signal, le parent a déjà avancé jusqu'à sa prochaine tâche dans la même transaction. `AvancerATacheSuivanteAsync` vérifie le nœud courant au lieu d'interroger en boucle (jusqu'à 10 essais, environ 30 s).
- **Transaction** : chaque méthode utilise la transaction courante du client, ou en ouvre une sur la connexion de la portée et la valide.
- **Retour de `DemarrerProcessusSynchroneAsync`** et de `AvancerATacheSuivanteAsync` : identifiant de l'instance Sentier (au lieu du numéro de processus OX).
- **`TerminerTacheEnAttenteAsync`** : un seul message d'erreur quand aucune attente ne correspond (l'ancien distinguait « pas de tâche d'attente » et « pas de processus parent »).
