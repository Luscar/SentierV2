namespace Sentier.Execution;

/// <summary>Une tâche utilisateur a été complétée.</summary>
public sealed record RepriseTache(TacheSentier Tache, IReadOnlyDictionary<string, object?>? Donnees, string? Utilisateur) : EvenementReprise;
