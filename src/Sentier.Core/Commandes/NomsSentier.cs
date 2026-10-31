using System.Reflection;

namespace Sentier.Commandes;

/// <summary>Résolution des noms de commandes et de requêtes.</summary>
public static class NomsSentier
{
    /// <summary>Nom d'une commande : celui de l'attribut, sinon celui de la classe.</summary>
    public static string NomCommande(Type type)
        => type.GetCustomAttribute<CommandeSentierAttribute>()?.Nom ?? type.Name;

    /// <summary>Nom d'une requête : celui de l'attribut, sinon celui de la classe.</summary>
    public static string NomRequete(Type type)
        => type.GetCustomAttribute<RequeteSentierAttribute>()?.Nom ?? type.Name;

    /// <summary>Clé d'un processus défini par une classe : celle de l'attribut, sinon le nom de la classe.</summary>
    public static string CleProcessus(Type type)
        => type.GetCustomAttribute<Definitions.ProcessusSentierAttribute>()?.Cle ?? type.Name;
}
