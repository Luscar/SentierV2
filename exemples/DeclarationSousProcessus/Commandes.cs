using Sentier.Commandes;

namespace Exemples.DeclarationSousProcessus;

// Commandes : elles n'ont besoin que de la clé d'affaire, le numéro de la déclaration.

public sealed class IdentifierDeclaration(IServiceIdentification identification) : ICommandeSentier
{
    public Task ExecuterAsync(ContexteCommande contexte, CancellationToken ct)
        => identification.IdentifierAsync(contexte.CleAffaire, ct);
}

public sealed class ValiderConformite(IServiceConformite conformite) : ICommandeSentier
{
    public Task ExecuterAsync(ContexteCommande contexte, CancellationToken ct)
        => conformite.ValiderAsync(contexte.CleAffaire, ct);
}

public sealed class EtablirCriteresSelection(IServiceVerification verification) : ICommandeSentier
{
    public Task ExecuterAsync(ContexteCommande contexte, CancellationToken ct)
        => verification.EtablirCriteresSelectionAsync(contexte.CleAffaire, ct);
}

public sealed class Comptabiliser(IServiceComptable comptable) : ICommandeSentier
{
    public Task ExecuterAsync(ContexteCommande contexte, CancellationToken ct)
        => comptable.ComptabiliserAsync(contexte.CleAffaire, ct);
}

// Requêtes : lecture seule de l'état de la déclaration.

public abstract class RequeteEtatDeclaration(IDossiersDeclarations dossiers) : IRequeteSentier
{
    public async Task<bool> EvaluerAsync(ContexteRequete contexte, CancellationToken ct)
        => Evaluer(await dossiers.ObtenirEtatAsync(contexte.CleAffaire, ct));

    protected abstract bool Evaluer(EtatDeclaration etat);
}

public sealed class EstIdentifiee(IDossiersDeclarations dossiers) : RequeteEtatDeclaration(dossiers)
{
    protected override bool Evaluer(EtatDeclaration etat) => etat.CodeRejetIdentification is null;
}

public sealed class EstConforme(IDossiersDeclarations dossiers) : RequeteEtatDeclaration(dossiers)
{
    protected override bool Evaluer(EtatDeclaration etat) => etat.CodeRejetConformite is null;
}

public sealed class CorrectionConformiteAutorisee(IDossiersDeclarations dossiers) : RequeteEtatDeclaration(dossiers)
{
    protected override bool Evaluer(EtatDeclaration etat) => etat.CorrectionConformiteAutorisee;
}

public sealed class EstRejeteeComptabilite(IDossiersDeclarations dossiers) : RequeteEtatDeclaration(dossiers)
{
    protected override bool Evaluer(EtatDeclaration etat) => etat.CodeRejetComptable is not null;
}
