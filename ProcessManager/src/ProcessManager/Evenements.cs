namespace ProcessManager;

/// <summary>L'échéance de l'attente <paramref name="Etape"/> est dépassée.</summary>
public sealed record DelaiExpire(string Etape);

/// <summary>L'attente <paramref name="Etape"/> a été abandonnée de l'extérieur.</summary>
public sealed record AttenteAbandonnee(string Etape, string? Raison);

/// <summary>Le sous-processus appelé à l'étape <paramref name="Etape"/> est terminé.</summary>
public sealed record SousProcessusTermine(string Etape, Guid EnfantId, string? Resultat);

/// <summary>Erreur d'utilisation de la couche processus.</summary>
public sealed class ProcessusException(string message) : InvalidOperationException(message);
