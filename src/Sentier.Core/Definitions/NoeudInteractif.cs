using System.Text.Json.Serialization;

namespace Sentier.Definitions;

/// <summary>Crée une tâche dans le bassin du client et suspend l'instance jusqu'à sa complétion.</summary>
public sealed class NoeudInteractif : Noeud
{
    /// <inheritdoc />
    [JsonIgnore]
    public override TypeNoeud Type => TypeNoeud.Interactif;

    /// <inheritdoc />
    [JsonIgnore]
    public override bool EstSuspensif => true;

    /// <summary>Nom de la tâche créée.</summary>
    public required string NomTache { get; init; }

    /// <summary>Rôle assigné, optionnel.</summary>
    public string? Role { get; init; }

    /// <summary>Utilisateur assigné, fixe ou tiré d'une variable.</summary>
    public Parametre? Assigne { get; init; }

    /// <summary>Durée indicative avant l'échéance de la tâche.</summary>
    public TimeSpan? DureeEcheance { get; init; }

    /// <summary>Variable (date) donnant l'échéance indicative de la tâche.</summary>
    public string? VariableEcheance { get; init; }

    /// <summary>Données transmises au bassin de tâches.</summary>
    public IReadOnlyList<Parametre> Donnees { get; init; } = [];

    /// <summary>Commandes exécutées à l'entrée, avant la création de la tâche.</summary>
    public IReadOnlyList<AppelCommande> CommandesEntree { get; init; } = [];

    /// <summary>Commandes exécutées à la sortie, après la fusion des données de la tâche.</summary>
    public IReadOnlyList<AppelCommande> CommandesSortie { get; init; } = [];
}
