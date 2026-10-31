namespace Sentier.Migration;

/// <summary>Règle de migration d'un nœud de la version source.</summary>
public sealed class RegleNoeudMigration
{
    /// <summary>Code du nœud dans la version source.</summary>
    public required string CodeSource { get; init; }

    /// <summary>Code correspondant dans la version cible ; par défaut, le même code.</summary>
    public string? CodeCible { get; internal set; }

    /// <summary>Traitement des attentes ; par défaut, conservées si compatibles, sinon recréées.</summary>
    public ActionAttentes? Attentes { get; internal set; }

    /// <summary>Traitement des tâches ; par défaut, conservées.</summary>
    public ActionTaches? Taches { get; internal set; }

    /// <summary>Nœud de la version cible où l'instance reprend son exécution après la migration.</summary>
    public string? CodeSuite { get; internal set; }
}
