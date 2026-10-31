using Sentier.Variables;

namespace Sentier.Execution;

/// <summary>Vue en lecture seule des variables, transmise aux requêtes.</summary>
internal sealed class VariablesLectureSeule(IVariablesLecture source) : IVariablesLecture
{
    public IReadOnlyDictionary<string, object?> Valeurs => source.Valeurs;

    public bool Contient(string nom) => source.Contient(nom);

    public T Lire<T>(Variable<T> variable) => source.Lire(variable);

    public bool TryLire<T>(Variable<T> variable, out T valeur) => source.TryLire(variable, out valeur);

    public T LireOuDefaut<T>(Variable<T> variable, T defaut = default!) => source.LireOuDefaut(variable, defaut);

    public object? Lire(string nom) => source.Lire(nom);
}
