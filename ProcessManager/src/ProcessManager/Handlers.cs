using System.Collections.Concurrent;
using System.Reflection;

namespace ProcessManager;

/// <summary>Résolution par réflexion (avec cache) et invocation des méthodes Handle.</summary>
internal static class Handlers
{
    private const BindingFlags Recherche = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

    private static readonly ConcurrentDictionary<(Type Processus, Type Evenement), MethodInfo?> Cache = new();

    public static async Task Invoquer(ProcessusBase processus, object evenement)
    {
        var methode = Cache.GetOrAdd((processus.GetType(), evenement.GetType()), static cle => Trouver(cle.Processus, cle.Evenement))
            ?? throw new ProcessusException(
                $"Le processus '{processus.GetType().Name}' n'a pas de méthode Handle({evenement.GetType().Name}).");

        var retour = methode.Invoke(processus, BindingFlags.DoNotWrapExceptions, null, [evenement], null);
        if (retour is Task tache)
            await tache;
        else if (retour is ValueTask valueTask)
            await valueTask;
    }

    private static MethodInfo? Trouver(Type typeProcessus, Type typeEvenement)
    {
        for (var type = typeProcessus; type is not null; type = type.BaseType)
            foreach (var methode in type.GetMethods(Recherche))
                if (methode.Name == "Handle" && methode.GetParameters() is [var parametre] && parametre.ParameterType == typeEvenement)
                    return methode;
        return null;
    }
}
