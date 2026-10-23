using Sentier.Commandes;
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

    /// <summary>Déclare les variables du processus (à écrire en premier).</summary>
    public ConstructeurProcessus Variables(params IVariable[] variables)
    {
        foreach (var variable in variables)
        {
            _variables.Add(variable.VersDeclaration());
        }

        return this;
    }

    /// <summary>Déclare une variable sans constante typée.</summary>
    public ConstructeurProcessus Variable(string nom, TypeVariable type, bool obligatoire = false, object? defaut = null)
    {
        _variables.Add(new DeclarationVariable
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

/// <summary>Constructeur d'un bloc imbriqué (branche d'un nœud à plusieurs sorties).</summary>
public sealed class ConstructeurBloc : ConstructeurSequence<ConstructeurBloc>
{
    internal ConstructeurBloc(EtatConstruction etat, SequenceEnConstruction sequence) : base(etat, sequence)
    {
    }
}

/// <summary>Méthodes communes au processus principal et aux blocs imbriqués.</summary>
/// <typeparam name="TSelf">Type concret du constructeur, pour le chaînage.</typeparam>
public abstract class ConstructeurSequence<TSelf> where TSelf : ConstructeurSequence<TSelf>
{
    /// <summary>Code du nœud de début implicite.</summary>
    public const string CodeDebut = "debut";

    private protected ConstructeurSequence(EtatConstruction etat, SequenceEnConstruction sequence)
    {
        Etat = etat;
        Sequence = sequence;
    }

    private protected EtatConstruction Etat { get; }

    private protected SequenceEnConstruction Sequence { get; }

    private TSelf Moi => (TSelf)this;

    /// <summary>Nœud systémique exécutant la commande <typeparamref name="TCommande"/>.</summary>
    public TSelf Systemique<TCommande>(string code, Action<ConfigurationParametres>? parametres = null)
        where TCommande : ICommandeSentier
        => Systemique(code, NomsSentier.NomCommande(typeof(TCommande)), parametres);

    /// <summary>Nœud systémique exécutant une commande désignée par son nom.</summary>
    public TSelf Systemique(string code, string nomCommande, Action<ConfigurationParametres>? parametres = null)
        => Ajouter(new NoeudSystemique { Code = code, Commande = ConfigurationParametres.Appel(nomCommande, parametres) });

    /// <summary>Nœud d'attente : échéance et/ou signaux, chacun avec sa branche.</summary>
    public TSelf Attente(string code, Action<ConstructeurAttente> configuration)
    {
        var constructeur = new ConstructeurAttente();
        configuration(constructeur);
        return Ajouter(constructeur.Creer(code), constructeur.Branches, constructeur.Erreurs);
    }

    /// <summary>Raccourci : attente d'un seul délai.</summary>
    public TSelf AttendreDelai(string code, TimeSpan duree) => Attente(code, a => a.Echeance(duree));

    /// <summary>Raccourci : attente d'un seul signal.</summary>
    public TSelf AttendreSignal(string code, string nomSignal, Action<OptionsSignal>? options = null)
        => Attente(code, a => a.Signal(nomSignal, options));

    /// <summary>Nœud interactif : crée une tâche nommée d'après le code du nœud.</summary>
    public TSelf Interactif(string code) => Interactif(code, code);

    /// <summary>Nœud interactif : crée une tâche portant le nom <paramref name="nomTache"/>.</summary>
    public TSelf Interactif(string code, string nomTache) => Interactif(code, nomTache, _ => { });

    /// <summary>Nœud interactif nommé d'après son code, avec sa configuration (rôle, assigné, données, commandes).</summary>
    public TSelf Interactif(string code, Action<ConstructeurInteractif> configuration) => Interactif(code, code, configuration);

    /// <summary>Nœud interactif avec le nom de la tâche et sa configuration (rôle, assigné, données, commandes).</summary>
    public TSelf Interactif(string code, string nomTache, Action<ConstructeurInteractif> configuration)
    {
        var constructeur = new ConstructeurInteractif();
        configuration(constructeur);
        return Ajouter(constructeur.Creer(code, nomTache));
    }

    /// <summary>Nœud de décision : branches conditionnelles évaluées dans l'ordre, puis « sinon ».</summary>
    public TSelf Decision(string code, Action<ConstructeurDecision> branches)
    {
        var constructeur = new ConstructeurDecision();
        branches(constructeur);
        return Ajouter(new NoeudDecision { Code = code }, constructeur.Branches, constructeur.Erreurs);
    }

    /// <summary>Nœud sous-processus : démarre une instance enfant sur la clé d'affaire du parent ; à sa fin, mène au nœud suivant.</summary>
    public TSelf SousProcessus(string code, string cleProcessus) => SousProcessus(code, cleProcessus, _ => { });

    /// <summary>Nœud sous-processus avec sa configuration (clé d'affaire, mappages, branches par code de fin).</summary>
    public TSelf SousProcessus(string code, string cleProcessus, Action<ConstructeurSousProcessus> configuration)
    {
        var constructeur = new ConstructeurSousProcessus();
        configuration(constructeur);
        return Ajouter(constructeur.Creer(code, cleProcessus), constructeur.Branches, constructeur.Erreurs);
    }

    /// <summary>Nœud de fin ; son code est le code de fin de l'instance.</summary>
    public TSelf Fin(string code) => Ajouter(new NoeudFin { Code = code });

    /// <summary>
    /// Lien explicite : la sortie du dernier nœud déclaré mène au nœud <paramref name="code"/>
    /// (déclaré avant ou après). Sur un bloc vide, la branche mène directement à ce nœud.
    /// Sur un nœud à plusieurs sorties, désigne le point de convergence de ses branches.
    /// </summary>
    public TSelf Vers(string code)
    {
        if (Sequence.Elements.Count == 0)
        {
            if (Sequence.LienDirect is not null)
            {
                Etat.Erreurs.Add($"Le bloc a déjà un lien vers '{Sequence.LienDirect}'.");
            }

            Sequence.LienDirect = code;
            return Moi;
        }

        var dernier = Sequence.Elements[^1];
        if (dernier.Noeud is NoeudFin)
        {
            Etat.Erreurs.Add($"Le nœud de fin '{dernier.Noeud.Code}' ne peut pas avoir de sortie (Vers '{code}').");
        }
        else if (dernier.Lien is not null)
        {
            Etat.Erreurs.Add($"Le nœud '{dernier.Noeud.Code}' a déjà un lien vers '{dernier.Lien}'.");
        }
        else
        {
            dernier.Lien = code;
        }

        return Moi;
    }

    /// <summary>Donne un libellé lisible au dernier nœud déclaré.</summary>
    public TSelf Libelle(string nom)
    {
        if (Sequence.Elements.Count > 0)
        {
            Sequence.Elements[^1].Noeud.Nom = nom;
        }

        return Moi;
    }

    private protected TSelf Ajouter(Noeud noeud, IReadOnlyList<BrancheDeclaree>? branches = null, IReadOnlyList<string>? erreurs = null)
    {
        if (noeud is not NoeudDebut && Sequence == Etat.Principale && Sequence.Elements.Count == 0)
        {
            Ajouter(new NoeudDebut { Code = CodeDebut });
        }

        if (string.IsNullOrWhiteSpace(noeud.Code))
        {
            Etat.Erreurs.Add($"Un nœud {noeud.Type} n'a pas de code.");
        }
        else if (!Etat.Codes.Add(noeud.Code))
        {
            Etat.Erreurs.Add($"Le code de nœud '{noeud.Code}' est utilisé plusieurs fois.");
        }

        var element = new ElementSequence(noeud);
        Sequence.Elements.Add(element);
        Etat.TousLesElements.Add(element);

        foreach (var erreur in erreurs ?? [])
        {
            Etat.Erreurs.Add($"Nœud '{noeud.Code}' : {erreur}");
        }

        if (branches is not null)
        {
            element.Branches = [];
            foreach (var declaree in branches)
            {
                var branche = new BrancheEnConstruction(declaree.NomSortie)
                {
                    Lien = declaree.Lien,
                    Requete = declaree.Requete,
                    EstSinon = declaree.EstSinon,
                };

                if (declaree.Bloc is not null)
                {
                    branche.Bloc = new SequenceEnConstruction();
                    declaree.Bloc(new ConstructeurBloc(Etat, branche.Bloc));
                }

                element.Branches.Add(branche);
            }
        }

        return Moi;
    }
}

/// <summary>État partagé pendant la construction d'une définition.</summary>
internal sealed class EtatConstruction
{
    internal List<string> Erreurs { get; } = [];

    internal HashSet<string> Codes { get; } = new(StringComparer.Ordinal);

    internal List<ElementSequence> TousLesElements { get; } = [];

    internal SequenceEnConstruction Principale { get; } = new();
}

/// <summary>Séquence de nœuds consécutifs (processus principal ou bloc imbriqué).</summary>
internal sealed class SequenceEnConstruction
{
    internal List<ElementSequence> Elements { get; } = [];

    internal string? LienDirect { get; set; }
}

internal sealed class ElementSequence(Noeud noeud)
{
    public Noeud Noeud { get; } = noeud;

    public string? Lien { get; set; }

    public List<BrancheEnConstruction>? Branches { get; set; }
}

internal sealed class BrancheEnConstruction(string nomSortie)
{
    public string NomSortie { get; } = nomSortie;

    public SequenceEnConstruction? Bloc { get; set; }

    public string? Lien { get; init; }

    public AppelRequete? Requete { get; init; }

    public bool EstSinon { get; init; }
}

/// <summary>Branche telle que déclarée par l'auteur, avant construction de son bloc.</summary>
internal sealed class BrancheDeclaree(string nomSortie)
{
    public string NomSortie { get; set; } = nomSortie;

    public Action<ConstructeurBloc>? Bloc { get; set; }

    public string? Lien { get; set; }

    public AppelRequete? Requete { get; init; }

    public bool EstSinon { get; init; }
}
