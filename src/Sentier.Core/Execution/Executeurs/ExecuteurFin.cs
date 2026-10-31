using Sentier.Definitions;

namespace Sentier.Execution.Executeurs;

/// <summary>Fin : termine l'instance avec le code du nœud.</summary>
internal sealed class ExecuteurFin : ExecuteurNonSuspensif
{
    public override TypeNoeud Type => TypeNoeud.Fin;

    public override Task<ResultatNoeud> EntrerAsync(ContexteNoeud contexte, CancellationToken ct)
        => Task.FromResult(ResultatNoeud.Terminer(contexte.Noeud.Code));
}
