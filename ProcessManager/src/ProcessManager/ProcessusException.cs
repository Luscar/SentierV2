namespace ProcessManager;

/// <summary>Erreur d'utilisation de la couche processus.</summary>
public sealed class ProcessusException(string message) : InvalidOperationException(message);
