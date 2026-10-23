namespace Sentier;

/// <summary>Exception de base de toutes les erreurs levées par Sentier.</summary>
public class SentierException : Exception
{
    /// <summary>Crée une exception Sentier.</summary>
    public SentierException(string message, Exception? interne = null) : base(message, interne)
    {
    }
}

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

/// <summary>Aucune transition sortante ne correspond au résultat d'un nœud.</summary>
public sealed class AucuneTransitionException : SentierException
{
    /// <summary>Crée l'exception.</summary>
    public AucuneTransitionException(string cleProcessus, string codeNoeud, string? nomSortie)
        : base(nomSortie is null
            ? $"Processus '{cleProcessus}' : aucune transition sortante pour le nœud '{codeNoeud}'."
            : $"Processus '{cleProcessus}' : aucune transition '{nomSortie}' pour le nœud '{codeNoeud}'.")
    {
    }
}

/// <summary>L'instance a été modifiée par un autre traitement (verrou optimiste).</summary>
public sealed class ConflitConcurrenceException : SentierException
{
    /// <summary>Crée l'exception.</summary>
    public ConflitConcurrenceException(IdInstance idInstance, int versionAttendue)
        : base($"L'instance {idInstance} a été modifiée par un autre traitement (version attendue : {versionAttendue}).")
    {
        IdInstance = idInstance;
    }

    /// <summary>Instance en conflit.</summary>
    public IdInstance IdInstance { get; }
}

/// <summary>Le nombre maximal d'étapes par exécution a été dépassé.</summary>
public sealed class BoucleInfinieException : SentierException
{
    /// <summary>Crée l'exception.</summary>
    public BoucleInfinieException(IdInstance idInstance, int maxEtapes)
        : base($"L'instance {idInstance} a dépassé le nombre maximal de {maxEtapes} étapes par exécution : boucle infinie probable.")
    {
    }
}

/// <summary>Une commande référencée n'est pas enregistrée.</summary>
public sealed class CommandeIntrouvableException : SentierException
{
    /// <summary>Crée l'exception.</summary>
    public CommandeIntrouvableException(string nomCommande)
        : base($"La commande '{nomCommande}' n'est pas enregistrée. Vérifiez les assemblys balayés par AddSentier.")
    {
    }
}

/// <summary>Une requête référencée n'est pas enregistrée.</summary>
public sealed class RequeteIntrouvableException : SentierException
{
    /// <summary>Crée l'exception.</summary>
    public RequeteIntrouvableException(string nomRequete)
        : base($"La requête '{nomRequete}' n'est pas enregistrée. Vérifiez les assemblys balayés par AddSentier.")
    {
    }
}

/// <summary>Un paramètre de commande ou de requête est absent ou invalide.</summary>
public sealed class ParametreInvalideException : SentierException
{
    /// <summary>Crée l'exception.</summary>
    public ParametreInvalideException(string message) : base(message)
    {
    }
}

/// <summary>Aucune transaction n'est active au moment de l'appel.</summary>
public sealed class TransactionRequiseException : SentierException
{
    /// <summary>Crée l'exception.</summary>
    public TransactionRequiseException()
        : base("Sentier exige une transaction active, fournie par le client via ISessionSentier. Sentier n'ouvre jamais de transaction lui-même.")
    {
    }
}

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

/// <summary>Un plan de migration est incomplet ou incohérent.</summary>
public sealed class MigrationInvalideException : SentierException
{
    /// <summary>Crée l'exception.</summary>
    public MigrationInvalideException(string message) : base(message)
    {
    }
}

/// <summary>Variable non déclarée, de mauvais type, ou obligatoire manquante.</summary>
public sealed class VariableInvalideException : SentierException
{
    /// <summary>Crée l'exception.</summary>
    public VariableInvalideException(string message) : base(message)
    {
    }
}
