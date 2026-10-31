using System.Text.Json;
using Sentier.Variables;

namespace Sentier.Definitions;

/// <summary>
/// Constructeur fluide d'une définition de processus. Enchaîner deux nœuds crée automatiquement
/// la transition entre eux ; les branches s'écrivent en blocs imbriqués (<c>Alors</c>) ou en liens
/// (<c>Vers</c>). Le nœud de début est implicite.
/// </summary>
/// <example>
/// <code>
/// ConstructeurProcessus.Nouveau("demande-conge", "Demande de congé")
///     .Variables(NbJours, Solde)
///     .Systemique&lt;CalculerSolde&gt;("calculer-solde")
///     .Decision("solde-suffisant", d =&gt; d
///         .Si&lt;SoldeSuffisant&gt;(p =&gt; p.Variables(NbJours, Solde))
///         .Sinon(b =&gt; b.Fin("refusee")))
///     .Interactif("approbation", "Approuver la demande")
///     .Fin("approuvee")
///     .Construire();
/// </code>
/// </example>
public sealed class ConstructeurProcessus : ConstructeurSequence<ConstructeurProcessus>
{
    private readonly string _cleProcessus;
    private readonly string _nom;
    private readonly List<DeclarationVariable> _variables = [];
    private bool _instanceUnique = true;
    private bool _migrationAutomatique;

    private ConstructeurProcessus(string cleProcessus, string nom, EtatConstruction etat)
        : base(etat, etat.Principale)
    {
        _cleProcessus = cleProcessus;
        _nom = nom;
    }

    /// <summary>Commence une nouvelle définition.</summary>
    /// <param name="cleProcessus">Clé stable du processus.</param>
    /// <param name="nom">Nom lisible ; par défaut, la clé.</param>
    public static ConstructeurProcessus Nouveau(string cleProcessus, string? nom = null)
    {
        if (string.IsNullOrWhiteSpace(cleProcessus))
        {
            throw new ArgumentException("La clé du processus est obligatoire.", nameof(cleProcessus));
        }

        return new ConstructeurProcessus(cleProcessus, nom ?? cleProcessus, new EtatConstruction());
    }

    /// <summary>
    /// Déclare les variables du processus (à écrire en premier). Inutile avec <see cref="DefinitionSentier"/>,
    /// qui déclare les champs statiques de la classe. Redéclarer une variable identique est sans effet.
    /// </summary>
    public ConstructeurProcessus Variables(params IVariable[] variables)
    {
        foreach (var variable in variables)
        {
            Declarer(variable.VersDeclaration());
        }

        return this;
    }

    /// <summary>Déclare une variable sans constante typée.</summary>
    public ConstructeurProcessus Variable(string nom, TypeVariable type, bool obligatoire = false, object? defaut = null)
    {
        Declarer(new DeclarationVariable
        {
            Nom = nom,
            Type = type,
            Obligatoire = obligatoire,
            Defaut = defaut is null ? null : ValeurTypee.Depuis(defaut, type, nom),
        });
        return this;
    }

    /// <summary>Autorise plusieurs instances actives pour une même clé d'affaire.</summary>
    public ConstructeurProcessus InstancesMultiples()
    {
        _instanceUnique = false;
        return this;
    }

    /// <summary>Active la migration automatique des instances lors d'un changement compatible.</summary>
    public ConstructeurProcessus MigrationAutomatique()
    {
        _migrationAutomatique = true;
        return this;
    }

    /// <summary>
    /// Déclare le nœud de début. Facultatif : sans lui, un début de code <c>debut</c> est
    /// ajouté devant le premier nœud. À n'écrire que pour donner un autre code.
    /// </summary>
    public ConstructeurProcessus Debut(string code = CodeDebut)
    {
        if (Sequence.Elements.Count > 0)
        {
            Etat.Erreurs.Add($"Le nœud de début '{code}' doit être le premier nœud du processus.");
        }

        return Ajouter(new NoeudDebut { Code = code });
    }

