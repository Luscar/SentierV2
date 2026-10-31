namespace Sentier;

/// <summary>Mode d'une migration d'instance.</summary>
public enum ModeMigration
{
    /// <summary>Migration automatique d'un changement compatible, au déploiement.</summary>
    Auto,

    /// <summary>Migration selon un plan écrit par le client.</summary>
    Plan,
}
