namespace Sentier;

/// <summary>Source de la valeur d'un paramètre de commande, de requête ou de mappage.</summary>
public enum SourceParametre
{
    /// <summary>Valeur fixe stockée dans la définition.</summary>
    Statique,

    /// <summary>Valeur d'une variable de l'instance, lue au moment de l'appel.</summary>
    Variable,

    /// <summary>Clé d'affaire de l'instance.</summary>
    CleAffaire,
}
