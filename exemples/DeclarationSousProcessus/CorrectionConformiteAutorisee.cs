namespace Exemples.DeclarationSousProcessus;

public sealed class CorrectionConformiteAutorisee(IDossiersDeclarations dossiers) : RequeteEtatDeclaration(dossiers)
{
    protected override bool Evaluer(EtatDeclaration etat) => etat.CorrectionConformiteAutorisee;
}
