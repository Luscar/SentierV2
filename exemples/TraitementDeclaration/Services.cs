namespace Exemples.TraitementDeclaration;

// Services métier de l'application, appelés par les commandes du processus.
// Ils travaillent dans la même transaction que Sentier.

/// <summary>Dossiers des déclarations reçues.</summary>
public interface IServiceDeclarations
{
    Task EnregistrerReceptionAsync(long numeroDeclaration, string numeroPeriode, string typeDeclaration, decimal montantDeclare, CancellationToken ct);
}

/// <summary>Identification du déclarant.</summary>
public interface IServiceIdentification
{
    /// <summary>Identifie le déclarant ; retourne son numéro, ou un code de rejet.</summary>
    Task<ResultatIdentification> IdentifierAsync(long numeroDeclaration, string numeroPeriode, CancellationToken ct);
}

public sealed record ResultatIdentification(long? NoIdentification, string? CodeRejet);

/// <summary>Règles de conformité d'une déclaration.</summary>
public interface IServiceConformite
{
    /// <summary>Valide la déclaration ; retourne un code de rejet, ou <c>null</c> si elle est conforme.</summary>
    Task<string?> ValiderAsync(long numeroDeclaration, long noIdentification, string typeDeclaration, decimal montantDeclare, decimal toleranceEcart, CancellationToken ct);
}

/// <summary>Calendrier des traitements comptables.</summary>
public interface ICalendrierComptable
{
    /// <summary>Prochaine date (UTC) de comptabilisation ouverte pour la période, à partir de <paramref name="apres"/>.</summary>
    Task<DateTime> ProchaineDateComptabilisationAsync(string numeroPeriode, DateTime apres, CancellationToken ct);
}

/// <summary>Système comptable : reçoit les écritures et répond plus tard par le signal RETOUR_COMPTABLE.</summary>
public interface IServiceComptable
{
    /// <summary>Transmet les écritures et retourne le numéro du lot comptable.</summary>
    Task<string> TransmettreAsync(long numeroDeclaration, long noIdentification, string numeroPeriode, decimal montant, CancellationToken ct);
}
