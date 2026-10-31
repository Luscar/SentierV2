namespace Sentier;

/// <summary>Heure courante, injectable. Toutes les dates de Sentier sont en UTC.</summary>
public interface IHorlogeSentier
{
    /// <summary>Date et heure courantes, en UTC.</summary>
    DateTime MaintenantUtc { get; }
}
