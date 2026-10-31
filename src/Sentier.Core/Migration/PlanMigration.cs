using Sentier.Commandes;
using Sentier.Definitions;
using Sentier.Variables;

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

/// <summary>Traitement des tâches ouvertes d'une instance migrée.</summary>
public enum ActionTaches
{
    /// <summary>Conserver les tâches ouvertes (le nœud interactif existe toujours).</summary>
    Conserver,

    /// <summary>Annuler les tâches dans le bassin du client ; l'instance reprend au nœud de suite.</summary>
    Annuler,
}

/// <summary>Règle de migration d'un nœud de la version source.</summary>
public sealed class RegleNoeudMigration
{
    /// <summary>Code du nœud dans la version source.</summary>
    public required string CodeSource { get; init; }

    /// <summary>Code correspondant dans la version cible ; par défaut, le même code.</summary>
    public string? CodeCible { get; internal set; }

    /// <summary>Traitement des attentes ; par défaut, conservées si compatibles, sinon recréées.</summary>
    public ActionAttentes? Attentes { get; internal set; }

    /// <summary>Traitement des tâches ; par défaut, conservées.</summary>
    public ActionTaches? Taches { get; internal set; }

    /// <summary>Nœud de la version cible où l'instance reprend son exécution après la migration.</summary>
    public string? CodeSuite { get; internal set; }
}

/// <summary>Plan de migration compilé.</summary>
public sealed class PlanMigration
{
    internal PlanMigration()
    {
    }

    /// <summary>Règles par code de nœud source.</summary>
    public IReadOnlyDictionary<string, RegleNoeudMigration> Noeuds { get; internal set; } = new Dictionary<string, RegleNoeudMigration>();

    /// <summary>Renommages de variables (ancien nom → nouveau nom).</summary>
    public IReadOnlyDictionary<string, string> RenommagesVariables { get; internal set; } = new Dictionary<string, string>();

    /// <summary>Valeurs statiques pour les nouvelles variables.</summary>
    public IReadOnlyDictionary<string, ValeurTypee> ValeursVariables { get; internal set; } = new Dictionary<string, ValeurTypee>();

    /// <summary>Commandes dédiées qui calculent les nouvelles variables (moment <see cref="MomentCommande.Migration"/>).</summary>
    public IReadOnlyList<AppelCommande> CommandesVariables { get; internal set; } = [];
}

/// <summary>Constructeur fluide d'un plan de migration.</summary>
public sealed class ConstructeurPlanMigration
{
    private readonly Dictionary<string, RegleNoeudMigration> _noeuds = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _renommages = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ValeurTypee> _valeurs = new(StringComparer.Ordinal);
    private readonly List<AppelCommande> _commandes = [];

    /// <summary>Le nœud <paramref name="codeSource"/> correspond au nœud <paramref name="codeCible"/> de la version cible.</summary>
    public ConstructeurPlanMigration Noeud(string codeSource, string codeCible, Action<ConfigurationRegleNoeud>? regle = null)
        => PourNoeud(codeSource, r =>
        {
            r.Vers(codeCible);
            regle?.Invoke(r);
        });

    /// <summary>Règle détaillée pour les instances au repos sur le nœud <paramref name="codeSource"/>.</summary>
    public ConstructeurPlanMigration PourNoeud(string codeSource, Action<ConfigurationRegleNoeud> regle)
    {
        if (!_noeuds.TryGetValue(codeSource, out var existante))
        {
            existante = new RegleNoeudMigration { CodeSource = codeSource };
            _noeuds[codeSource] = existante;
        }

        regle(new ConfigurationRegleNoeud(existante));
        return this;
    }

    /// <summary>Renomme une variable.</summary>
    public ConstructeurPlanMigration RenommerVariable(string ancienNom, string nouveauNom)
    {
        _renommages[ancienNom] = nouveauNom;
        return this;
    }

