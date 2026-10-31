namespace ProcessManager;

internal sealed record Suspension(string Etape, TimeSpan? Delai, DateTimeOffset? Echeance) : Suite($"attente '{Etape}'");
