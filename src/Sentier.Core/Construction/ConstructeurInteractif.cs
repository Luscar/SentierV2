using Sentier.Commandes;
using Sentier.Variables;

namespace Sentier.Definitions;

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
