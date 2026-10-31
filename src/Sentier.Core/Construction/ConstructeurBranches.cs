namespace Sentier.Definitions;

/// <summary>
/// Méthodes communes aux nœuds à plusieurs sorties. Une branche se déclare, puis sa destination
/// s'écrit juste après : <c>.Alors(b =&gt; ...)</c> (bloc imbriqué) ou <c>.Vers("code")</c> (lien).
/// Sans destination, la branche mène au nœud qui suit le nœud à plusieurs sorties.
/// </summary>
/// <typeparam name="TSelf">Type concret du constructeur, pour le chaînage.</typeparam>
public abstract class ConstructeurBranches<TSelf> where TSelf : ConstructeurBranches<TSelf>
{
    private protected ConstructeurBranches()
    {
    }

    internal List<BrancheDeclaree> Branches { get; } = [];

    internal List<string> Erreurs { get; } = [];

    private protected TSelf Moi => (TSelf)this;

    /// <summary>Bloc imbriqué de la dernière branche déclarée (style imbriqué).</summary>
    public TSelf Alors(Action<ConstructeurBloc> bloc) => Destination(nameof(Alors), b => b.Bloc = bloc);

    /// <summary>Lien de la dernière branche déclarée vers le code d'un nœud déclaré ailleurs (style plat).</summary>
    public TSelf Vers(string code) => Destination(nameof(Vers), b => b.Lien = code);

    private protected TSelf AjouterBranche(BrancheDeclaree branche)
    {
        Branches.Add(branche);
        return Moi;
    }

    private protected TSelf AjouterBranche(BrancheDeclaree branche, Action<ConstructeurBloc> alors)
    {
        branche.Bloc = alors;
        return AjouterBranche(branche);
    }

    private protected TSelf AjouterBranche(BrancheDeclaree branche, string vers)
    {
        branche.Lien = vers;
        return AjouterBranche(branche);
    }

    /// <summary>Renomme la sortie de la dernière branche déclarée.</summary>
    private protected TSelf Renommer(string nomSortie)
    {
        if (Derniere("Nommee") is { } branche)
        {
            branche.NomSortie = nomSortie;
        }

        return Moi;
    }

    private TSelf Destination(string methode, Action<BrancheDeclaree> appliquer)
    {
        if (Derniere(methode) is not { } branche)
        {
            return Moi;
        }

        if (branche.Bloc is not null || branche.Lien is not null)
        {
            Erreurs.Add($"la branche '{branche.NomSortie}' a déjà une destination ({methode}).");
        }
        else
        {
            appliquer(branche);
        }

        return Moi;
    }

    private BrancheDeclaree? Derniere(string methode)
    {
        if (Branches.Count == 0)
        {
            Erreurs.Add($"{methode}(...) doit suivre la déclaration d'une branche.");
            return null;
        }

        return Branches[^1];
    }
}
