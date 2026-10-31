using Sentier.Variables;

namespace Sentier.Definitions;

/// <summary>Options d'un déclencheur de signal.</summary>
public sealed class OptionsSignal
{
    internal string? CleCorrelation { get; private set; }

    internal string? VariableCorrelation { get; private set; }

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

    /// <summary>Ne fusionne pas la charge utile du signal dans les variables.</summary>
    public OptionsSignal SansFusion()
    {
        Fusionner = false;
        return this;
    }
}
