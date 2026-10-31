namespace Sentier.Variables;

/// <summary>Valeurs de variables à fournir au démarrage, écrites avec les constantes typées.</summary>
/// <example><code>ValeursVariables.Avec(NbJours, 5L).Avec(Motif, "Vacances")</code></example>
public sealed class ValeursVariables : Dictionary<string, object?>
{
    /// <summary>Crée un ensemble de valeurs vide.</summary>
    public ValeursVariables() : base(StringComparer.Ordinal)
    {
    }

    /// <summary>Crée un ensemble de valeurs avec une première variable.</summary>
    public static ValeursVariables Avec<T>(Variable<T> variable, T valeur) => new ValeursVariables().Et(variable, valeur);

    /// <summary>Ajoute la valeur d'une variable.</summary>
    public ValeursVariables Et<T>(Variable<T> variable, T valeur)
    {
        this[variable.Nom] = valeur;
        return this;
    }
}
