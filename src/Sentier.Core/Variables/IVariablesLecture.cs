namespace Sentier.Variables;

/// <summary>Accès en lecture seule aux variables d'une instance.</summary>
public interface IVariablesLecture
{
    /// <summary>Valeurs courantes, dans leur forme canonique.</summary>
    IReadOnlyDictionary<string, object?> Valeurs { get; }

    /// <summary>Indique si la variable a une valeur non nulle.</summary>
    bool Contient(string nom);

    /// <summary>Lit une variable ; lève <see cref="VariableInvalideException"/> si elle est absente.</summary>
    T Lire<T>(Variable<T> variable);

    /// <summary>Tente de lire une variable.</summary>
    bool TryLire<T>(Variable<T> variable, out T valeur);

    /// <summary>Lit une variable, ou retourne <paramref name="defaut"/> si elle est absente.</summary>
    T LireOuDefaut<T>(Variable<T> variable, T defaut = default!);

    /// <summary>Lit une variable par son nom (forme canonique), ou <c>null</c>.</summary>
    object? Lire(string nom);
}
