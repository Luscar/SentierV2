namespace Exemples.DeclarationSousProcessus;

// Services métier de l'application, appelés par les commandes et les requêtes.
// Ils travaillent dans la même transaction que Sentier et tiennent l'état de la déclaration.

/// <summary>Dossiers des déclarations reçues.</summary>
public interface IDossiersDeclarations
{
    /// <summary>Enregistre une déclaration reçue ; le traitement est démarré ensuite, dans la même transaction.</summary>
    Task EnregistrerAsync(Declaration declaration, CancellationToken ct);

    /// <summary>État courant de la déclaration, lu par les décisions des processus.</summary>
    Task<EtatDeclaration> ObtenirEtatAsync(long numeroDeclaration, CancellationToken ct);
}

public sealed record Declaration(long Numero, string NumeroPeriode, string Type, decimal Montant);

/// <summary>Résultats des étapes : un code de rejet vide signifie « accepté ».</summary>
public sealed record EtatDeclaration(
    string? CodeRejetIdentification,
    string? CodeRejetConformite,
    bool CorrectionConformiteAutorisee,
    string? CodeRejetComptable);

/// <summary>Identification du déclarant ; retient le numéro d'identification ou un code de rejet.</summary>
public interface IServiceIdentification
{
    Task IdentifierAsync(long numeroDeclaration, CancellationToken ct);
}

/// <summary>Règles de conformité ; retient un code de rejet et remet à zéro l'autorisation de correction.</summary>
public interface IServiceConformite
{
    Task ValiderAsync(long numeroDeclaration, CancellationToken ct);
}

/// <summary>Système de vérification : reçoit les critères de sélection et répond plus tard par le signal RETOUR_VERIFICATION.</summary>
public interface IServiceVerification
{
    Task EtablirCriteresSelectionAsync(long numeroDeclaration, CancellationToken ct);
}

/// <summary>Système comptable : reçoit les écritures et répond plus tard par le signal RETOUR_COMPTABLE.</summary>
public interface IServiceComptable
{
    Task ComptabiliserAsync(long numeroDeclaration, CancellationToken ct);
}
