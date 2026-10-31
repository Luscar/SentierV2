namespace Sentier.Variables;

/// <summary>
/// Variables d'une instance, déclarées et typées dans la définition.
/// Toute écriture est validée : variable déclarée, type compatible, nom non réservé.
/// </summary>
public sealed class VariablesProcessus : IVariablesLecture
{
    /// <summary>Variable système : nom de la sortie gagnante d'un nœud d'attente.</summary>
    public const string VariableDeclencheur = "_declencheur";

    /// <summary>Variable système : code de fin du dernier sous-processus terminé.</summary>
    public const string VariableFinEnfant = "_finEnfant";

    private readonly Dictionary<string, object?> _valeurs;
    private readonly IReadOnlyDictionary<string, DeclarationVariable> _declarations;

    internal VariablesProcessus(IReadOnlyDictionary<string, DeclarationVariable> declarations, Dictionary<string, object?> valeurs)
    {
        _declarations = declarations;
        _valeurs = valeurs;
    }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, object?> Valeurs => _valeurs;

    /// <summary>Déclarations des variables de la définition.</summary>
    public IReadOnlyDictionary<string, DeclarationVariable> Declarations => _declarations;

    /// <summary>Indique si un nom est réservé aux variables système.</summary>
    public static bool EstReserve(string nom) => nom.StartsWith('_');

    /// <inheritdoc />
    public bool Contient(string nom) => _valeurs.TryGetValue(nom, out var valeur) && valeur is not null;

    /// <inheritdoc />
    public object? Lire(string nom) => _valeurs.TryGetValue(nom, out var valeur) ? valeur : null;

    /// <inheritdoc />
    public T Lire<T>(Variable<T> variable)
    {
        if (!TryLire(variable, out var valeur))
        {
            throw new VariableInvalideException($"La variable '{variable.Nom}' n'a pas de valeur.");
        }

        return valeur;
    }

    /// <inheritdoc />
    public bool TryLire<T>(Variable<T> variable, out T valeur)
    {
        if (_valeurs.TryGetValue(variable.Nom, out var brute) && brute is not null)
        {
            valeur = ConvertisseurValeurs.VersType<T>(brute, variable.Nom);
            return true;
        }

        valeur = default!;
        return false;
    }

    /// <inheritdoc />
    public T LireOuDefaut<T>(Variable<T> variable, T defaut = default!)
        => TryLire(variable, out var valeur) ? valeur : defaut;

    /// <summary>Écrit une variable typée.</summary>
    public void Ecrire<T>(Variable<T> variable, T valeur) => Ecrire(variable.Nom, valeur);

    /// <summary>
    /// Écrit une variable par son nom. Lève <see cref="VariableInvalideException"/> si la variable
    /// n'est pas déclarée, si son nom est réservé ou si la valeur est du mauvais type.
    /// </summary>
    public void Ecrire(string nom, object? valeur)
    {
        if (EstReserve(nom))
        {
            throw new VariableInvalideException($"Le nom '{nom}' est réservé aux variables système de Sentier.");
        }

        if (!_declarations.TryGetValue(nom, out var declaration))
        {
            throw new VariableInvalideException($"La variable '{nom}' n'est pas déclarée dans la définition.");
        }

        _valeurs[nom] = ConvertisseurValeurs.Normaliser(declaration.Type, valeur, nom);
    }

    /// <summary>Efface la valeur d'une variable déclarée.</summary>
    public void Effacer(string nom) => Ecrire(nom, null);

    internal void EcrireSysteme(string nom, string? valeur) => _valeurs[nom] = valeur;

    /// <summary>Fusionne des données externes (tâche, signal) : seules les clés déclarées sont retenues.</summary>
    internal int Fusionner(IReadOnlyDictionary<string, object?>? donnees)
    {
        if (donnees is null)
        {
            return 0;
        }

        var nombre = 0;
        foreach (var (nom, valeur) in donnees)
        {
            if (!EstReserve(nom) && _declarations.ContainsKey(nom))
            {
                Ecrire(nom, valeur);
                nombre++;
            }
        }

        return nombre;
    }

    /// <summary>Copie brute des valeurs, pour un instantané.</summary>
    internal Dictionary<string, object?> Copier() => new(_valeurs);

    /// <summary>
    /// Initialise les variables d'une nouvelle instance : valeurs fournies validées, valeurs par défaut
    /// appliquées, variables obligatoires vérifiées.
    /// </summary>
    internal static VariablesProcessus Initialiser(
        IReadOnlyDictionary<string, DeclarationVariable> declarations,
        IReadOnlyDictionary<string, object?>? initiales)
    {
        var variables = new VariablesProcessus(declarations, new Dictionary<string, object?>());
        if (initiales is not null)
        {
            foreach (var (nom, valeur) in initiales)
            {
                variables.Ecrire(nom, valeur);
            }
        }

        var manquantes = new List<string>();
        foreach (var declaration in declarations.Values)
        {
            if (variables.Contient(declaration.Nom))
            {
                continue;
            }

            if (declaration.Defaut is not null)
            {
                variables._valeurs[declaration.Nom] = declaration.Defaut.VersObjet(declaration.Nom);
            }
            else if (declaration.Obligatoire)
            {
                manquantes.Add(declaration.Nom);
            }
        }

        if (manquantes.Count > 0)
        {
            throw new VariableInvalideException($"Variables obligatoires manquantes : {string.Join(", ", manquantes)}.");
        }

        return variables;
    }

    /// <summary>Recharge des variables persistées, relues avec leur type déclaré.</summary>
    internal static VariablesProcessus Charger(
        IReadOnlyDictionary<string, DeclarationVariable> declarations,
        ISerialiseurVariablesSentier serialiseur,
        string? json)
    {
        var types = declarations.ToDictionary(d => d.Key, d => d.Value.Type);
        return new VariablesProcessus(declarations, serialiseur.Deserialiser(json, types));
    }
}
