namespace Exemples.DeclarationSousProcessus;

/// <summary>Dossiers des déclarations reçues.</summary>
public interface IDossiersDeclarations
{
    /// <summary>Enregistre une déclaration reçue ; le traitement est démarré ensuite, dans la même transaction.</summary>
    Task EnregistrerAsync(Declaration declaration, CancellationToken ct);

    /// <summary>État courant de la déclaration, lu par les décisions des processus.</summary>
    Task<EtatDeclaration> ObtenirEtatAsync(long numeroDeclaration, CancellationToken ct);
}
