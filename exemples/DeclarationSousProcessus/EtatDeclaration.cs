namespace Exemples.DeclarationSousProcessus;

/// <summary>Résultats des étapes : un code de rejet vide signifie « accepté ».</summary>
public sealed record EtatDeclaration(
    string? CodeRejetIdentification,
    string? CodeRejetConformite,
    bool CorrectionConformiteAutorisee,
    string? CodeRejetComptable);
