namespace Exemples.DeclarationSousProcessus;

/// <summary>Système de vérification : reçoit les critères de sélection et répond plus tard par le signal RETOUR_VERIFICATION.</summary>
public interface IServiceVerification
{
    Task EtablirCriteresSelectionAsync(long numeroDeclaration, CancellationToken ct);
}
