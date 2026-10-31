namespace Sentier.Commandes;

/// <summary>Fixe le nom d'une commande. Sans attribut, le nom est celui de la classe.</summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class CommandeSentierAttribute : Attribute
{
    /// <summary>Crée l'attribut.</summary>
    public CommandeSentierAttribute(string nom) => Nom = nom;

    /// <summary>Nom de la commande.</summary>
    public string Nom { get; }
}