    /// <summary>Valeur statique d'une nouvelle variable (appliquée si l'instance n'en a pas).</summary>
    public ConstructeurPlanMigration ValeurVariable<T>(Variable<T> variable, T valeur) where T : notnull
    {
        _valeurs[variable.Nom] = ValeurTypee.Depuis(valeur, variable.Type, variable.Nom);
        return this;
    }

    /// <summary>Valeur statique d'une nouvelle variable désignée par son nom.</summary>
    public ConstructeurPlanMigration ValeurVariable(string nom, object valeur)
    {
        _valeurs[nom] = ValeurTypee.Depuis(valeur, nom: nom);
        return this;
    }

    /// <summary>Commande dédiée qui calcule des variables pendant la migration.</summary>
    public ConstructeurPlanMigration CalculerVariables<TCommande>(Action<ConfigurationParametres>? parametres = null) where TCommande : ICommandeSentier
        => CalculerVariables(NomsSentier.NomCommande(typeof(TCommande)), parametres);

    /// <summary>Commande désignée par son nom, qui calcule des variables pendant la migration.</summary>
    public ConstructeurPlanMigration CalculerVariables(string nomCommande, Action<ConfigurationParametres>? parametres = null)
    {
        _commandes.Add(ConfigurationParametres.Appel(nomCommande, parametres));
        return this;
    }

    /// <summary>Valide et compile le plan.</summary>
    /// <exception cref="MigrationInvalideException">Le plan est incohérent.</exception>
    internal PlanMigration Construire(string nomPlan)
    {
        var erreurs = new List<string>();
        foreach (var regle in _noeuds.Values)
        {
            var code = regle.CodeSource;
            if (regle.CodeSuite is null && (regle.Attentes == ActionAttentes.Annuler || regle.Taches == ActionTaches.Annuler))
            {
                erreurs.Add($"Nœud '{code}' : annuler les attentes ou les tâches exige d'indiquer le nœud de suite (PuisReprendreA).");
            }

            if (regle.CodeSuite is not null && (regle.Attentes is ActionAttentes.Conserver or ActionAttentes.Recreer || regle.Taches == ActionTaches.Conserver))
            {
                erreurs.Add($"Nœud '{code}' : une reprise à un nœud de suite annule les attentes et les tâches, elles ne peuvent pas être conservées.");
            }

            if (regle.CodeSuite is not null && regle.CodeCible is not null)
            {
                erreurs.Add($"Nœud '{code}' : indiquez soit un nœud correspondant (Vers), soit un nœud de suite (PuisReprendreA), pas les deux.");
            }
        }

        foreach (var doublon in _renommages.GroupBy(r => r.Value).Where(g => g.Count() > 1))
        {
            erreurs.Add($"Plusieurs variables sont renommées en '{doublon.Key}'.");
        }

        if (erreurs.Count > 0)
        {
            throw new MigrationInvalideException($"Plan de migration '{nomPlan}' invalide :{Environment.NewLine}- {string.Join(Environment.NewLine + "- ", erreurs)}");
        }

        return new PlanMigration
        {
            Noeuds = new Dictionary<string, RegleNoeudMigration>(_noeuds),
            RenommagesVariables = new Dictionary<string, string>(_renommages),
            ValeursVariables = new Dictionary<string, ValeurTypee>(_valeurs),
            CommandesVariables = _commandes.ToList(),
        };
    }
}

/// <summary>Configuration de la règle d'un nœud dans un plan de migration.</summary>
public sealed class ConfigurationRegleNoeud
{
    private readonly RegleNoeudMigration _regle;

    internal ConfigurationRegleNoeud(RegleNoeudMigration regle) => _regle = regle;

    /// <summary>Code du nœud correspondant dans la version cible.</summary>
    public ConfigurationRegleNoeud Vers(string codeCible)
    {
        _regle.CodeCible = codeCible;
        return this;
    }

    /// <summary>Conserve les attentes en cours.</summary>
    public ConfigurationRegleNoeud ConserverAttentes()
    {
        _regle.Attentes = ActionAttentes.Conserver;
        return this;
    }

