namespace Sentier.Execution;

/// <summary>Le nœud est terminé et l'exécution continue.</summary>
public sealed record ResultatContinuer(string? NomSortie, string? Resultat) : ResultatNoeud;
