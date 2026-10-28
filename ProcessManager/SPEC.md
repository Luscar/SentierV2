# Spécification — Process Manager léger

## Objectif

Implémenter un process manager minimal, dans une couche indépendante de l'application. La couche ne dépend d'aucun type du domaine. Elle orchestre des processus métier longs composés d'étapes systémiques, de décisions, de tâches utilisateur, d'attentes temporelles et de sous-processus.

La priorité absolue est la légèreté : peu de types, peu de concepts, aucune dépendance externe autre que la sérialisation JSON standard de la plateforme.

## Principes

- Un processus est une classe applicative qui expose une méthode **Demarrer** (signature libre, propre au processus) et des méthodes **Handle**, une par type d'événement. Seul le processus connaît ses handlers.
- Le processus utilise directement les bus de l'application (commandes, queries), injectés par le conteneur d'injection de dépendances. Le moteur ne connaît pas ces bus.
- L'enchaînement se fait par événements internes : un handler (ou Demarrer) lève un événement, ce qui appelle le Handle correspondant du même processus.
- On ne persiste que l'identifiant de l'instance, son étape courante (nœud) et un état sérialisé.
- **On ne commit la transaction qu'aux points de suspension** (tâche utilisateur, attente, appel de sous-processus, fin). Tout ce qui se passe entre deux suspensions est exécuté dans une seule transaction.

## Types d'étapes

| Type | Comment elle s'exprime | Commit |
|---|---|---|
| Systémique | Un handler envoie des commandes puis lève un événement | Non |
| Décision | Un handler appelle une query et lève un événement selon le résultat | Non |
| Tâche utilisateur | Le handler déclare une tâche user nommée, délai optionnel | Oui |
| Attente | Le handler déclare une attente nommée, avec durée relative ou date fixe | Oui |
| Sous-processus | Le handler appelle un autre processus via son propre Demarrer | Oui (le parent est suspendu) |
| Fin | Le handler termine le processus, avec un résultat optionnel | Oui |

Tâche utilisateur et attente reposent sur le même mécanisme. La tâche utilisateur n'est qu'un alias plus lisible.

## Classe de base des processus

Elle offre aux processus concrets :

