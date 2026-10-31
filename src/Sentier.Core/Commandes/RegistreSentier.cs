using System.Reflection;
using Sentier.Definitions;
using Sentier.Migration;

namespace Sentier.Commandes;

/// <summary>
/// Registre des commandes, requêtes, définitions, plans de migration et gestionnaires de tâches
/// découverts par balayage d'assemblys.
/// Enregistré en Singleton.
/// </summary>
public sealed class RegistreSentier
{
    private readonly Dictionary<string, Type> _commandes;
    private readonly Dictionary<string, Type> _requetes;

    private RegistreSentier(Dictionary<string, Type> commandes, Dictionary<string, Type> requetes, List<Type> definitions, List<Type> plans, List<Type> gestionnairesTaches)
    {
        _commandes = commandes;
        _requetes = requetes;
        Definitions = definitions;
        PlansMigration = plans;
        GestionnairesTaches = gestionnairesTaches;
    }

    /// <summary>Commandes enregistrées, par nom.</summary>
    public IReadOnlyDictionary<string, Type> Commandes => _commandes;

    /// <summary>Requêtes enregistrées, par nom.</summary>
    public IReadOnlyDictionary<string, Type> Requetes => _requetes;

    /// <summary>Classes de définitions (<see cref="IDefinitionSentier"/>).</summary>
    public IReadOnlyList<Type> Definitions { get; }

    /// <summary>Classes de plans de migration (<see cref="IPlanMigration"/>).</summary>
    public IReadOnlyList<Type> PlansMigration { get; }

    /// <summary>
    /// Implémentations de <see cref="IGestionnaireTachesSentier"/> trouvées, sans les décorateurs
    /// (classes dont un constructeur reçoit un <see cref="IGestionnaireTachesSentier"/>).
    /// </summary>
    public IReadOnlyList<Type> GestionnairesTaches { get; }

    /// <summary>Balaye les assemblys et construit le registre.</summary>
    /// <exception cref="SentierException">Deux commandes ou deux requêtes portent le même nom.</exception>
    public static RegistreSentier Balayer(IEnumerable<Assembly> assemblys)
    {
        var commandes = new Dictionary<string, Type>(StringComparer.Ordinal);
        var requetes = new Dictionary<string, Type>(StringComparer.Ordinal);
        var definitions = new List<Type>();
        var plans = new List<Type>();
        var gestionnairesTaches = new List<Type>();

        foreach (var type in assemblys.Distinct().SelectMany(TypesConcrets))
        {
            if (typeof(ICommandeSentier).IsAssignableFrom(type))
            {
                Ajouter(commandes, NomsSentier.NomCommande(type), type, "commande");
            }

            if (typeof(IRequeteSentier).IsAssignableFrom(type))
            {
                Ajouter(requetes, NomsSentier.NomRequete(type), type, "requête");
            }

            if (typeof(IDefinitionSentier).IsAssignableFrom(type))
            {
                definitions.Add(type);
            }

            if (typeof(IPlanMigration).IsAssignableFrom(type))
            {
                plans.Add(type);
            }

            if (typeof(IGestionnaireTachesSentier).IsAssignableFrom(type) && !EstDecorateur(type))
            {
                gestionnairesTaches.Add(type);
            }
        }

        return new RegistreSentier(commandes, requetes, definitions, plans, gestionnairesTaches);
    }

    /// <summary>Type de la commande de ce nom, ou <c>null</c>.</summary>
    public Type? TypeCommande(string nom) => _commandes.GetValueOrDefault(nom);

    /// <summary>Type de la requête de ce nom, ou <c>null</c>.</summary>
    public Type? TypeRequete(string nom) => _requetes.GetValueOrDefault(nom);

    private static void Ajouter(Dictionary<string, Type> registre, string nom, Type type, string nature)
    {
        if (registre.TryGetValue(nom, out var existant) && existant != type)
        {
            throw new SentierException($"Deux {nature}s portent le nom '{nom}' : {existant.FullName} et {type.FullName}.");
        }

        registre[nom] = type;
    }

    private static bool EstDecorateur(Type type)
        => type.GetConstructors().Any(c => c.GetParameters().Any(p => p.ParameterType == typeof(IGestionnaireTachesSentier)));

    private static IEnumerable<Type> TypesConcrets(Assembly assembly)
    {
        Type?[] types;
        try
        {
            types = assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            types = ex.Types;
        }

        return types.Where(t => t is { IsClass: true, IsAbstract: false, IsGenericTypeDefinition: false }).Cast<Type>();
    }
}
