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

/// <summary>Point d'entrée unique du processus.</summary>
public sealed class NoeudDebut : Noeud
{
    /// <inheritdoc />
    [JsonIgnore]
    public override TypeNoeud Type => TypeNoeud.Debut;
}

/// <summary>Exécute une commande obligatoire.</summary>
public sealed class NoeudSystemique : Noeud
{
    /// <inheritdoc />
    [JsonIgnore]
    public override TypeNoeud Type => TypeNoeud.Systemique;

    /// <summary>Commande exécutée.</summary>
    public required AppelCommande Commande { get; init; }
}

/// <summary>
/// Suspend l'instance jusqu'au premier de ses déclencheurs (une échéance et/ou des signaux).
/// Chaque déclencheur a sa propre sortie.
/// </summary>
public sealed class NoeudAttente : Noeud
{
    /// <inheritdoc />
    [JsonIgnore]
    public override TypeNoeud Type => TypeNoeud.Attente;

    /// <inheritdoc />
    [JsonIgnore]
    public override bool EstSuspensif => true;

    /// <summary>Commandes exécutées à l'entrée, avant l'enregistrement des déclencheurs.</summary>
    public IReadOnlyList<AppelCommande> CommandesEntree { get; init; } = [];

    /// <summary>Déclencheurs (au moins un, au plus une échéance).</summary>
    public IReadOnlyList<Declencheur> Declencheurs { get; init; } = [];
}

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

/// <summary>
/// Passerelle exclusive : évalue dans l'ordre la requête de chaque branche sortante et prend
/// la première vraie, sinon la branche « sinon ». Les branches sont portées par les transitions.
/// </summary>
public sealed class NoeudDecision : Noeud
{
    /// <inheritdoc />
    [JsonIgnore]
    public override TypeNoeud Type => TypeNoeud.Decision;
}

/// <summary>Démarre une instance enfant dans la même transaction.</summary>
public sealed class NoeudSousProcessus : Noeud
{
    /// <summary>Nom de la sortie suivie lorsque l'enfant est annulé.</summary>
    public const string SortieAnnule = "annule";

    /// <inheritdoc />
    [JsonIgnore]
    public override TypeNoeud Type => TypeNoeud.SousProcessus;

    /// <inheritdoc />
    [JsonIgnore]
    public override bool EstSuspensif => true;

    /// <summary>Clé du processus enfant (sa dernière version active est utilisée).</summary>
    public required string CleProcessus { get; init; }

    /// <summary>Variable entière du parent donnant la clé d'affaire de l'enfant ; par défaut, celle du parent.</summary>
    public string? VariableCleAffaire { get; init; }

    /// <summary>Mappage d'entrée : chaque paramètre remplit la variable de l'enfant du même nom.</summary>
    public IReadOnlyList<Parametre> Entrees { get; init; } = [];

    /// <summary>Mappage de sortie, appliqué à la fin normale de l'enfant.</summary>
    public IReadOnlyList<MappageSortie> Sorties { get; init; } = [];
}

/// <summary>Termine l'instance. Le code du nœud est le code de fin.</summary>
public sealed class NoeudFin : Noeud
{
    /// <inheritdoc />
    [JsonIgnore]
    public override TypeNoeud Type => TypeNoeud.Fin;
}

/// <summary>Déclencheur d'un nœud d'attente.</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(DeclencheurEcheance), "ECHEANCE")]
[JsonDerivedType(typeof(DeclencheurSignal), "SIGNAL")]
public abstract class Declencheur
{
    /// <summary>Nom de la sortie suivie si ce déclencheur gagne.</summary>
    public required string NomSortie { get; init; }

    /// <summary>Type du déclencheur.</summary>
    [JsonIgnore]
    public abstract TypeDeclencheur Type { get; }
}

/// <summary>Échéance : date absolue, durée relative à l'entrée dans le nœud, ou date lue dans une variable.</summary>
public sealed class DeclencheurEcheance : Declencheur
{
    /// <summary>Nom de sortie par défaut d'une échéance.</summary>
    public const string SortieParDefaut = "echeance";

    /// <inheritdoc />
    [JsonIgnore]
    public override TypeDeclencheur Type => TypeDeclencheur.Echeance;

    /// <summary>Date absolue (UTC).</summary>
    public DateTime? DateAbsolue { get; init; }

    /// <summary>Durée relative à l'entrée dans le nœud.</summary>
    public TimeSpan? Duree { get; init; }

    /// <summary>Variable (date) donnant l'échéance.</summary>
    public string? Variable { get; init; }
}

/// <summary>Signal nommé, avec clé de corrélation optionnelle (fixe ou tirée d'une variable).</summary>
public sealed class DeclencheurSignal : Declencheur
{
    /// <inheritdoc />
    [JsonIgnore]
    public override TypeDeclencheur Type => TypeDeclencheur.Signal;

    /// <summary>Nom du signal attendu.</summary>
    public required string NomSignal { get; init; }

    /// <summary>Clé de corrélation fixe.</summary>
    public string? CleCorrelation { get; init; }

    /// <summary>Variable donnant la clé de corrélation.</summary>
    public string? VariableCorrelation { get; init; }

    /// <summary>Fusionne la charge utile du signal dans les variables (vrai par défaut).</summary>
    public bool FusionnerChargeUtile { get; init; } = true;
}

/// <summary>Lien orienté d'un nœud vers un autre, créé par le constructeur.</summary>
public sealed class Transition
{
    /// <summary>Code du nœud source.</summary>
    public required string Source { get; init; }

    /// <summary>Code du nœud destination.</summary>
    public required string Destination { get; init; }

    /// <summary>Nom de la sortie (branche, déclencheur, code de fin) ; nul pour la sortie par défaut.</summary>
    public string? NomSortie { get; init; }

    /// <summary>Requête d'une branche conditionnelle de décision.</summary>
    public AppelRequete? Requete { get; init; }

    /// <summary>Vrai pour la branche « sinon » d'une décision.</summary>
    public bool EstSinon { get; init; }
}
