using Sentier.Commandes;

namespace Sentier.Definitions;

/// <summary>Branches d'un nœud de décision : branches conditionnelles évaluées dans l'ordre, puis « sinon ».</summary>
/// <example>
/// <code>
/// .Decision("solde-suffisant", d =&gt; d
///     .Si&lt;SoldeSuffisant&gt;(p =&gt; p.Variables(NbJours, Solde))
///     .Sinon(b =&gt; b.Fin("refusee")))
/// </code>
/// </example>
public sealed class ConstructeurDecision : ConstructeurBranches<ConstructeurDecision>
{
    /// <summary>Nom de la sortie « sinon » par défaut.</summary>
    public const string SortieSinon = "sinon";

    /// <summary>Préfixe du nom de sortie par défaut d'une branche à résultat inversé.</summary>
    public const string PrefixeNon = "non-";

    internal ConstructeurDecision()
    {
    }

    /// <summary>Branche prise si la requête <typeparamref name="TRequete"/> retourne vrai. Sa sortie porte le nom de la requête.</summary>
    public ConstructeurDecision Si<TRequete>(Action<ConfigurationParametres>? parametres = null) where TRequete : IRequeteSentier
        => Si(NomsSentier.NomRequete(typeof(TRequete)), parametres);

    /// <summary>Branche prise si la requête <typeparamref name="TRequete"/> retourne faux. Sa sortie porte le nom « non-requête ».</summary>
    public ConstructeurDecision SiPas<TRequete>(Action<ConfigurationParametres>? parametres = null) where TRequete : IRequeteSentier
        => SiPas(NomsSentier.NomRequete(typeof(TRequete)), parametres);

    /// <summary>Branche conditionnelle sur une requête désignée par son nom.</summary>
    public ConstructeurDecision Si(string nomRequete, Action<ConfigurationParametres>? parametres = null)
        => AjouterRequete(nomRequete, false, parametres);

    /// <summary>Branche à résultat inversé sur une requête désignée par son nom.</summary>
    public ConstructeurDecision SiPas(string nomRequete, Action<ConfigurationParametres>? parametres = null)
        => AjouterRequete(nomRequete, true, parametres);

    /// <summary>Branche « sinon », prise si aucune requête ne retourne vrai ; sans destination, elle mène au nœud suivant.</summary>
    public ConstructeurDecision Sinon() => AjouterBranche(BrancheSinon());

    /// <summary>Branche « sinon » avec son bloc imbriqué.</summary>
    public ConstructeurDecision Sinon(Action<ConstructeurBloc> alors) => AjouterBranche(BrancheSinon(), alors);

    /// <summary>Branche « sinon » avec un lien vers un nœud déclaré ailleurs.</summary>
    public ConstructeurDecision Sinon(string vers) => AjouterBranche(BrancheSinon(), vers);

    /// <summary>Nom de la sortie de la dernière branche (historique), si le nom par défaut ne convient pas.</summary>
    public ConstructeurDecision Nommee(string nomSortie) => Renommer(nomSortie);

    private ConstructeurDecision AjouterRequete(string nomRequete, bool inverser, Action<ConfigurationParametres>? parametres)
        => AjouterBranche(new BrancheDeclaree(inverser ? PrefixeNon + nomRequete : nomRequete)
        {
            Requete = new AppelRequete { Nom = nomRequete, Parametres = ConfigurationParametres.Lire(parametres), Inverser = inverser },
        });

    private static BrancheDeclaree BrancheSinon() => new(SortieSinon) { EstSinon = true };
}
