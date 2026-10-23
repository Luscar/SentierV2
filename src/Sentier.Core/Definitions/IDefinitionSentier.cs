namespace Sentier.Definitions;

/// <summary>
/// Définition d'un processus écrite par le client : une classe par processus, qui déclare
/// ses variables en premier puis son déroulement. Découverte par balayage d'assemblys et
/// déployée par <c>IServiceSentier.DeployerDefinitionsAsync</c>.
/// </summary>
public interface IDefinitionSentier
{
    /// <summary>Construit la définition, typiquement avec <see cref="ConstructeurProcessus"/>.</summary>
    DefinitionProcessus Definir();
}
