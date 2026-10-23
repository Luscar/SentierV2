-- Sentier : passage au gestionnaire de tâches du client (IGestionnaireTaches), SQLite.
-- Remplacer {{P}} par le préfixe des tables, puis exécuter dans une transaction, application arrêtée.

-- 1. Lien vers la tâche attendue, porté par l'instance.
ALTER TABLE {{P}}_INSTANCE ADD COLUMN ID_TACHE_COURANTE INTEGER;

UPDATE {{P}}_INSTANCE
SET ID_TACHE_COURANTE = (
    SELECT t.ID_TACHE_EXTERNE
    FROM {{P}}_TACHE t
    WHERE t.ID_INSTANCE = {{P}}_INSTANCE.ID AND t.CODE_NOEUD = {{P}}_INSTANCE.CODE_NOEUD_COURANT AND t.STATUT = 'OUVERTE'
    ORDER BY t.ID DESC
    LIMIT 1)
WHERE STATUT IN ('EN_COURS', 'SUSPENDUE', 'EN_ERREUR');

DROP VIEW IF EXISTS {{P}}_V_INSTANCE_ACTIVE;
CREATE VIEW {{P}}_V_INSTANCE_ACTIVE AS
SELECT i.ID, i.CLE_PROCESSUS, d.VERSION AS VERSION_DEFINITION, i.ID_DEFINITION, i.CLE_AFFAIRE, i.STATUT,
       i.CODE_NOEUD_COURANT, i.ID_TACHE_COURANTE, i.ID_INSTANCE_PARENT, i.MESSAGE_ERREUR, i.DATE_CREATION, i.DATE_MAJ
FROM {{P}}_INSTANCE i
JOIN {{P}}_DEFINITION d ON d.ID = i.ID_DEFINITION
WHERE i.STATUT IN ('EN_COURS', 'SUSPENDUE', 'EN_ERREUR');

-- 2. Tâches ouvertes à reprendre dans le stockage du gestionnaire du client
--    (ID_TACHE_EXTERNE devient l'identifiant de TacheUtilisateur, statut Ouverte).
SELECT ID_TACHE_EXTERNE, ID_INSTANCE, CODE_NOEUD FROM {{P}}_TACHE WHERE STATUT = 'OUVERTE' ORDER BY ID;

-- 3. Une fois la reprise vérifiée :
-- DROP TABLE {{P}}_TACHE;
-- DELETE FROM {{P}}_SEQUENCE WHERE NOM = '{{P}}_SEQ_TACHE';
