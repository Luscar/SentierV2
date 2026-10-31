using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Sentier.Definitions;
using Sentier.Variables;

namespace Sentier.Execution;

/// <summary>Instance chargée avec sa définition et ses variables, le temps d'une exécution.</summary>
internal sealed record ExecutionInstance(InstanceProcessus Instance, DefinitionProcessus Definition, VariablesProcessus Variables, string? Utilisateur);