    /// <summary>Recrée les attentes selon le nœud cible (sans exécuter ses commandes d'entrée).</summary>
    public ConfigurationRegleNoeud RecreerAttentes()
    {
        _regle.Attentes = ActionAttentes.Recreer;
        return this;
    }

    /// <summary>Annule les attentes en cours.</summary>
    public ConfigurationRegleNoeud AnnulerAttentes()
    {
        _regle.Attentes = ActionAttentes.Annuler;
        return this;
    }

    /// <summary>Conserve les tâches ouvertes.</summary>
    public ConfigurationRegleNoeud ConserverTaches()
    {
        _regle.Taches = ActionTaches.Conserver;
        return this;
    }

    /// <summary>Annule les tâches ouvertes dans le bassin du client.</summary>
    public ConfigurationRegleNoeud AnnulerTaches()
    {
        _regle.Taches = ActionTaches.Annuler;
        return this;
    }

    /// <summary>
    /// Après la migration, l'instance reprend en exécutant le nœud <paramref name="codeSuite"/> de la version cible.
    /// Ses attentes, tâches ouvertes et sous-processus actifs sont annulés.
    /// </summary>
    public ConfigurationRegleNoeud PuisReprendreA(string codeSuite)
    {
        _regle.CodeSuite = codeSuite;
        _regle.Attentes ??= ActionAttentes.Annuler;
        _regle.Taches ??= ActionTaches.Annuler;
        return this;
    }
}

/// <summary>Demande de simulation ou de migration d'instances.</summary>
public sealed class DemandeMigration
{
    /// <summary>Clé du processus.</summary>
    public required string CleProcessus { get; init; }

    /// <summary>Version cible ; par défaut, la dernière version active.</summary>
    public int? VersionCible { get; init; }

    /// <summary>Instances visées par identifiant ; sinon, sélection par critère.</summary>
    public IReadOnlyList<IdInstance>? Instances { get; init; }

    /// <summary>Critère : version source.</summary>
    public int? VersionSource { get; init; }

    /// <summary>Critère : code du nœud courant.</summary>
    public string? CodeNoeud { get; init; }

    /// <summary>Nombre maximal d'instances traitées (par lot).</summary>
    public int TailleMax { get; init; } = 100;

    /// <summary>Utilisateur à l'origine de la migration (sinon, <see cref="IContexteUtilisateur"/>).</summary>
    public string? Utilisateur { get; init; }
}

/// <summary>Rapport d'une simulation ou d'une migration.</summary>
public sealed class RapportMigration
{
    /// <summary>Clé du processus.</summary>
    public required string CleProcessus { get; init; }

    /// <summary>Version cible.</summary>
    public required int VersionCible { get; init; }

    /// <summary>Vrai pour une simulation (rien n'est écrit).</summary>
    public required bool Simulation { get; init; }

    /// <summary>Résultat par instance.</summary>
    public required IReadOnlyList<ResultatMigrationInstance> Instances { get; init; }

    /// <summary>Nombre d'instances migrables (ou migrées).</summary>
    public int NombreMigrables => Instances.Count(i => i.Migrable);

    /// <summary>Nombre d'instances bloquées.</summary>
    public int NombreBloquees => Instances.Count(i => !i.Migrable);
}

/// <summary>Résultat de migration d'une instance.</summary>
public sealed class ResultatMigrationInstance
{
    /// <summary>Instance concernée.</summary>
    public required IdInstance IdInstance { get; init; }

    /// <summary>Version source.</summary>
    public required int VersionSource { get; init; }

    /// <summary>Version cible.</summary>
    public required int VersionCible { get; init; }

    /// <summary>Vrai si l'instance est migrable (ou a été migrée).</summary>
    public required bool Migrable { get; init; }

    /// <summary>Raison du blocage.</summary>
    public string? Raison { get; init; }

    /// <summary>Actions prévues ou effectuées.</summary>
    public IReadOnlyList<string> Actions { get; init; } = [];
}
