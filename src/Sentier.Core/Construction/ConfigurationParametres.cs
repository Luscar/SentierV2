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
