namespace Exemples.DeclarationSousProcessus;

/// <summary>Règles de conformité ; retient un code de rejet et remet à zéro l'autorisation de correction.</summary>
public interface IServiceConformite
{
    Task ValiderAsync(long numeroDeclaration, CancellationToken ct);
}
