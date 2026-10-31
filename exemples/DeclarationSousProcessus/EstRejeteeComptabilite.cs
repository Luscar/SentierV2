namespace Exemples.DeclarationSousProcessus;

public sealed class EstRejeteeComptabilite(IDossiersDeclarations dossiers) : RequeteEtatDeclaration(dossiers)
{
    protected override bool Evaluer(EtatDeclaration etat) => etat.CodeRejetComptable is not null;
}
