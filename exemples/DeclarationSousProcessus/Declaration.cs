namespace Exemples.DeclarationSousProcessus;

// Services métier de l'application, appelés par les commandes et les requêtes.
// Ils travaillent dans la même transaction que Sentier et tiennent l'état de la déclaration.

public sealed record Declaration(long Numero, string NumeroPeriode, string Type, decimal Montant);
