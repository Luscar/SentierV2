namespace Exemples.DeclarationSousProcessus;

/// <summary>
/// Services simulés pour la démonstration : un dossier en mémoire par déclaration.
/// Les méthodes publiques hors interfaces jouent les écrans de l'application et les retours des systèmes externes.
/// </summary>
public sealed class Simulateur : IDossiersDeclarations, IServiceIdentification, IServiceConformite, IServiceVerification, IServiceComptable
{
    private readonly Dictionary<long, Dossier> _dossiers = [];
    private int _identifications;
    private int _lots;

    /// <summary>Nombre d'identifications rejetées avant la première acceptée.</summary>
    public int RejetsIdentification { get; set; } = 1;

    /// <summary>Montant attendu par la conformité ; tout autre montant est rejeté.</summary>
    public decimal MontantAttendu { get; set; }

    public Task EnregistrerAsync(Declaration declaration, CancellationToken ct)
    {
        _dossiers[declaration.Numero] = new Dossier { Declaration = declaration };
        Journal.Ecrire($"Déclaration {declaration.Numero} enregistrée ({declaration.Type}, période {declaration.NumeroPeriode}, {declaration.Montant:N2} $)");
        return Task.CompletedTask;
    }

    public Task<EtatDeclaration> ObtenirEtatAsync(long numeroDeclaration, CancellationToken ct)
    {
        var d = _dossiers[numeroDeclaration];
        return Task.FromResult(new EtatDeclaration(d.CodeRejetIdentification, d.CodeRejetConformite, d.CorrectionConformiteAutorisee, d.CodeRejetComptable));
    }

    public Task IdentifierAsync(long numeroDeclaration, CancellationToken ct)
    {
        var d = _dossiers[numeroDeclaration];
        d.CodeRejetIdentification = _identifications++ < RejetsIdentification ? "ID-01" : null;
        Journal.Ecrire(d.CodeRejetIdentification is null ? $"Identification : déclarant {700_000 + numeroDeclaration}" : $"Identification rejetée : {d.CodeRejetIdentification}");
        return Task.CompletedTask;
    }

    public Task ValiderAsync(long numeroDeclaration, CancellationToken ct)
    {
        var d = _dossiers[numeroDeclaration];
        d.CodeRejetConformite = d.Declaration.Montant != MontantAttendu ? "CF-12" : null;

        // Chaque nouveau rejet demande une nouvelle autorisation.
        d.CorrectionConformiteAutorisee = false;
        Journal.Ecrire(d.CodeRejetConformite is null ? "Conformité : déclaration conforme" : $"Conformité rejetée : {d.CodeRejetConformite} (écart sur {d.Declaration.Montant:N2} $)");
        return Task.CompletedTask;
    }

    public Task EtablirCriteresSelectionAsync(long numeroDeclaration, CancellationToken ct)
    {
        Journal.Ecrire("Vérification : critères de sélection transmis au système de vérification");
        return Task.CompletedTask;
    }

    public Task ComptabiliserAsync(long numeroDeclaration, CancellationToken ct)
    {
        var d = _dossiers[numeroDeclaration];
        d.CodeRejetComptable = null;
        Journal.Ecrire($"Comptabilisation : écritures transmises, lot LOT-{numeroDeclaration}-{++_lots}");
        return Task.CompletedTask;
    }

    // Écrans de l'application.

    public void CorrigerMontant(long numeroDeclaration, decimal montant)
    {
        var d = _dossiers[numeroDeclaration];
        d.Declaration = d.Declaration with { Montant = montant };
        Journal.Ecrire($"Écran : montant corrigé à {montant:N2} $");
    }

    public void DeciderAutorisation(long numeroDeclaration, bool autorisee)
    {
        _dossiers[numeroDeclaration].CorrectionConformiteAutorisee = autorisee;
        Journal.Ecrire($"Écran : correction {(autorisee ? "autorisée" : "refusée")}");
    }

    // Retours des systèmes externes, reçus par l'application avant l'envoi du signal.

    public void RecevoirRetourComptable(long numeroDeclaration, string? codeRejet)
    {
        _dossiers[numeroDeclaration].CodeRejetComptable = codeRejet;
        Journal.Ecrire(codeRejet is null ? "Retour comptable : accepté" : $"Retour comptable : rejet {codeRejet}");
    }

    private sealed class Dossier
    {
        public required Declaration Declaration { get; set; }
        public string? CodeRejetIdentification { get; set; }
        public string? CodeRejetConformite { get; set; }
        public bool CorrectionConformiteAutorisee { get; set; }
        public string? CodeRejetComptable { get; set; }
    }
}
