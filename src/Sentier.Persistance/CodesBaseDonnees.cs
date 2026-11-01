namespace Sentier.Persistance;

/// <summary>Codes stockés en base pour les énumérations de Sentier.</summary>
public static class CodesBaseDonnees
{
    /// <summary>Liste SQL des statuts actifs.</summary>
    public const string StatutsActifs = "('EN_COURS', 'SUSPENDUE', 'EN_ERREUR')";

    /// <summary>Liste SQL des statuts au repos.</summary>
    public const string StatutsAuRepos = "('SUSPENDUE', 'EN_ERREUR')";

    /// <summary>Code d'un statut d'instance.</summary>
    public static string Code(StatutInstance statut) => statut switch
    {
        StatutInstance.EnCours => "EN_COURS",
        StatutInstance.Suspendue => "SUSPENDUE",
        StatutInstance.Terminee => "TERMINEE",
        StatutInstance.Annulee => "ANNULEE",
        StatutInstance.EnErreur => "EN_ERREUR",
        _ => throw new ArgumentOutOfRangeException(nameof(statut)),
    };

    /// <summary>Statut d'instance d'un code.</summary>
    public static StatutInstance LireStatutInstance(string code) => code switch
    {
        "EN_COURS" => StatutInstance.EnCours,
        "SUSPENDUE" => StatutInstance.Suspendue,
        "TERMINEE" => StatutInstance.Terminee,
        "ANNULEE" => StatutInstance.Annulee,
        "EN_ERREUR" => StatutInstance.EnErreur,
        _ => throw new SentierException($"Statut d'instance inconnu en base : '{code}'."),
    };

    /// <summary>Code d'un type de nœud (<c>TYP_NOEUD</c>).</summary>
    public static string Code(TypeNoeud type) => type switch
    {
        TypeNoeud.Debut => "DEBUT",
        TypeNoeud.Systemique => "SYSTEMIQUE",
        TypeNoeud.Attente => "ATTENTE",
        TypeNoeud.Interactif => "INTERACTIF",
        TypeNoeud.Decision => "DECISION",
        TypeNoeud.SousProcessus => "SOUS_PROCESSUS",
        TypeNoeud.Fin => "FIN",
        _ => throw new ArgumentOutOfRangeException(nameof(type)),
    };

    /// <summary>Type de nœud d'un code.</summary>
    public static TypeNoeud LireTypeNoeud(string code) => code switch
    {
        "DEBUT" => TypeNoeud.Debut,
        "SYSTEMIQUE" => TypeNoeud.Systemique,
        "ATTENTE" => TypeNoeud.Attente,
        "INTERACTIF" => TypeNoeud.Interactif,
        "DECISION" => TypeNoeud.Decision,
        "SOUS_PROCESSUS" => TypeNoeud.SousProcessus,
        "FIN" => TypeNoeud.Fin,
        _ => throw new SentierException($"Type de nœud inconnu en base : '{code}'."),
    };

    /// <summary>Code d'un type de déclencheur.</summary>
    public static string Code(TypeDeclencheur type) => type == TypeDeclencheur.Echeance ? "ECHEANCE" : "SIGNAL";

    /// <summary>Type de déclencheur d'un code.</summary>
    public static TypeDeclencheur LireTypeDeclencheur(string code) => code switch
    {
        "ECHEANCE" => TypeDeclencheur.Echeance,
        "SIGNAL" => TypeDeclencheur.Signal,
        _ => throw new SentierException($"Type de déclencheur inconnu en base : '{code}'."),
    };

    /// <summary>Code d'un mode de migration.</summary>
    public static string Code(ModeMigration mode) => mode == ModeMigration.Auto ? "AUTO" : "PLAN";
}