    /// <summary>Compile les transitions, valide la définition et la retourne.</summary>
    /// <exception cref="DefinitionInvalideException">La définition est invalide.</exception>
    public DefinitionProcessus Construire()
    {
        var erreurs = new List<string>(Etat.Erreurs);
        var principale = Etat.Principale;
        if (principale.Elements.Count == 0)
        {
            erreurs.Add("Le processus ne déclare aucun nœud.");
        }

        var transitions = new List<Transition>();
        Compiler(principale, null, transitions, erreurs);

        var definition = new DefinitionProcessus
        {
            CleProcessus = _cleProcessus,
            Nom = _nom,
            InstanceUnique = _instanceUnique,
            MigrationAutomatique = _migrationAutomatique,
            Variables = _variables.ToList(),
            Noeuds = Etat.TousLesElements.Select(e => e.Noeud).ToList(),
            Transitions = transitions,
        };

        foreach (var erreur in ValidateurDefinition.Valider(definition))
        {
            if (!erreurs.Contains(erreur))
            {
                erreurs.Add(erreur);
            }
        }

        if (erreurs.Count > 0)
        {
            throw new DefinitionInvalideException(_cleProcessus, erreurs);
        }

        return definition;
    }

    private void Declarer(DeclarationVariable declaration)
    {
        var existante = _variables.FirstOrDefault(v => v.Nom == declaration.Nom);
        if (existante is null)
        {
            _variables.Add(declaration);
        }
        else if (JsonSerializer.Serialize(existante, SerialisationDefinition.Options) != JsonSerializer.Serialize(declaration, SerialisationDefinition.Options))
        {
            Etat.Erreurs.Add($"La variable '{declaration.Nom}' est déclarée plusieurs fois, de façons différentes.");
        }
    }

    /// <summary>
    /// Résout la sortie de chaque nœud d'une séquence : lien explicite, sinon nœud suivant de la
    /// séquence, sinon continuation (nœud qui suit le nœud à plusieurs sorties englobant).
    /// </summary>
    private static void Compiler(SequenceEnConstruction sequence, string? continuation, List<Transition> transitions, List<string> erreurs)
    {
        for (var i = 0; i < sequence.Elements.Count; i++)
        {
            var element = sequence.Elements[i];
            var code = element.Noeud.Code;
            if (element.Noeud is NoeudFin)
            {
                continue;
            }

            var suivant = i + 1 < sequence.Elements.Count ? sequence.Elements[i + 1].Noeud.Code : continuation;
            var sortie = element.Lien ?? suivant;

            if (element.Branches is null)
            {
                if (sortie is null)
                {
                    erreurs.Add($"Le nœud '{code}' n'a pas de sortie : déclarez un nœud après lui, un lien Vers(...) ou une fin.");
                }
                else
                {
                    transitions.Add(new Transition { Source = code, Destination = sortie });
                }

                continue;
            }

            foreach (var branche in element.Branches)
            {
                var destination = branche.Lien ?? (branche.Bloc is null ? sortie : Entree(branche.Bloc, sortie));
                if (destination is null)
                {
                    erreurs.Add($"La branche '{branche.NomSortie}' du nœud '{code}' n'a pas de destination : déclarez un nœud après '{code}', un bloc ou un lien.");
                }
                else
                {
                    transitions.Add(new Transition
                    {
                        Source = code,
                        Destination = destination,
                        NomSortie = branche.NomSortie,
                        Requete = branche.Requete,
                        Condition = branche.Condition,
                        EstSinon = branche.EstSinon,
                    });
                }

                if (branche.Bloc is not null)
                {
                    Compiler(branche.Bloc, sortie, transitions, erreurs);
                }
            }

            if (element.Noeud is NoeudSousProcessus)
            {
                if (sortie is not null)
                {
                    transitions.Add(new Transition { Source = code, Destination = sortie });
                }
                else if (element.Branches.Count == 0)
                {
                    erreurs.Add($"Le sous-processus '{code}' n'a pas de sortie : déclarez un nœud après lui, un lien Vers(...) ou des branches par code de fin.");
                }
            }
        }
    }

    private static string? Entree(SequenceEnConstruction bloc, string? continuation)
        => bloc.LienDirect ?? (bloc.Elements.Count > 0 ? bloc.Elements[0].Noeud.Code : continuation);
}
