namespace Sentier.Commandes;

/// <summary>Fixe le nom d'une requête. Sans attribut, le nom est celui de la classe.</summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class RequeteSentierAttribute : Attribute
{
    /// <summary>Crée l'attribut.</summary>
    public RequeteSentierAttribute(string nom) => Nom = nom;

    /// <summary>Nom de la requête.</summary>
    public string Nom { get; }
}
