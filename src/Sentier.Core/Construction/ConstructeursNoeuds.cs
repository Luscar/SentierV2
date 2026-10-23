using Sentier.Commandes;
using Sentier.Variables;

namespace Sentier.Definitions;

/// <summary>Configuration des paramètres d'un appel de commande ou de requête (ou d'un mappage d'entrée).</summary>
public sealed class ConfigurationParametres
{
    internal List<Parametre> Parametres { get; } = [];

    /// <summary>Paramètre de valeur fixe (texte, entier, décimal, booléen, date ; tout autre objet est stocké en JSON).</summary>
    public ConfigurationParametres Valeur(string nom, object valeur)
    {
        Parametres.Add(new Parametre { Nom = nom, Source = SourceParametre.Statique, Valeur = ValeurTypee.Depuis(valeur, nom: nom) });
        return this;
    }

    /// <summary>Paramètre tiré d'une variable, portant le nom de la variable.</summary>
    public ConfigurationParametres Variable<T>(Variable<T> variable) => Variable(variable.Nom, variable.Nom);

    /// <summary>Paramètre tiré d'une variable.</summary>
    public ConfigurationParametres Variable<T>(string nom, Variable<T> variable) => Variable(nom, variable.Nom);

    /// <summary>Paramètre tiré d'une variable désignée par son nom.</summary>
    public ConfigurationParametres Variable(string nom, string nomVariable)
    {
        Parametres.Add(new Parametre { Nom = nom, Source = SourceParametre.Variable, Variable = nomVariable });
        return this;
    }

    /// <summary>Un paramètre par variable, chacun portant le nom de sa variable.</summary>
    public ConfigurationParametres Variables(params IVariable[] variables)
    {
        foreach (var variable in variables)
        {
            Variable(variable.Nom, variable.Nom);
        }

        return this;
    }

    /// <summary>Paramètre optionnel tiré d'une variable, portant le nom de la variable : absent si la variable n'a pas de valeur.</summary>
    public ConfigurationParametres VariableOptionnelle<T>(Variable<T> variable) => VariableOptionnelle(variable.Nom, variable);

    /// <summary>Paramètre optionnel tiré d'une variable : absent si la variable n'a pas de valeur.</summary>
    public ConfigurationParametres VariableOptionnelle<T>(string nom, Variable<T> variable)
    {
        Parametres.Add(new Parametre { Nom = nom, Source = SourceParametre.Variable, Variable = variable.Nom, Optionnel = true });
        return this;
    }

    /// <summary>Paramètre optionnel tiré d'une variable, avec une valeur par défaut.</summary>
    public ConfigurationParametres VariableOptionnelle<T>(string nom, Variable<T> variable, T defaut)
    {
        Parametres.Add(new Parametre
        {
            Nom = nom,
            Source = SourceParametre.Variable,
            Variable = variable.Nom,
            Optionnel = true,
            Defaut = defaut is null ? null : ValeurTypee.Depuis(defaut, variable.Type, nom),
        });
        return this;
    }

    /// <summary>Paramètre recevant la clé d'affaire de l'instance.</summary>
    public ConfigurationParametres CleAffaire(string nom)
    {
        Parametres.Add(new Parametre { Nom = nom, Source = SourceParametre.CleAffaire });
        return this;
    }

    internal static List<Parametre> Lire(Action<ConfigurationParametres>? configuration)
    {
        var parametres = new ConfigurationParametres();
        configuration?.Invoke(parametres);
        return parametres.Parametres;
    }

    internal static AppelCommande Appel(string nomCommande, Action<ConfigurationParametres>? configuration)
        => new() { Nom = nomCommande, Parametres = Lire(configuration) };
}

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

/// <summary>Déclencheurs d'un nœud d'attente : au plus une échéance, des signaux ; chacun a sa branche.</summary>
/// <example>
/// <code>
/// .Attente("attendre-rh", a =&gt; a
///     .Signal("VALIDATION_RH")
///     .Echeance(TimeSpan.FromDays(2)).Alors(b =&gt; b.Systemique&lt;Relancer&gt;("relancer").Vers("attendre-rh")))
/// </code>
/// </example>
public sealed class ConstructeurAttente : ConstructeurBranches<ConstructeurAttente>
{
    private readonly List<Func<string, Declencheur>> _declencheurs = [];
    private readonly List<AppelCommande> _commandesEntree = [];

