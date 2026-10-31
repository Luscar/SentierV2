# Mnémoniques des objets de base

Les noms de tables, colonnes, séquences, vue, contraintes et index de Sentier suivent les mnémoniques de BpmPlus.
Les mnémoniques absents de BpmPlus ont été créés par analogie : **ils sont à vérifier avec la table officielle des mnémoniques**.

Colonne « Source » : **BpmPlus** = repris tel quel de BpmPlus ; **À vérifier** = créé pour Sentier.

## Mnémoniques

| Mnémonique | Mot | Source | Utilisé dans | Vérifié |
|---|---|---|---|---|
| `ACTIV` | actif / active | À vérifier | `IND_ACTIV`, `_V_INSTC_ACTIV`, `IDX_{P}_DEFIN_ACTIV` | ☐ |
| `AGGRE` | agrégat (clé d'affaire) | BpmPlus | `AGGRE_ID`, `IDX_{P}_INSTC_AGGID` | ☐ |
| `ATTEN` | attente | BpmPlus | `_ATTEN_PROCS`, `_SEQ_ATTEN` | ☐ |
| `AUTO` | automatique | À vérifier | `IND_MIGRA_AUTO` | ☐ |
| `CIBLE` | cible | À vérifier | `NO_SEQ_DEFIN_CIBLE` | ☐ |
| `CLE` | clé | BpmPlus | `CLE`, `CLE_DEFIN`, `CLE_CORRL` | ☐ |
| `CORRL` | corrélation | À vérifier | `CLE_CORRL` | ☐ |
| `COUR` | courant | BpmPlus | `ID_NOEUD_COUR` | ☐ |
| `CREA` | création | BpmPlus | `DH_CREA` | ☐ |
| `DECLN` | déclencheur | À vérifier | `TYP_DECLN` | ☐ |
| `DEFIN` | définition | BpmPlus | `_DEFIN_PROCS`, `CLE_DEFIN`, `NOM_DEFIN`, `DEFIN_JSON` | ☐ |
| `DETL` | détail | BpmPlus | `DETL` | ☐ |
| `DH` | date-heure | BpmPlus | `DH_*` | ☐ |
| `ECHEA` | échéance | À vérifier | `DH_ECHEA`, `IDX_{P}_ATTEN_ECHEA` | ☐ |
| `EMPRE` | empreinte | À vérifier | `EMPRE` | ☐ |
| `ENTRE` | entrée | À vérifier | `DH_ENTRE` | ☐ |
| `ERR` | erreur | À vérifier | `MESS_ERR`, `DH_ERR` | ☐ |
| `EXTE` | externe | BpmPlus | `ID_TACH_EXTE` | ☐ |
| `FIN` | fin | BpmPlus | `DH_FIN`, `ID_NOEUD_FIN` | ☐ |
| `HIST` | historique | À vérifier | `_HIST_NOEUD_PROCS`, `_SEQ_HIST_NOEUD` | ☐ |
| `ID` | identifiant (non séquentiel) | BpmPlus | `ID_NOEUD`, `ID_INSTC_PARN`, `ID_TACH_EXTE` | ☐ |
| `IND` | indicateur (booléen) | À vérifier | `IND_INSTC_UNIQ`, `IND_MIGRA_AUTO`, `IND_ACTIV` | ☐ |
| `INSTC` | instance | BpmPlus | `_INSTC_PROCS`, `NO_SEQ_INSTC_PROCS` | ☐ |
| `JSON` | JSON | BpmPlus | `DEFIN_JSON`, `VAR_JSON` | ☐ |
| `LOGON` | code d'utilisateur | BpmPlus | `LOGON_UTIL` | ☐ |
| `MESS` | message | À vérifier | `MESS_ERR` | ☐ |
| `MIGRA` | migration | À vérifier | `_MIGRA_PROCS`, `IND_MIGRA_AUTO`, `MODE_MIGRA`, `DH_MIGRA` | ☐ |
| `MODE` | mode | À vérifier | `MODE_MIGRA` | ☐ |
| `MODIF` | modification | BpmPlus | `DH_MODIF` | ☐ |
| `NO_SEQ` | numéro séquentiel (clé primaire) | BpmPlus | `NO_SEQ_*` | ☐ |
| `NOEUD` | nœud | BpmPlus | `ID_NOEUD`, `TYP_NOEUD` | ☐ |
| `NOM` | nom | BpmPlus | `NOM_DEFIN`, `NOM_SIGNL`, `NOM_SORTI`, `NOM_SEQ` | ☐ |
| `PARN` | parent | BpmPlus | `ID_INSTC_PARN`, `ID_NOEUD_PARN` | ☐ |
| `PROCS` | processus | BpmPlus | `_DEFIN_PROCS`, `_INSTC_PROCS`… | ☐ |
| `RESUL` | résultat | BpmPlus | `RESUL` | ☐ |
| `SEQ` | séquence | BpmPlus | `_SEQ_*`, `_SEQ`, `NOM_SEQ`, `VAL_SEQ` | ☐ |
| `SIGNL` | signal | BpmPlus | `NOM_SIGNL`, `IDX_{P}_ATTEN_SIGNL` | ☐ |
| `SORTI` | sortie | À vérifier | `NOM_SORTI`, `DH_SORTI` | ☐ |
| `SOURC` | source | À vérifier | `NO_SEQ_DEFIN_SOURC` | ☐ |
| `STAT` | statut | BpmPlus | `STAT` | ☐ |
| `TACH` | tâche | BpmPlus | `ID_TACH_EXTE` | ☐ |
| `TYP` | type | BpmPlus | `TYP_NOEUD`, `TYP_DECLN` | ☐ |
| `UNIQ` | unique | À vérifier | `IND_INSTC_UNIQ`, `UQ_{P}_INSTC_UNIQ` | ☐ |
| `UTIL` | utilisateur | À vérifier | `LOGON_UTIL` | ☐ |
| `V` | vue | À vérifier | `_V_INSTC_ACTIV` | ☐ |
| `VAL` | valeur | BpmPlus | `VAL_SEQ` | ☐ |
| `VAR` | variable | BpmPlus | `VAR_JSON` | ☐ |
| `VERSI` | version | BpmPlus | `VERSI` | ☐ |

Préfixes de contraintes et d'index repris de BpmPlus : `PK_`, `FK_`, `UQ_`, `IDX_` (placés avant le préfixe des tables).

## Choix de correspondance à confirmer

Ces colonnes reprennent un nom de BpmPlus dont le sens est proche, mais pas identique :

| Colonne | Ancien nom Sentier | Remarque |
|---|---|---|
| `AGGRE_ID` | `CLE_AFFAIRE` | Sentier parle de « clé d'affaire » ; BpmPlus d'« agrégat ». Alternative : `CLE_AFFR`. |
| `ID_TACH_EXTE` | `ID_TACHE_COURANTE` | Identifiant de la tâche chez le gestionnaire du client. |
| `ID_NOEUD`, `ID_NOEUD_*` | `CODE_NOEUD`, `CODE_*` | BpmPlus utilise `ID_` pour le code (texte) d'un nœud. |
| `DEFIN_JSON` | `CONTENU` | Contenu JSON canonique de la définition. |
| `CLE` / `CLE_DEFIN` | `CLE_PROCESSUS` | `CLE` dans `_DEFIN_PROCS`, `CLE_DEFIN` dans `_INSTC_PROCS`, comme dans BpmPlus. |
| `VERSI` (instance) | `VERSION` | Version de verrou optimiste, pas la version de la définition. |

## Correspondance complète

| Objet | Ancien nom | Nouveau nom |
|---|---|---|
| Table | `_DEFINITION` | `_DEFIN_PROCS` |
| Table | `_INSTANCE` | `_INSTC_PROCS` |
| Table | `_HIST_NOEUD` | `_HIST_NOEUD_PROCS` |
| Table | `_ATTENTE` | `_ATTEN_PROCS` |
| Table | `_MIGRATION` | `_MIGRA_PROCS` |
| Table (SQLite) | `_SEQUENCE` (`NOM`, `VALEUR`) | `_SEQ` (`NOM_SEQ`, `VAL_SEQ`) |
| Vue | `_V_INSTANCE_ACTIVE` | `_V_INSTC_ACTIV` |
| Séquences | `_SEQ_DEFINITION`, `_SEQ_INSTANCE`, `_SEQ_HIST_NOEUD`, `_SEQ_ATTENTE`, `_SEQ_MIGRATION` | `_SEQ_DEFIN`, `_SEQ_INSTC`, `_SEQ_HIST_NOEUD`, `_SEQ_ATTEN`, `_SEQ_MIGRA` |
| `_DEFIN_PROCS` | `ID`, `CLE_PROCESSUS`, `VERSION`, `NOM`, `INSTANCE_UNIQUE`, `MIGRATION_AUTO`, `CONTENU`, `EMPREINTE`, `EST_ACTIVE`, `DATE_CREATION` | `NO_SEQ_DEFIN_PROCS`, `CLE`, `VERSI`, `NOM_DEFIN`, `IND_INSTC_UNIQ`, `IND_MIGRA_AUTO`, `DEFIN_JSON`, `EMPRE`, `IND_ACTIV`, `DH_CREA` |
| `_INSTC_PROCS` | `ID`, `ID_DEFINITION`, `CLE_PROCESSUS`, `ID_INSTANCE_PARENT`, `CODE_NOEUD_PARENT`, `CLE_AFFAIRE`, `INSTANCE_UNIQUE`, `STATUT`, `CODE_FIN`, `CODE_NOEUD_COURANT`, `ID_TACHE_COURANTE`, `VARIABLES`, `MESSAGE_ERREUR`, `DATE_ERREUR`, `VERSION`, `DATE_CREATION`, `DATE_MAJ`, `DATE_FIN` | `NO_SEQ_INSTC_PROCS`, `NO_SEQ_DEFIN_PROCS`, `CLE_DEFIN`, `ID_INSTC_PARN`, `ID_NOEUD_PARN`, `AGGRE_ID`, `IND_INSTC_UNIQ`, `STAT`, `ID_NOEUD_FIN`, `ID_NOEUD_COUR`, `ID_TACH_EXTE`, `VAR_JSON`, `MESS_ERR`, `DH_ERR`, `VERSI`, `DH_CREA`, `DH_MODIF`, `DH_FIN` |
| `_HIST_NOEUD_PROCS` | `ID`, `ID_INSTANCE`, `CODE_NOEUD`, `TYPE_NOEUD`, `DATE_ENTREE`, `DATE_SORTIE`, `RESULTAT` | `NO_SEQ_HIST_NOEUD_PROCS`, `NO_SEQ_INSTC_PROCS`, `ID_NOEUD`, `TYP_NOEUD`, `DH_ENTRE`, `DH_SORTI`, `RESUL` |
| `_ATTEN_PROCS` | `ID`, `ID_INSTANCE`, `CODE_NOEUD`, `TYPE_DECLENCHEUR`, `NOM_SORTIE`, `DATE_ECHEANCE`, `NOM_SIGNAL`, `CLE_CORRELATION`, `DATE_CREATION` | `NO_SEQ_ATTEN_PROCS`, `NO_SEQ_INSTC_PROCS`, `ID_NOEUD`, `TYP_DECLN`, `NOM_SORTI`, `DH_ECHEA`, `NOM_SIGNL`, `CLE_CORRL`, `DH_CREA` |
| `_MIGRA_PROCS` | `ID`, `ID_INSTANCE`, `ID_DEFINITION_SOURCE`, `ID_DEFINITION_CIBLE`, `MODE`, `DETAIL`, `UTILISATEUR`, `DATE_MIGRATION` | `NO_SEQ_MIGRA_PROCS`, `NO_SEQ_INSTC_PROCS`, `NO_SEQ_DEFIN_SOURC`, `NO_SEQ_DEFIN_CIBLE`, `MODE_MIGRA`, `DETL`, `LOGON_UTIL`, `DH_MIGRA` |

Après correction d'un mnémonique, mettre à jour : `src/Sentier.Persistance/Scripts/Oracle/oracle.sql`, `src/Sentier.Persistance/Scripts/Sqlite/sqlite.sql`, `src/Sentier.Persistance/NomsObjets.cs` et les dépôts de `src/Sentier.Persistance/Depots`. Tous les noms doivent tenir en 30 caractères avec un préfixe de 12 caractères.
