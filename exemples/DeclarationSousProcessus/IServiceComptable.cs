namespace Exemples.DeclarationSousProcessus;

/// <summary>Système comptable : reçoit les écritures et répond plus tard par le signal RETOUR_COMPTABLE.</summary>
public interface IServiceComptable
{
    Task ComptabiliserAsync(long numeroDeclaration, CancellationToken ct);
}