    internal ConstructeurAttente()
    {
    }

    /// <summary>Échéance relative à l'entrée dans le nœud. Sa sortie se nomme « echeance ».</summary>
    public ConstructeurAttente Echeance(TimeSpan duree)
        => AjouterEcheance(nom => new DeclencheurEcheance { NomSortie = nom, Duree = duree });

    /// <summary>Échéance à une date absolue (UTC).</summary>
    public ConstructeurAttente Echeance(DateTime date)
    {
        var utc = date.Kind == DateTimeKind.Local ? date.ToUniversalTime() : DateTime.SpecifyKind(date, DateTimeKind.Utc);
        return AjouterEcheance(nom => new DeclencheurEcheance { NomSortie = nom, DateAbsolue = utc });
    }

    /// <summary>Échéance lue dans une variable date (calculée, par exemple, par une commande d'entrée).</summary>
    public ConstructeurAttente Echeance(Variable<DateTime> variable)
        => AjouterEcheance(nom => new DeclencheurEcheance { NomSortie = nom, Variable = variable.Nom });

    /// <summary>Signal nommé ; sa sortie porte le nom du signal et sa charge utile est fusionnée dans les variables.</summary>
    /// <param name="nomSignal">Nom du signal.</param>
    /// <param name="options">Corrélation, fusion de la charge utile.</param>
    public ConstructeurAttente Signal(string nomSignal, Action<OptionsSignal>? options = null)
    {
        var configuration = new OptionsSignal();
        options?.Invoke(configuration);
        _declencheurs.Add(nom => new DeclencheurSignal
        {
            NomSortie = nom,
            NomSignal = nomSignal,
            CleCorrelation = configuration.CleCorrelation,
            VariableCorrelation = configuration.VariableCorrelation,
            FusionnerChargeUtile = configuration.Fusionner,
        });
        return AjouterBranche(new BrancheDeclaree(nomSignal));
    }

    /// <summary>Nom de la sortie du dernier déclencheur, si le nom par défaut ne convient pas.</summary>
    public ConstructeurAttente Nommee(string nomSortie) => Renommer(nomSortie);

    /// <summary>Commande exécutée à l'entrée, avant l'enregistrement des déclencheurs.</summary>
    public ConstructeurAttente CommandeEntree<TCommande>(Action<ConfigurationParametres>? parametres = null) where TCommande : ICommandeSentier
        => CommandeEntree(NomsSentier.NomCommande(typeof(TCommande)), parametres);

    /// <summary>Commande désignée par son nom, exécutée à l'entrée.</summary>
    public ConstructeurAttente CommandeEntree(string nomCommande, Action<ConfigurationParametres>? parametres = null)
    {
        _commandesEntree.Add(ConfigurationParametres.Appel(nomCommande, parametres));
        return this;
    }

    internal NoeudAttente Creer(string code) => new()
    {
        Code = code,
        Declencheurs = _declencheurs.Select((creer, i) => creer(Branches[i].NomSortie)).ToList(),
        CommandesEntree = _commandesEntree.ToList(),
    };

    private ConstructeurAttente AjouterEcheance(Func<string, Declencheur> declencheur)
    {
        _declencheurs.Add(declencheur);
        return AjouterBranche(new BrancheDeclaree(DeclencheurEcheance.SortieParDefaut));
    }
}

/// <summary>Options d'un déclencheur de signal.</summary>
public sealed class OptionsSignal
{
    internal string? CleCorrelation { get; private set; }

    internal string? VariableCorrelation { get; private set; }

    internal bool Fusionner { get; private set; } = true;

    /// <summary>Clé de corrélation fixe.</summary>
    public OptionsSignal Correlation(string cleCorrelation)
    {
        CleCorrelation = cleCorrelation;
        return this;
    }

    /// <summary>Clé de corrélation tirée d'une variable.</summary>
    public OptionsSignal Correlation<T>(Variable<T> variable)
    {
        VariableCorrelation = variable.Nom;
        return this;
    }

