namespace Sentier;

/// <summary>Nombre d'instances actives d'un processus pour une version et un nœud.</summary>
public sealed record CompteInstancesActives(IdDefinition IdDefinition, int Version, string CodeNoeud, int Nombre);
