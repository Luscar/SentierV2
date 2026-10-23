using System.Reflection;
using Sentier.Variables;

namespace Sentier.Commandes;

/// <summary>
/// Commande métier exécutée par un nœud. Enregistrée en Scoped par balayage d'assemblys ;
/// elle peut injecter ses propres services et travaille dans la transaction du moteur.
/// </summary>
public interface ICommandeSentier
{
    /// <summary>Exécute la commande.</summary>
    Task ExecuterAsync(ContexteCommande contexte, CancellationToken ct);
}

/// <summary>
/// Requête booléenne en lecture seule, évaluée par une branche de nœud de décision.
/// Même enregistrement que les commandes.
/// </summary>
public interface IRequeteSentier
{
    /// <summary>Évalue la requête.</summary>
    Task<bool> EvaluerAsync(ContexteRequete contexte, CancellationToken ct);
}

/// <summary>Fixe le nom d'une commande. Sans attribut, le nom est celui de la classe.</summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class CommandeSentierAttribute : Attribute
{
    /// <summary>Crée l'attribut.</summary>
    public CommandeSentierAttribute(string nom) => Nom = nom;

    /// <summary>Nom de la commande.</summary>
    public string Nom { get; }
}

/// <summary>Fixe le nom d'une requête. Sans attribut, le nom est celui de la classe.</summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class RequeteSentierAttribute : Attribute
{
    /// <summary>Crée l'attribut.</summary>
    public RequeteSentierAttribute(string nom) => Nom = nom;

    /// <summary>Nom de la requête.</summary>
    public string Nom { get; }
}

/// <summary>Commande dont les paramètres sont liés à une classe, par nom de propriété.</summary>
/// <typeparam name="TParametres">Classe de paramètres.</typeparam>
public abstract class CommandeSentier<TParametres> : ICommandeSentier where TParametres : new()
{
    /// <inheritdoc />
    public Task ExecuterAsync(ContexteCommande contexte, CancellationToken ct)
        => ExecuterAsync(contexte, contexte.Parametres.Lier<TParametres>(), ct);

    /// <summary>Exécute la commande avec ses paramètres typés.</summary>
    protected abstract Task ExecuterAsync(ContexteCommande contexte, TParametres parametres, CancellationToken ct);
}

/// <summary>Requête dont les paramètres sont liés à une classe, par nom de propriété.</summary>
/// <typeparam name="TParametres">Classe de paramètres.</typeparam>
public abstract class RequeteSentier<TParametres> : IRequeteSentier where TParametres : new()
{
    /// <inheritdoc />
    public Task<bool> EvaluerAsync(ContexteRequete contexte, CancellationToken ct)
        => EvaluerAsync(contexte, contexte.Parametres.Lier<TParametres>(), ct);

    /// <summary>Évalue la requête avec ses paramètres typés.</summary>
    protected abstract Task<bool> EvaluerAsync(ContexteRequete contexte, TParametres parametres, CancellationToken ct);
}

/// <summary>Optionnel : code exécuté autour de chaque commande (journalisation, audit).</summary>
public interface IIntercepteurCommande
{
    /// <summary>Intercepte l'exécution ; appeler <paramref name="suivant"/> pour exécuter la commande.</summary>
    Task ExecuterAsync(string nomCommande, ContexteCommande contexte, Func<Task> suivant, CancellationToken ct);
}

/// <summary>Optionnel : code exécuté autour de chaque requête.</summary>
public interface IIntercepteurRequete
{
    /// <summary>Intercepte l'évaluation ; appeler <paramref name="suivant"/> pour évaluer la requête.</summary>
    Task<bool> EvaluerAsync(string nomRequete, ContexteRequete contexte, Func<Task<bool>> suivant, CancellationToken ct);
}

/// <summary>Contexte reçu par une commande.</summary>
public sealed class ContexteCommande
{
    internal ContexteCommande(
        InstanceProcessus instance,
        string codeNoeud,
        MomentCommande moment,
        ParametresResolus parametres,
        VariablesProcessus variables,
        IReadOnlyDictionary<string, object?>? donneesTache,
        string? utilisateur)
    {
        Instance = instance;
        CodeNoeud = codeNoeud;
        Moment = moment;
        Parametres = parametres;
        Variables = variables;
        DonneesTache = donneesTache;
        Utilisateur = utilisateur;
    }

    /// <summary>Instance en cours (lecture seule).</summary>
    public InstanceProcessus Instance { get; }

    /// <summary>Identifiant de l'instance.</summary>
    public IdInstance IdInstance => Instance.Id;

    /// <summary>Clé du processus de l'instance.</summary>
    public string CleProcessus => Instance.CleProcessus;

    /// <summary>Clé d'affaire de l'instance.</summary>
    public long CleAffaire => Instance.CleAffaire;

    /// <summary>Code du nœud qui exécute la commande.</summary>
    public string CodeNoeud { get; }

