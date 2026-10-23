using Sentier;
using Sentier.Commandes;

namespace Exemples.TraitementDeclaration;

using static TraitementDeclaration;

// Commandes : chacune reçoit ses paramètres typés (liés par nom) et écrit ses résultats dans les variables.

public sealed class ParametresReception
{
    public long NumeroDeclaration { get; set; }
    public string NumeroPeriode { get; set; } = "";
    public string TypeDeclaration { get; set; } = "";
    public decimal MontantDeclare { get; set; }
}

public sealed class RecevoirDeclaration(IServiceDeclarations declarations) : CommandeSentier<ParametresReception>
{
    protected override Task ExecuterAsync(ContexteCommande contexte, ParametresReception p, CancellationToken ct)
        => declarations.EnregistrerReceptionAsync(p.NumeroDeclaration, p.NumeroPeriode, p.TypeDeclaration, p.MontantDeclare, ct);
}

public sealed class ParametresIdentification
{
    public long NumeroDeclaration { get; set; }
    public string NumeroPeriode { get; set; } = "";
}

public sealed class IdentifierDeclarant(IServiceIdentification identification) : CommandeSentier<ParametresIdentification>
{
    protected override async Task ExecuterAsync(ContexteCommande contexte, ParametresIdentification p, CancellationToken ct)
    {
        var resultat = await identification.IdentifierAsync(p.NumeroDeclaration, p.NumeroPeriode, ct);
        if (resultat.NoIdentification is { } numero)
        {
            contexte.Variables.Ecrire(NoIdentification, numero);
        }

        contexte.Variables.Ecrire(CodeRejetIdentification.Nom, resultat.CodeRejet);
    }
}

public sealed class ParametresConformite
{
    public long NumeroDeclaration { get; set; }
    public long NoIdentification { get; set; }
    public string TypeDeclaration { get; set; } = "";
    public decimal MontantDeclare { get; set; }
    public decimal ToleranceEcart { get; set; }
}

public sealed class ValiderConformite(IServiceConformite conformite) : CommandeSentier<ParametresConformite>
{
    protected override async Task ExecuterAsync(ContexteCommande contexte, ParametresConformite p, CancellationToken ct)
    {
        var codeRejet = await conformite.ValiderAsync(p.NumeroDeclaration, p.NoIdentification, p.TypeDeclaration, p.MontantDeclare, p.ToleranceEcart, ct);
        contexte.Variables.Ecrire(CodeRejetConformite.Nom, codeRejet);

        // Chaque nouveau rejet demande une nouvelle autorisation.
        contexte.Variables.Ecrire(CorrectionAutorisee, false);
    }
}

public sealed class ParametresCalendrier
{
    public string NumeroPeriode { get; set; } = "";
}

/// <summary>Commande d'entrée de l'attente : fixe l'échéance sur la prochaine date du calendrier comptable.</summary>
public sealed class CalculerDateComptabilisation(ICalendrierComptable calendrier, IHorloge horloge) : CommandeSentier<ParametresCalendrier>
{
    protected override async Task ExecuterAsync(ContexteCommande contexte, ParametresCalendrier p, CancellationToken ct)
        => contexte.Variables.Ecrire(DateComptabilisation, await calendrier.ProchaineDateComptabilisationAsync(p.NumeroPeriode, horloge.MaintenantUtc, ct));
}

public sealed class ParametresComptabilisation
{
    public long NumeroDeclaration { get; set; }
    public long NoIdentification { get; set; }
    public string NumeroPeriode { get; set; } = "";
    public decimal MontantDeclare { get; set; }
}

public sealed class Comptabiliser(IServiceComptable comptable) : CommandeSentier<ParametresComptabilisation>
{
    protected override async Task ExecuterAsync(ContexteCommande contexte, ParametresComptabilisation p, CancellationToken ct)
    {
        var noLot = await comptable.TransmettreAsync(p.NumeroDeclaration, p.NoIdentification, p.NumeroPeriode, p.MontantDeclare, ct);

        // Le signal de retour est corrélé à ce lot ; le rejet d'un lot précédent ne compte plus.
        contexte.Variables.Ecrire(NoLotComptable, noLot);
        contexte.Variables.Effacer(CodeRejetComptable.Nom);
    }
}

// Requêtes : lecture seule.

public sealed class ParametresRejet
{
    public string? CodeRejet { get; set; }
}

public sealed class EstSansRejet : RequeteSentier<ParametresRejet>
{
    protected override Task<bool> EvaluerAsync(ContexteRequete contexte, ParametresRejet p, CancellationToken ct)
        => Task.FromResult(string.IsNullOrEmpty(p.CodeRejet));
}

public sealed class ParametresBooleen
{
    public bool Valeur { get; set; }
}

public sealed class EstVrai : RequeteSentier<ParametresBooleen>
{
    protected override Task<bool> EvaluerAsync(ContexteRequete contexte, ParametresBooleen p, CancellationToken ct)
        => Task.FromResult(p.Valeur);
}
