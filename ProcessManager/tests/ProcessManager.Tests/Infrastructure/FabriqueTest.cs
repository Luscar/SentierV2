using ProcessManager;

namespace ProcessManager.Tests.Infrastructure;

/// <summary>Fabrique minimale jouant le rôle du conteneur DI.</summary>
public sealed class FabriqueTest : IFabriqueProcessus
{
    private readonly Dictionary<Type, Func<ProcessusBase>> _fabriques = new();

    public FabriqueTest Enregistrer<TProcessus>(Func<TProcessus> fabrique) where TProcessus : ProcessusBase
    {
        _fabriques[typeof(TProcessus)] = fabrique;
        return this;
    }

    public ProcessusBase Creer(Type typeProcessus) => _fabriques[typeProcessus]();
}
