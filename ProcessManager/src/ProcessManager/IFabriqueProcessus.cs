namespace ProcessManager;

/// <summary>Crée un processus à partir de son type (typiquement via le conteneur DI).</summary>
public interface IFabriqueProcessus
{
    ProcessusBase Creer(Type typeProcessus);
}
