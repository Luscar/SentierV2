namespace Sentier;

/// <summary>Tâche utilisateur telle que la voit Sentier, fournie par le <see cref="IGestionnaireTaches"/> du client.</summary>
public sealed class TacheSentier
{
    /// <summary>Identifiant de la tâche chez le client.</summary>
    public required long Id { get; init; }

    /// <summary>Instance à l'origine de la tâche.</summary>
    public required IdInstance IdInstance { get; init; }

    /// <summary>Code du nœud interactif.</summary>
    public required string CodeNoeud { get; init; }

    /// <summary>Statut de la tâche.</summary>
    public required StatutTacheSentier Statut { get; init; }
}
