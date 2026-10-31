namespace Sentier;

/// <summary>Version de définition telle qu'enregistrée en base.</summary>
public sealed class DefinitionStockee
{
    /// <summary>Identifiant de la version.</summary>
    public IdDefinition Id { get; internal set; }

    /// <summary>Clé du processus.</summary>
    public string CleProcessus { get; internal set; } = "";

    /// <summary>Numéro de version (1, 2, …).</summary>
    public int Version { get; internal set; }

    /// <summary>Nom lisible du processus.</summary>
    public string Nom { get; internal set; } = "";

    /// <summary>Une seule instance active par clé d'affaire.</summary>
    public bool InstanceUnique { get; internal set; }

    /// <summary>Migration automatique des changements compatibles.</summary>
    public bool MigrationAuto { get; internal set; }

    /// <summary>Contenu JSON canonique de la définition.</summary>
    public string Contenu { get; internal set; } = "";

    /// <summary>Empreinte SHA-256 du contenu.</summary>
    public string Empreinte { get; internal set; } = "";

    /// <summary>Vrai pour la dernière version active.</summary>
    public bool EstActive { get; internal set; }

    /// <summary>Date (UTC) de déploiement.</summary>
    public DateTime DateCreation { get; internal set; }
}
