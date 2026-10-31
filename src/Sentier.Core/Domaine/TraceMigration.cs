namespace Sentier;

/// <summary>Trace d'une migration d'instance vers une autre version.</summary>
public sealed class TraceMigration
{
    /// <summary>Identifiant de la trace.</summary>
    public IdMigration Id { get; internal set; }

    /// <summary>Instance migrée.</summary>
    public IdInstance IdInstance { get; internal set; }

    /// <summary>Version de définition d'origine.</summary>
    public IdDefinition IdDefinitionSource { get; internal set; }

    /// <summary>Version de définition cible.</summary>
    public IdDefinition IdDefinitionCible { get; internal set; }

    /// <summary>Mode de migration.</summary>
    public ModeMigration Mode { get; internal set; }

    /// <summary>Détail JSON des actions effectuées.</summary>
    public string? Detail { get; internal set; }

    /// <summary>Utilisateur à l'origine de la migration.</summary>
    public string? Utilisateur { get; internal set; }

    /// <summary>Date (UTC) de la migration.</summary>
    public DateTime DateMigration { get; internal set; }
}
