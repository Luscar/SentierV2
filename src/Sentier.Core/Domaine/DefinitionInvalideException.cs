namespace Sentier;

/// <summary>Une définition de processus est invalide (construction ou déploiement).</summary>
public sealed class DefinitionInvalideException : SentierException
{
    /// <summary>Crée l'exception.</summary>
    public DefinitionInvalideException(string cleProcessus, IReadOnlyList<string> erreurs)
        : base($"Définition '{cleProcessus}' invalide :{Environment.NewLine}- {string.Join(Environment.NewLine + "- ", erreurs)}")
    {
        CleProcessus = cleProcessus;
        Erreurs = erreurs;
    }

    /// <summary>Clé du processus concerné.</summary>
    public string CleProcessus { get; }

    /// <summary>Liste des erreurs détectées.</summary>
    public IReadOnlyList<string> Erreurs { get; }
}
