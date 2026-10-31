namespace ProcessManager;

internal sealed record Appel(string Etape, Type Type, Func<ProcessusBase, Task> Demarrer) : Suite($"appel de '{Type.Name}' à l'étape '{Etape}'");
