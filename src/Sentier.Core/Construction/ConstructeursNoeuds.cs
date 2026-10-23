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

/// <summary>Branches d'un nœud de décision.</summary>
public sealed class ConstructeurDecision
{
    /// <summary>Nom de la sortie « sinon » par défaut.</summary>
    public const string SortieSinon = "sinon";

    internal List<BrancheDeclaree> Branches { get; } = [];

    /// <summary>Branche prise si la requête <typeparamref name="TRequete"/> retourne vrai.</summary>
    /// <param name="nomSortie">Nom de la branche (historique).</param>
    /// <param name="alors">Bloc imbriqué de la branche.</param>
    /// <param name="vers">Lien vers le code d'un nœud déclaré ailleurs.</param>
    /// <param name="parametres">Paramètres de la requête.</param>
    public ConstructeurDecision Si<TRequete>(string nomSortie, Action<ConstructeurBloc>? alors = null, string? vers = null, Action<ConfigurationParametres>? parametres = null)
        where TRequete : IRequeteSentier
        => SiRequete(nomSortie, NomsSentier.NomRequete(typeof(TRequete)), false, alors, vers, parametres);

    /// <summary>Branche prise si la requête <typeparamref name="TRequete"/> retourne faux (résultat inversé).</summary>
    public ConstructeurDecision SiPas<TRequete>(string nomSortie, Action<ConstructeurBloc>? alors = null, string? vers = null, Action<ConfigurationParametres>? parametres = null)
        where TRequete : IRequeteSentier
        => SiRequete(nomSortie, NomsSentier.NomRequete(typeof(TRequete)), true, alors, vers, parametres);

    /// <summary>Branche conditionnelle sur une requête désignée par son nom.</summary>
    public ConstructeurDecision SiRequete(string nomSortie, string nomRequete, bool inverser = false, Action<ConstructeurBloc>? alors = null, string? vers = null, Action<ConfigurationParametres>? parametres = null)
    {
        var requete = new AppelRequete { Nom = nomRequete, Parametres = ConfigurationParametres.Lire(parametres), Inverser = inverser };
        Branches.Add(new BrancheDeclaree(nomSortie, alors, vers, requete));
        return this;
    }

    /// <summary>Branche « sinon », prise si aucune requête ne retourne vrai.</summary>
    public ConstructeurDecision Sinon(Action<ConstructeurBloc>? alors = null, string? vers = null, string nomSortie = SortieSinon)
    {
        Branches.Add(new BrancheDeclaree(nomSortie, alors, vers, EstSinon: true));
        return this;
    }
}

/// <summary>Déclencheurs d'un nœud d'attente.</summary>
public sealed class ConstructeurAttente
{
    private readonly List<Declencheur> _declencheurs = [];
    private readonly List<AppelCommande> _commandesEntree = [];

    internal List<BrancheDeclaree> Branches { get; } = [];

    /// <summary>Échéance relative à l'entrée dans le nœud.</summary>
    public ConstructeurAttente Echeance(TimeSpan duree, Action<ConstructeurBloc>? alors = null, string? vers = null, string nomSortie = DeclencheurEcheance.SortieParDefaut)
        => AjouterEcheance(new DeclencheurEcheance { NomSortie = nomSortie, Duree = duree }, alors, vers);

    /// <summary>Échéance à une date absolue (UTC).</summary>
    public ConstructeurAttente Echeance(DateTime date, Action<ConstructeurBloc>? alors = null, string? vers = null, string nomSortie = DeclencheurEcheance.SortieParDefaut)
        => AjouterEcheance(new DeclencheurEcheance { NomSortie = nomSortie, DateAbsolue = date.Kind == DateTimeKind.Local ? date.ToUniversalTime() : DateTime.SpecifyKind(date, DateTimeKind.Utc) }, alors, vers);

    /// <summary>Échéance lue dans une variable date (calculée, par exemple, par une commande d'entrée).</summary>
    public ConstructeurAttente Echeance(Variable<DateTime> variable, Action<ConstructeurBloc>? alors = null, string? vers = null, string nomSortie = DeclencheurEcheance.SortieParDefaut)
        => AjouterEcheance(new DeclencheurEcheance { NomSortie = nomSortie, Variable = variable.Nom }, alors, vers);

    /// <summary>Signal nommé. Sa sortie porte, par défaut, le nom du signal.</summary>
    public ConstructeurAttente Signal(string nomSignal, Action<ConstructeurBloc>? alors = null, string? vers = null, Action<OptionsSignal>? options = null)
    {
        var configuration = new OptionsSignal();
        options?.Invoke(configuration);
        var declencheur = new DeclencheurSignal
        {
            NomSortie = configuration.NomSortie ?? nomSignal,
            NomSignal = nomSignal,
            CleCorrelation = configuration.CleCorrelation,
            VariableCorrelation = configuration.VariableCorrelation,
            FusionnerChargeUtile = configuration.Fusionner,
        };
        _declencheurs.Add(declencheur);
        Branches.Add(new BrancheDeclaree(declencheur.NomSortie, alors, vers));
        return this;
    }

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
        Declencheurs = _declencheurs.ToList(),
        CommandesEntree = _commandesEntree.ToList(),
    };

    private ConstructeurAttente AjouterEcheance(DeclencheurEcheance declencheur, Action<ConstructeurBloc>? alors, string? vers)
    {
        _declencheurs.Add(declencheur);
        Branches.Add(new BrancheDeclaree(declencheur.NomSortie, alors, vers));
        return this;
    }
}

/// <summary>Options d'un déclencheur de signal.</summary>
public sealed class OptionsSignal
{
    internal string? CleCorrelation { get; private set; }

    internal string? VariableCorrelation { get; private set; }

    internal string? NomSortie { get; private set; }

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

    /// <summary>Nom de la sortie, si différent du nom du signal.</summary>
    public OptionsSignal Sortie(string nomSortie)
    {
        NomSortie = nomSortie;
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

    /// <summary>Données transmises au bassin de tâches.</summary>
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

/// <summary>Configuration d'un nœud sous-processus.</summary>
public sealed class ConstructeurSousProcessus
{
    private readonly List<Parametre> _entrees = [];
    private readonly List<MappageSortie> _sorties = [];
    private string? _variableCleAffaire;

    internal List<BrancheDeclaree> Branches { get; } = [];

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

    /// <summary>Branche suivie lorsque l'enfant se termine sur le nœud de fin <paramref name="codeFin"/>.</summary>
    public ConstructeurSousProcessus SiFin(string codeFin, Action<ConstructeurBloc>? alors = null, string? vers = null)
    {
        Branches.Add(new BrancheDeclaree(codeFin, alors, vers));
        return this;
    }

    /// <summary>Branche suivie lorsque l'enfant est annulé (sans elle, le parent est annulé aussi).</summary>
    public ConstructeurSousProcessus SiAnnule(Action<ConstructeurBloc>? alors = null, string? vers = null)
    {
        Branches.Add(new BrancheDeclaree(NoeudSousProcessus.SortieAnnule, alors, vers));
        return this;
    }

    internal NoeudSousProcessus Creer(string code, string cleProcessus) => new()
    {
        Code = code,
        CleProcessus = cleProcessus,
        VariableCleAffaire = _variableCleAffaire,
        Entrees = _entrees.ToList(),
        Sorties = _sorties.ToList(),
    };
}
