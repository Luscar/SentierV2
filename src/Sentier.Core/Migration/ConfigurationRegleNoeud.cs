namespace Sentier.Migration;

/// <summary>Configuration de la règle d'un nœud dans un plan de migration.</summary>
public sealed class ConfigurationRegleNoeud
{
    private readonly RegleNoeudMigration _regle;

    internal ConfigurationRegleNoeud(RegleNoeudMigration regle) => _regle = regle;

    /// <summary>Code du nœud correspondant dans la version cible.</summary>
    public ConfigurationRegleNoeud Vers(string codeCible)
    {
        _regle.CodeCible = codeCible;
        return this;
    }

    /// <summary>Conserve les attentes en cours.</summary>
    public ConfigurationRegleNoeud ConserverAttentes()
    {
        _regle.Attentes = ActionAttentes.Conserver;
        return this;
    }

    /// <summary>Recrée les attentes selon le nœud cible (sans exécuter ses commandes d'entrée).</summary>
    public ConfigurationRegleNoeud RecreerAttentes()
    {
        _regle.Attentes = ActionAttentes.Recreer;
        return this;
    }

    /// <summary>Annule les attentes en cours.</summary>
    public ConfigurationRegleNoeud AnnulerAttentes()
    {
        _regle.Attentes = ActionAttentes.Annuler;
        return this;
    }

    /// <summary>Conserve les tâches ouvertes.</summary>
    public ConfigurationRegleNoeud ConserverTaches()
    {
        _regle.Taches = ActionTaches.Conserver;
        return this;
    }

    /// <summary>Annule les tâches ouvertes dans le bassin du client.</summary>
    public ConfigurationRegleNoeud AnnulerTaches()
    {
        _regle.Taches = ActionTaches.Annuler;
        return this;
    }

    /// <summary>
    /// Après la migration, l'instance reprend en exécutant le nœud <paramref name="codeSuite"/> de la version cible.
    /// Ses attentes, tâches ouvertes et sous-processus actifs sont annulés.
    /// </summary>
    public ConfigurationRegleNoeud PuisReprendreA(string codeSuite)
    {
        _regle.CodeSuite = codeSuite;
        _regle.Attentes ??= ActionAttentes.Annuler;
        _regle.Taches ??= ActionTaches.Annuler;
        return this;
    }
}
