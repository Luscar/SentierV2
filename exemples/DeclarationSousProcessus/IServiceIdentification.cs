namespace Exemples.DeclarationSousProcessus;

/// <summary>Identification du déclarant ; retient le numéro d'identification ou un code de rejet.</summary>
public interface IServiceIdentification
{
    Task IdentifierAsync(long numeroDeclaration, CancellationToken ct);
}
