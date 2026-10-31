namespace Sentier.Definitions;

/// <summary>
/// Fixe la clé (et le nom lisible) d'un processus défini par <see cref="DefinitionSentier"/>.
/// Sans attribut, la clé est le nom de la classe.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class ProcessusSentierAttribute : Attribute
{
    /// <summary>Crée l'attribut.</summary>
    public ProcessusSentierAttribute(string cle) => Cle = cle;

    /// <summary>Clé stable du processus.</summary>
    public string Cle { get; }

    /// <summary>Nom lisible ; par défaut, la clé.</summary>
    public string? Nom { get; init; }
}
