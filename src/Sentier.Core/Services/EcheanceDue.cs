namespace Sentier;

/// <summary>Échéance atteinte, à traiter par le batch externe.</summary>
public sealed record EcheanceDue(IdAttente IdAttente, IdInstance IdInstance, string CleProcessus, long CleAffaire, DateTime DateEcheance);
