namespace Sentier;

/// <summary>
/// Contrat commun des identifiants typés de Sentier : chaque entité persistée possède
/// son propre type d'identifiant, qui encapsule un <see cref="long"/>.
/// </summary>
/// <typeparam name="TSelf">Type concret de l'identifiant.</typeparam>
public interface IIdentifiant<TSelf> where TSelf : struct, IIdentifiant<TSelf>
{
    /// <summary>Valeur brute de l'identifiant.</summary>
    long Valeur { get; }

    /// <summary>Crée un identifiant à partir de sa valeur brute.</summary>
    static abstract TSelf Depuis(long valeur);
}
