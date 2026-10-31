using System.Reflection;
using Sentier.Variables;

namespace Sentier.Commandes;

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
