using System.Reflection;
using Sentier.Commandes;
using Sentier.Variables;

namespace Sentier.Definitions;

/// <summary>
/// Définition d'un processus réduite à son déroulement. La clé vient de
/// <see cref="ProcessusSentierAttribute"/> (sinon, le nom de la classe), les champs statiques
/// <see cref="Variable{T}"/> de la classe sont déclarés automatiquement, dans leur ordre
/// d'écriture, et Sentier appelle <see cref="ConstructeurProcessus.Construire"/>.
/// </summary>
/// <example>
/// <code>
/// [ProcessusSentier("demande-conge", Nom = "Demande de congé")]
/// public sealed class DemandeConge : DefinitionSentier
/// {
///     public static readonly Variable&lt;long&gt; NbJours = Variable.Entier("nbJours").Obligatoire();
///
///     protected override void Definir(ConstructeurProcessus processus) =&gt; processus
///         .Interactif("approbation", "Approuver la demande", i =&gt; i.Donnees(NbJours))
///         .Fin("approuvee");
/// }
/// </code>
/// </example>
public abstract class DefinitionSentier : IDefinitionSentier
{
    /// <summary>Décrit le déroulement du processus, de haut en bas.</summary>
    /// <param name="processus">Constructeur déjà initialisé avec la clé, le nom et les variables.</param>
    protected abstract void Definir(ConstructeurProcessus processus);

    /// <inheritdoc />
    DefinitionProcessus IDefinitionSentier.Definir()
    {
        var type = GetType();
        var processus = ConstructeurProcessus.Nouveau(NomsSentier.CleProcessus(type), type.GetCustomAttribute<ProcessusSentierAttribute>()?.Nom)
            .Variables(VariablesDeclarees(type));
        Definir(processus);
        return processus.Construire();
    }

    private static IVariable[] VariablesDeclarees(Type type) => type
        .GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
        .Where(champ => typeof(IVariable).IsAssignableFrom(champ.FieldType))
        .OrderBy(champ => champ.MetadataToken)
        .Select(champ => (IVariable?)champ.GetValue(null))
        .OfType<IVariable>()
        .ToArray();
}
