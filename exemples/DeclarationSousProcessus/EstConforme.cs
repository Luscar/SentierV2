namespace Exemples.DeclarationSousProcessus;

public sealed class EstConforme(IDossiersDeclarations dossiers) : RequeteEtatDeclaration(dossiers)
{
    protected override bool Evaluer(EtatDeclaration etat) => etat.CodeRejetConformite is null;
}
