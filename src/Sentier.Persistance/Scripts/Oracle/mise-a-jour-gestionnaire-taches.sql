-- Sentier : passage au gestionnaire de tâches du client (IGestionnaireTaches), Oracle.
-- Remplacer {{P}} par le préfixe des tables, puis exécuter dans une transaction, application arrêtée.

-- 1. Lien vers la tâche attendue, porté par l'instance.
ALTER TABLE {{P}}_INSTANCE ADD (ID_TACHE_COURANTE NUMBER(19));

UPDATE {{P}}_INSTANCE i
SET ID_TACHE_COURANTE = (
    SELECT MAX(t.ID_TACHE_EXTERNE) KEEP (DENSE_RANK LAST ORDER BY t.ID)
    FROM {{P}}_TACHE t
    WHERE t.ID_INSTANCE = i.ID AND t.CODE_NOEUD = i.CODE_NOEUD_COURANT AND t.STATUT = 'OUVERTE')
WHERE i.STATUT IN ('EN_COURS', 'SUSPENDUE', 'EN_ERREUR');

CREATE OR REPLACE VIEW {{P}}_V_INSTANCE_ACTIVE AS
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
-- DROP SEQUENCE {{P}}_SEQ_TACHE;
