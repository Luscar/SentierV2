namespace Exemples.DeclarationSousProcessus;

public sealed class EstIdentifiee(IDossiersDeclarations dossiers) : RequeteEtatDeclaration(dossiers)
{
    protected override bool Evaluer(EtatDeclaration etat) => etat.CodeRejetIdentification is null;
}
