# Exemple : `IServiceProcessusAffaires` sur Sentier

`ServiceProcessusAffairesSentier.cs` remplace l'appel à l'agent OX (BPM externe) par Sentier, sans changer l'interface utilisée par l'application. Ce fichier n'est pas compilé avec la solution : il dépend des types de l'application cliente (`TypeProcessus`, `IFabriqueProcessus`…) et se copie dans son projet, en ajustant l'espace de noms.

## Enregistrement

```csharp
services.AddScoped<IDbConnection>(_ => OuvrirConnexionOracle());
services.AddSentier(o =>
{
    o.PrefixeTables = "SENTIER";
    o.UtiliserOracle().BalayerAssemblyDe<Program>();      // définitions, commandes, requêtes, GestionnaireTachesOX
});
services.AddScoped<IServiceProcessusAffaires, ServiceProcessusAffairesSentier>();
```

Le gestionnaire de tâches (`IGestionnaireTachesSentier`) s'appuie sur `IAgentServiceOXTache` : le bassin de tâches OX reste en place.

## Correspondances à respecter dans les définitions Sentier

| Ancien BPM | Sentier |
|---|---|
| `TypeProcessus.Code`, `TypeSousProcessus.Code` | clé du processus (`ConstructeurProcessus.Nouveau(code)`) |
| Numéro d'identification système (`NomVariableIdentificationSysteme`) | clé d'affaire |
| `processus.VariablesProcessus` (fabrique) | variables déclarées, mêmes noms (une variable non déclarée est refusée au démarrage) |
| Sous-processus `TACHE_EN_ATTENTE` | nœud `SousProcessus` ; l'enfant garde la clé d'affaire du parent et déclare `InstancesMultiples()` si besoin |
| `NUMERO_PERIODE_DECLARATION` | corrélation des signaux : `.Signal(nom, options: s => s.Correlation(NumeroPeriodeDeclaration))` |
| Code de tâche OX (`CodeProchaineTache`) | code du nœud interactif |
| Propriétés de `contenu` (tâche, signal) | variables déclarées sous le nom C# de la propriété (voir ci-dessous) |

## Contenu des tâches et des signaux (`VariablesAffectation`)

Le contenu est sérialisé selon son **type réel** : un appel typé `VariablesAffectation` donne les propriétés de la classe dérivée (`VariablesAnalysePreliminaire`…), sans le discriminant `$type`. Chaque propriété est fusionnée dans la variable Sentier **du même nom** ; une propriété sans variable déclarée est ignorée sans erreur. Déclarer les variables avec `nameof` évite les fautes de frappe :

```csharp
public static readonly Variable<bool> EffectuerAnalyseDetaillee =
    Variable.Booleen(nameof(VariablesAnalysePreliminaire.EffectuerAnalyseDetaillee));
public static readonly Variable<string> LogonAssigne =
    Variable.Texte(nameof(VariablesAnalysePreliminaire.LogonAssigne));

// …
.Interactif("analyse-preliminaire", "Analyse préliminaire", i => i.Assigne(LogonAssigneCreation))
.Decision("detaillee", d => d
    .Si<EstVrai>(p => p.Variable("valeur", EffectuerAnalyseDetaillee))
    .Sinon("fin"))
.Interactif("analyse-detaillee", "Analyse détaillée", i => i.Assigne(LogonAssigne))
```

Types : `bool` → `Booleen`, `string` → `Texte`, `int`/`long` → `Entier`, `decimal` → `Decimal`, `DateTime` → `Date`, objet ou liste → `Json`. Un `enum` est sérialisé en nombre (`Entier`), sauf s'il porte `[JsonConverter(typeof(JsonStringEnumConverter))]` (`Texte`). Une propriété de même nom dans deux classes `VariablesAffectation` alimente la même variable.

## Différences de comportement

- **Synchrone** : après un signal, le parent a déjà avancé jusqu'à sa prochaine tâche dans la même transaction. `AvancerATacheSuivanteAsync` vérifie le nœud courant au lieu d'interroger en boucle (jusqu'à 10 essais, environ 30 s).
- **Transaction** : chaque méthode utilise la transaction courante du client, ou en ouvre une sur la connexion de la portée et la valide.
- **Retour de `DemarrerProcessusSynchroneAsync`** et de `AvancerATacheSuivanteAsync` : identifiant de l'instance Sentier (au lieu du numéro de processus OX).
- **`TerminerTacheEnAttenteAsync`** : un seul message d'erreur quand aucune attente ne correspond (l'ancien distinguait « pas de tâche d'attente » et « pas de processus parent »).
