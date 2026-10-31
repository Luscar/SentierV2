namespace ProcessManager;

/// <summary>L'attente <paramref name="Etape"/> a été abandonnée de l'extérieur.</summary>
public sealed record AttenteAbandonnee(string Etape, string? Raison);
