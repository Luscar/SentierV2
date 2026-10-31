using Sentier.Commandes;

namespace Sentier.Definitions;

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

    private TSelf Systemique(string code, string nomCommande, Action<ConfigurationParametres>? parametres = null)
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

    /// <summary>Nœud sous-processus défini par la classe <typeparamref name="TDefinition"/>, sur la clé d'affaire du parent ; à sa fin, mène au nœud suivant.</summary>
    public TSelf SousProcessus<TDefinition>(string code) where TDefinition : DefinitionSentier
        => SousProcessus(code, NomsSentier.CleProcessus(typeof(TDefinition)));

    /// <summary>Nœud sous-processus défini par la classe <typeparamref name="TDefinition"/>, avec sa configuration.</summary>
    public TSelf SousProcessus<TDefinition>(string code, Action<ConstructeurSousProcessus> configuration) where TDefinition : DefinitionSentier
        => SousProcessus(code, NomsSentier.CleProcessus(typeof(TDefinition)), configuration);

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
                    Condition = declaree.Condition,
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
