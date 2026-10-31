namespace ProcessManager;

/// <summary>Le sous-processus appelé à l'étape <paramref name="Etape"/> est terminé.</summary>
public sealed record SousProcessusTermine(string Etape, Guid EnfantId, string? Resultat);
