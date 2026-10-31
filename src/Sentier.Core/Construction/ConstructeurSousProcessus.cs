using Sentier.Variables;

namespace Sentier.Definitions;

/// <summary>Configuration d'un nœud sous-processus : mappage des variables et branches par code de fin.</summary>
public sealed class ConstructeurSousProcessus : ConstructeurBranches<ConstructeurSousProcessus>
{
    private readonly List<Parametre> _entrees = [];
    private readonly List<MappageSortie> _sorties = [];
    private string? _variableCleAffaire;

    internal ConstructeurSousProcessus()
    {
    }

    /// <summary>L'enfant vise la clé d'affaire lue dans une variable entière du parent (par défaut : celle du parent).</summary>
    public ConstructeurSousProcessus CleAffaire(Variable<long> variable)
    {
        _variableCleAffaire = variable.Nom;
        return this;
    }

    /// <summary>Copie une variable du parent dans une variable de l'enfant, au démarrage.</summary>
    public ConstructeurSousProcessus Entree<T>(Variable<T> variableEnfant, Variable<T> variableParent)
        => Entree(variableEnfant.Nom, variableParent.Nom);

    /// <summary>Copie une variable du parent dans une variable de l'enfant, désignées par leur nom.</summary>
    public ConstructeurSousProcessus Entree(string variableEnfant, string variableParent)
    {
        _entrees.Add(new Parametre { Nom = variableEnfant, Source = SourceParametre.Variable, Variable = variableParent });
        return this;
    }

    /// <summary>Remplit une variable de l'enfant avec une valeur fixe.</summary>
    public ConstructeurSousProcessus EntreeValeur<T>(Variable<T> variableEnfant, T valeur) where T : notnull
    {
        _entrees.Add(new Parametre { Nom = variableEnfant.Nom, Source = SourceParametre.Statique, Valeur = ValeurTypee.Depuis(valeur, variableEnfant.Type, variableEnfant.Nom) });
        return this;
    }

    /// <summary>Remplit une variable entière de l'enfant avec la clé d'affaire du parent.</summary>
    public ConstructeurSousProcessus EntreeCleAffaire(Variable<long> variableEnfant)
    {
        _entrees.Add(new Parametre { Nom = variableEnfant.Nom, Source = SourceParametre.CleAffaire });
        return this;
    }

    /// <summary>À la fin normale de l'enfant, recopie une de ses variables dans une variable du parent.</summary>
    public ConstructeurSousProcessus Sortie<T>(Variable<T> variableEnfant, Variable<T> variableParent)
        => Sortie(variableEnfant.Nom, variableParent.Nom);

    /// <summary>Mappage de sortie par noms de variables.</summary>
    public ConstructeurSousProcessus Sortie(string variableEnfant, string variableParent)
    {
        _sorties.Add(new MappageSortie { VariableEnfant = variableEnfant, VariableParent = variableParent });
        return this;
    }

    /// <summary>Branche suivie lorsque l'enfant se termine sur le nœud de fin <paramref name="codeFin"/> ; sans destination, elle mène au nœud suivant.</summary>
    public ConstructeurSousProcessus SiFin(string codeFin) => AjouterBranche(new BrancheDeclaree(codeFin));

    /// <summary>Branche de fin avec son bloc imbriqué.</summary>
    public ConstructeurSousProcessus SiFin(string codeFin, Action<ConstructeurBloc> alors) => AjouterBranche(new BrancheDeclaree(codeFin), alors);

    /// <summary>Branche de fin avec un lien vers un nœud déclaré ailleurs.</summary>
    public ConstructeurSousProcessus SiFin(string codeFin, string vers) => AjouterBranche(new BrancheDeclaree(codeFin), vers);

    /// <summary>Branche suivie lorsque l'enfant est annulé (sans elle, le parent est annulé aussi).</summary>
    public ConstructeurSousProcessus SiAnnule() => AjouterBranche(new BrancheDeclaree(NoeudSousProcessus.SortieAnnule));

    /// <summary>Branche d'annulation avec son bloc imbriqué.</summary>
    public ConstructeurSousProcessus SiAnnule(Action<ConstructeurBloc> alors) => AjouterBranche(new BrancheDeclaree(NoeudSousProcessus.SortieAnnule), alors);

    /// <summary>Branche d'annulation avec un lien vers un nœud déclaré ailleurs.</summary>
    public ConstructeurSousProcessus SiAnnule(string vers) => AjouterBranche(new BrancheDeclaree(NoeudSousProcessus.SortieAnnule), vers);

    internal NoeudSousProcessus Creer(string code, string cleProcessus) => new()
    {
        Code = code,
        CleProcessus = cleProcessus,
        VariableCleAffaire = _variableCleAffaire,
        Entrees = _entrees.ToList(),
        Sorties = _sorties.ToList(),
    };
}
