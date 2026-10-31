namespace Exemples.DeclarationSousProcessus;

// Services métier de l'application, appelés par les commandes et les requêtes.
// Ils travaillent dans la même transaction que Sentier et tiennent l'état de la déclaration.

public sealed record Declaration(long Numero, string NumeroPeriode, string Type, decimal Montant);

/// <summary>Résultats des étapes : un code de rejet vide signifie « accepté ».</summary>
public sealed record EtatDeclaration(
    string? CodeRejetIdentification,
    string? CodeRejetConformite,
    bool CorrectionConformiteAutorisee,
    string? CodeRejetComptable);
