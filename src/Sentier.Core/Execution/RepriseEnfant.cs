using Sentier.Variables;

namespace Sentier.Execution;

/// <summary>Une instance enfant s'est terminée ou a été annulée.</summary>
public sealed record RepriseEnfant(InstanceProcessus Enfant, IVariablesLecture? VariablesEnfant) : EvenementReprise;