- l'identifiant de l'instance et l'étape courante (lecture seule) ;
- un **état typé** propre au processus, sérialisé en JSON dans l'instance (classe de base générique sur le type d'état) ;
- **Raise(événement)** : met un événement interne en file ;
- **Correler(clé)** : associe une clé métier (ex. identifiant d'agrégat) à l'instance pour la retrouver lors d'un signal ;
- **TacheUser(étape, délai optionnel)** et **Attendre(étape, délai optionnel ou date fixe)** : déclarent un point de suspension ;
- **Appeler(type de sous-processus, étape, fonction qui appelle son Demarrer)** : déclare un appel de sous-processus ;
- **Terminer(résultat optionnel)**.

Les méthodes de suspension ne font que déclarer une intention. Le moteur applique l'étape et l'état à la fin de l'exécution.

## Instance persistée

Champs : identifiant, type du processus, étape courante, statut, échéance optionnelle, identifiant du parent optionnel, clé de corrélation optionnelle, état sérialisé, version (concurrence optimiste).

Statuts : EnCours, EnAttente, AttenteEnfant, Termine.

## Événements fournis par la couche

- **DelaiExpire** (étape) : l'échéance d'une attente est dépassée.
- **AttenteAbandonnee** (étape, raison) : l'attente a été abandonnée de l'extérieur.
- **SousProcessusTermine** (étape, identifiant de l'enfant, résultat) : un sous-processus appelé est terminé.

Un processus qui attend doit prévoir un Handle pour chacun des déclencheurs qui le concernent.

## Déclencheurs d'une attente

Une attente se termine par le **premier** de ces trois déclencheurs :

1. **Fin de délai** : un job périodique appelle le moteur, qui réveille les instances en attente dont l'échéance est dépassée avec DelaiExpire.
2. **Signal** : l'application envoie un événement à une instance, par identifiant ou par type de processus + clé de corrélation.
3. **Abandon** : l'application abandonne l'attente d'une instance, avec une raison optionnelle.

Les déclencheurs suivants arrivent sur une instance qui n'est plus EnAttente et sont ignorés silencieusement. Un DelaiExpire dont l'échéance n'est pas réellement dépassée (échéance modifiée entre-temps) est aussi ignoré. La version de l'instance garantit qu'un seul de deux déclencheurs concurrents peut commit.

## Moteur

### Points d'entrée publics

- **Demarrer** un processus d'un type donné en lui passant une fonction qui appelle son Demarrer. Retourne l'identifiant de l'instance.
- **Signaler** un événement à une instance (par identifiant, ou par type + clé de corrélation ; aucune action si aucune instance en attente n'est trouvée).
- **Abandonner** l'attente d'une instance.
- **Tick** : traite les échéances dépassées.

Chaque point d'entrée exécute le processus jusqu'à la prochaine suspension ou la fin, puis commit une seule fois.

### Boucle d'exécution

1. Passer l'instance à EnCours et effacer l'échéance.
2. Exécuter le premier appel (Demarrer, ou le Handle du message de réveil).
3. Tant que la file d'événements internes n'est pas vide, défiler et appeler le Handle correspondant.
4. Enregistrer l'état sérialisé dans l'instance.
5. Selon ce qui a été déclaré :
   - **suspension** : enregistrer étape + échéance, statut EnAttente, sauvegarder ;
   - **sous-processus** : enregistrer l'étape, statut AttenteEnfant, sauvegarder, puis démarrer l'enfant (avec le parent référencé) dans la même transaction ;
   - **fin** : statut Termine, sauvegarder ; s'il y a un parent, le recharger et lui envoyer SousProcessusTermine dans la même transaction.

### Règles et erreurs

- Un événement levé après un point de suspension (dans la même exécution) est une erreur.
- Un événement sans Handle correspondant est une erreur explicite qui nomme le processus et le type d'événement.
- Une exécution qui se termine sans suspension ni fin est une erreur (processus sans suite).
- Les exceptions des handlers remontent telles quelles (non enveloppées), et la transaction n'est pas commit.

### Résolution des handlers

Les Handle sont trouvés par réflexion : méthode d'instance nommée Handle, un seul paramètre dont le type correspond exactement à celui de l'événement, publique ou non. Le résultat est mis en cache par couple (type de processus, type d'événement). Un Handle peut être synchrone ou asynchrone.

### Réhydratation

À chaque réveil, le moteur recharge l'instance, crée le processus par la fabrique (conteneur DI), lui attache l'instance et restaure l'état depuis le JSON.

## Ports à fournir par l'application

- **Horloge** (heure courante), pour pouvoir tester les délais.
- **Unité de travail** (commit).
- **Fabrique de processus** à partir d'un type, via le conteneur DI.
- **Store** des instances : charger, sauvegarder (avec contrôle de version), lister les instances échues, trouver une instance en attente par type + clé.

## Points d'attention

- **Idempotence** : en cas d'échec entre deux suspensions, tout est rejoué depuis la dernière suspension. Les commandes envoyées dans cet intervalle doivent tolérer la répétition, ou être exécutées dans la même transaction.
- **Outbox** : si les handlers publient des événements vers l'extérieur, ils doivent passer par une outbox dans la même transaction.
- **Raise est interne** : il ne publie rien hors du processus.

## Exemple de flux (à implémenter comme exemple et test)

Processus de déclaration originale, avec un état contenant l'identifiant d'agrégat et un compteur de relances :

1. **Demarrer(aggregateId)** : mémorise l'agrégat, corrèle, envoie la commande d'identification, interroge le statut de la déclaration. Lève « identifiée » ou « rejet d'identification ».
2. **Identifiée** : envoie la commande de validation, puis appelle le sous-processus Signature.
3. **Rejet d'identification** : tâche utilisateur « CorrectionIdentification », délai de 5 jours. Commit.
4. **Correction soumise** (signal) : relance Demarrer avec l'agrégat mémorisé.
5. **Délai expiré** : incrémente les relances. Au-delà de 2, termine avec « expirée ». Sinon envoie une commande de relance au déclarant et rouvre la tâche utilisateur.
6. **Attente abandonnée** : termine avec « abandonnée ».
7. **Sous-processus terminé** : termine avec le résultat de l'enfant.

Sous-processus **Signature**, réutilisable seul : Demarrer envoie une demande de signature, puis attend une tâche utilisateur de 10 jours. Il termine avec « signée » sur signal, « non-signée » sur délai et « abandonnée » sur abandon.

## Critères d'acceptation (tests)

- Un démarrage qui mène directement à une tâche utilisateur produit une instance EnAttente, avec la bonne étape, l'échéance et l'état sauvegardé, et un seul commit.
- Les étapes systémiques et décisions chaînées ne déclenchent aucune sauvegarde intermédiaire.
- Un signal, une fin de délai et un abandon réveillent chacun le bon Handle.
- Après un premier déclencheur, un second est ignoré.
- Tick ne réveille que les instances échues.
- L'appel d'un sous-processus suspend le parent en AttenteEnfant. La fin de l'enfant reprend le parent avec SousProcessusTermine dans la même transaction.
- Un sous-processus peut aussi être démarré seul.
- Chacune des erreurs listées est levée avec un message explicite.
- L'état est correctement restauré après réhydratation.
- La couche ne référence aucun type de l'application.
