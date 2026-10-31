using Sentier.Commandes;
using Sentier.Variables;

namespace Sentier.Definitions;

/// <summary>
/// Branches d'un nœud de décision : branches conditionnelles (requête ou condition sur variable)
/// évaluées dans l'ordre de déclaration, puis « sinon ».
/// </summary>
/// <example>
/// <code>
/// .Decision("solde-suffisant", d =&gt; d
///     .Si&lt;SoldeSuffisant&gt;(p =&gt; p.Variables(NbJours, Solde))
///     .Sinon(b =&gt; b.Fin("refusee")))
///
/// .Decision("montant", d =&gt; d
///     .Si(Montant).SuperieurA(10_000m).Vers("approbation-direction")
///     .Si(Decision).EgalA("refusee").Alors(b =&gt; b.Fin("refusee"))
///     .Sinon())
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

    private ConstructeurDecision Si(string nomRequete, Action<ConfigurationParametres>? parametres = null)
        => AjouterRequete(nomRequete, false, parametres);

    private ConstructeurDecision SiPas(string nomRequete, Action<ConfigurationParametres>? parametres = null)
        => AjouterRequete(nomRequete, true, parametres);

    /// <summary>
    /// Branche conditionnelle sur une variable, sans requête : l'opérateur suit, par exemple
    /// <c>.Si(Montant).SuperieurA(1000m)</c> ou <c>.Si(Commentaire).EstRenseignee()</c>.
    /// Sa sortie porte l'expression de la condition (<c>montant &gt; 1000</c>) ; <see cref="Nommee"/> la renomme.
    /// </summary>
    public ConditionDecision<T> Si<T>(Variable<T> variable) => new(this, variable.Nom, variable.Type);

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

    internal ConstructeurDecision AjouterCondition(ConditionVariable condition)
        => AjouterBranche(new BrancheDeclaree(NomSortieParDefaut(condition)) { Condition = condition });

    /// <summary>Nom de sortie par défaut d'une condition : son expression, ou une forme courte si elle est trop longue.</summary>
    internal static string NomSortieParDefaut(ConditionVariable condition)
    {
        var expression = condition.Expression();
        return expression.Length <= LongueurMaxSortie ? expression : $"{condition.Variable} {ConditionVariable.Symbole(condition.Operateur)} …";
    }

    /// <summary>Longueur maximale d'un nom de sortie généré.</summary>
    internal const int LongueurMaxSortie = 100;

    private static BrancheDeclaree BrancheSinon() => new(SortieSinon) { EstSinon = true };
}
