namespace Sentier;

/// <summary>Aucune définition active n'existe pour la clé de processus demandée.</summary>
public sealed class ProcessusIntrouvableException : SentierException
{
    /// <summary>Crée l'exception.</summary>
    public ProcessusIntrouvableException(string cleProcessus, int? version = null)
        : base(version is null
            ? $"Aucune définition active pour le processus '{cleProcessus}'."
            : $"La version {version} du processus '{cleProcessus}' est introuvable.")
    {
        CleProcessus = cleProcessus;
    }

    /// <summary>Clé du processus recherché.</summary>
    public string CleProcessus { get; }
}
