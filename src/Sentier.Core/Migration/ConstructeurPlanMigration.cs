using Sentier.Commandes;
using Sentier.Definitions;
using Sentier.Variables;

namespace Sentier.Migration;

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
