using System.Text.Json.Serialization;

namespace Sentier.Definitions;

/// <summary>Nœud typé d'une définition, identifié par un code texte unique et stable.</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(NoeudDebut), "DEBUT")]
[JsonDerivedType(typeof(NoeudSystemique), "SYSTEMIQUE")]
[JsonDerivedType(typeof(NoeudAttente), "ATTENTE")]
[JsonDerivedType(typeof(NoeudInteractif), "INTERACTIF")]
[JsonDerivedType(typeof(NoeudDecision), "DECISION")]
[JsonDerivedType(typeof(NoeudSousProcessus), "SOUS_PROCESSUS")]
[JsonDerivedType(typeof(NoeudFin), "FIN")]
public abstract class Noeud
{
    /// <summary>Code unique du nœud dans la définition. C'est un contrat avec les instances en cours.</summary>
    public required string Code { get; init; }

    /// <summary>Libellé lisible, optionnel.</summary>
    [JsonInclude]
    public string? Nom { get; internal set; }

    /// <summary>Type du nœud.</summary>
    [JsonIgnore]
    public abstract TypeNoeud Type { get; }

    /// <summary>Indique si le nœud suspend l'instance (attente, interactif, sous-processus).</summary>
    [JsonIgnore]
    public virtual bool EstSuspensif => false;

    /// <inheritdoc />
    public override string ToString() => $"{Type} '{Code}'";
}
