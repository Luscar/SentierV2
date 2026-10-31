namespace Sentier.Execution;

/// <summary>Un déclencheur d'attente (échéance ou signal) a gagné.</summary>
public sealed record RepriseDeclencheur(Attente Attente, IReadOnlyDictionary<string, object?>? ChargeUtile) : EvenementReprise;