    /// <summary>Ne fusionne pas la charge utile du signal dans les variables.</summary>
    public OptionsSignal SansFusion()
    {
        Fusionner = false;
        return this;
    }
}

/// <summary>Configuration d'un nœud interactif.</summary>
public sealed class ConstructeurInteractif
{
    private readonly List<AppelCommande> _entree = [];
    private readonly List<AppelCommande> _sortie = [];
    private readonly List<Parametre> _donnees = [];
    private string? _role;
    private Parametre? _assigne;
    private TimeSpan? _dureeEcheance;
    private string? _variableEcheance;

    internal ConstructeurInteractif()
    {
    }

    /// <summary>Rôle assigné.</summary>
    public ConstructeurInteractif Role(string role)
    {
        _role = role;
        return this;
    }

    /// <summary>Utilisateur assigné, fixe.</summary>
    public ConstructeurInteractif Assigne(string utilisateur)
    {
        _assigne = new Parametre { Nom = "assigne", Source = SourceParametre.Statique, Valeur = ValeurTypee.Depuis(utilisateur) };
        return this;
    }

    /// <summary>Utilisateur assigné, lu dans une variable (calculée, par exemple, par une commande d'entrée).</summary>
    public ConstructeurInteractif Assigne(Variable<string> variable)
    {
        _assigne = new Parametre { Nom = "assigne", Source = SourceParametre.Variable, Variable = variable.Nom, Optionnel = true };
        return this;
    }

    /// <summary>Échéance indicative, relative à la création de la tâche.</summary>
    public ConstructeurInteractif Echeance(TimeSpan duree)
    {
        _dureeEcheance = duree;
        return this;
    }

    /// <summary>Échéance indicative lue dans une variable date.</summary>
    public ConstructeurInteractif Echeance(Variable<DateTime> variable)
    {
        _variableEcheance = variable.Nom;
        return this;
    }

    /// <summary>Variables transmises au gestionnaire de tâches, chacune sous son nom.</summary>
    public ConstructeurInteractif Donnees(params IVariable[] variables) => Donnees(p => p.Variables(variables));

    /// <summary>Données transmises au gestionnaire de tâches.</summary>
    public ConstructeurInteractif Donnees(Action<ConfigurationParametres> donnees)
    {
        _donnees.AddRange(ConfigurationParametres.Lire(donnees));
        return this;
    }

    /// <summary>Commande exécutée à l'entrée, avant la création de la tâche.</summary>
    public ConstructeurInteractif CommandeEntree<TCommande>(Action<ConfigurationParametres>? parametres = null) where TCommande : ICommandeSentier
        => CommandeEntree(NomsSentier.NomCommande(typeof(TCommande)), parametres);

    /// <summary>Commande désignée par son nom, exécutée à l'entrée.</summary>
    public ConstructeurInteractif CommandeEntree(string nomCommande, Action<ConfigurationParametres>? parametres = null)
    {
        _entree.Add(ConfigurationParametres.Appel(nomCommande, parametres));
        return this;
    }

    /// <summary>Commande exécutée à la sortie, après la fusion des données de la tâche.</summary>
    public ConstructeurInteractif CommandeSortie<TCommande>(Action<ConfigurationParametres>? parametres = null) where TCommande : ICommandeSentier
        => CommandeSortie(NomsSentier.NomCommande(typeof(TCommande)), parametres);

    /// <summary>Commande désignée par son nom, exécutée à la sortie.</summary>
    public ConstructeurInteractif CommandeSortie(string nomCommande, Action<ConfigurationParametres>? parametres = null)
    {
        _sortie.Add(ConfigurationParametres.Appel(nomCommande, parametres));
        return this;
    }

    internal NoeudInteractif Creer(string code, string nomTache) => new()
    {
        Code = code,
        NomTache = nomTache,
        Role = _role,
        Assigne = _assigne,
        DureeEcheance = _dureeEcheance,
        VariableEcheance = _variableEcheance,
        Donnees = _donnees.ToList(),
        CommandesEntree = _entree.ToList(),
        CommandesSortie = _sortie.ToList(),
    };
}

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