    /// <summary>Moment d'exécution.</summary>
    public MomentCommande Moment { get; }

    /// <summary>Paramètres déjà résolus.</summary>
    public ParametresResolus Parametres { get; }

    /// <summary>Variables de l'instance, en lecture et en écriture.</summary>
    public VariablesProcessus Variables { get; }

    /// <summary>Données de la tâche complétée (sortie d'un nœud interactif), sinon <c>null</c>.</summary>
    public IReadOnlyDictionary<string, object?>? DonneesTache { get; }

    /// <summary>Utilisateur courant, si disponible.</summary>
    public string? Utilisateur { get; }
}

/// <summary>Contexte reçu par une requête : tout est en lecture seule.</summary>
public sealed class ContexteRequete
{
    internal ContexteRequete(InstanceProcessus instance, string codeNoeud, ParametresResolus parametres, IVariablesLecture variables)
    {
        Instance = instance;
        CodeNoeud = codeNoeud;
        Parametres = parametres;
        Variables = variables;
    }

    /// <summary>Instance en cours (lecture seule).</summary>
    public InstanceProcessus Instance { get; }

    /// <summary>Identifiant de l'instance.</summary>
    public IdInstance IdInstance => Instance.Id;

    /// <summary>Clé du processus de l'instance.</summary>
    public string CleProcessus => Instance.CleProcessus;

    /// <summary>Clé d'affaire de l'instance.</summary>
    public long CleAffaire => Instance.CleAffaire;

    /// <summary>Code du nœud de décision.</summary>
    public string CodeNoeud { get; }

    /// <summary>Paramètres déjà résolus.</summary>
    public ParametresResolus Parametres { get; }

    /// <summary>Variables de l'instance, en lecture seule.</summary>
    public IVariablesLecture Variables { get; }
}

/// <summary>Paramètres résolus d'un appel de commande ou de requête.</summary>
public sealed class ParametresResolus
{
    private readonly IReadOnlyDictionary<string, object?> _valeurs;
    private readonly string _appel;

    internal ParametresResolus(string appel, IReadOnlyDictionary<string, object?> valeurs)
    {
        _appel = appel;
        _valeurs = valeurs;
    }

    /// <summary>Valeurs résolues, par nom.</summary>
    public IReadOnlyDictionary<string, object?> Valeurs => _valeurs;

    /// <summary>Indique si le paramètre est présent avec une valeur non nulle.</summary>
    public bool Contient(string nom) => _valeurs.TryGetValue(nom, out var v) && v is not null;

    /// <summary>Lit un paramètre ; lève <see cref="ParametreInvalideException"/> s'il est absent.</summary>
    public T Lire<T>(string nom)
    {
        if (!_valeurs.TryGetValue(nom, out var valeur) || valeur is null)
        {
            throw new ParametreInvalideException($"{_appel} : le paramètre '{nom}' est absent.");
        }

        return Convertir<T>(nom, valeur);
    }

    /// <summary>Lit un paramètre, ou retourne <paramref name="defaut"/> s'il est absent.</summary>
    public T LireOuDefaut<T>(string nom, T defaut = default!)
        => _valeurs.TryGetValue(nom, out var valeur) && valeur is not null ? Convertir<T>(nom, valeur) : defaut;

    /// <summary>Lie les paramètres aux propriétés publiques modifiables de <typeparamref name="T"/>, par nom (sans casse).</summary>
    public T Lier<T>() where T : new()
    {
        var cible = new T();
        foreach (var propriete in typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!propriete.CanWrite)
            {
                continue;
            }

            var entree = _valeurs.FirstOrDefault(v => string.Equals(v.Key, propriete.Name, StringComparison.OrdinalIgnoreCase));
            if (entree.Key is null || entree.Value is null)
            {
                continue;
            }

            try
            {
                propriete.SetValue(cible, ConvertisseurValeurs.VersType(entree.Value, propriete.PropertyType, entree.Key));
            }
            catch (VariableInvalideException ex)
            {
                throw new ParametreInvalideException($"{_appel} : {ex.Message}");
            }
        }

        return cible;
    }

    private T Convertir<T>(string nom, object valeur)
    {
        try
        {
            return ConvertisseurValeurs.VersType<T>(valeur, nom);
        }
        catch (VariableInvalideException ex)
        {
            throw new ParametreInvalideException($"{_appel} : {ex.Message}");
        }
    }
}

/// <summary>Résolution des noms de commandes et de requêtes.</summary>
public static class NomsSentier
{
    /// <summary>Nom d'une commande : celui de l'attribut, sinon celui de la classe.</summary>
    public static string NomCommande(Type type)
        => type.GetCustomAttribute<CommandeSentierAttribute>()?.Nom ?? type.Name;

    /// <summary>Nom d'une requête : celui de l'attribut, sinon celui de la classe.</summary>
    public static string NomRequete(Type type)
        => type.GetCustomAttribute<RequeteSentierAttribute>()?.Nom ?? type.Name;
}
