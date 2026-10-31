namespace ProcessManager;

/// <summary>L'échéance de l'attente <paramref name="Etape"/> est dépassée.</summary>
public sealed record DelaiExpire(string Etape);
