using Sentier.Definitions;

namespace Sentier.Execution.Executeurs;

/// <summary>Classe de base des exécuteurs de nœuds non suspensifs.</summary>
internal abstract class ExecuteurNonSuspensif : IExecuteurNoeud
{
    public abstract TypeNoeud Type { get; }

    public abstract Task<ResultatNoeud> EntrerAsync(ContexteNoeud contexte, CancellationToken ct);

    public Task<ResultatNoeud> ReprendreAsync(ContexteNoeud contexte, EvenementReprise evenement, CancellationToken ct)
        => throw new SentierException($"Le nœud '{contexte.Noeud.Code}' ({Type}) ne peut pas être repris : il n'est pas suspensif.");
}
