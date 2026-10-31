namespace Sentier.Execution.Executeurs;

/// <summary>Début : continue immédiatement.</summary>
internal sealed class ExecuteurDebut : ExecuteurNonSuspensif
{
    public override TypeNoeud Type => TypeNoeud.Debut;

    public override Task<ResultatNoeud> EntrerAsync(ContexteNoeud contexte, CancellationToken ct)
        => Task.FromResult(ResultatNoeud.Continuer());
}
