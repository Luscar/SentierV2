namespace Sentier.Migration;

/// <summary>Traitement des attentes d'une instance migrée.</summary>
public enum ActionAttentes
{
    /// <summary>Conserver les attentes en cours (renommées si le code du nœud change).</summary>
    Conserver,

    /// <summary>Supprimer les attentes et les recréer selon le nœud cible (sans commandes d'entrée).</summary>
    Recreer,

    /// <summary>Supprimer les attentes ; l'instance reprend au nœud de suite.</summary>
    Annuler,
}
