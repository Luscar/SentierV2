using Sentier.Variables;

namespace Sentier.Definitions;

/// <summary>Validation structurelle d'une définition (construction et déploiement).</summary>
public static class ValidateurDefinition
{
    /// <summary>Retourne la liste des erreurs de la définition (vide si elle est valide).</summary>
    public static IReadOnlyList<string> Valider(DefinitionProcessus definition)
    {
        var erreurs = new List<string>();
        ValiderVariables(definition, erreurs);
        ValiderNoeuds(definition, erreurs);
        ValiderTransitions(definition, erreurs);
        ValiderAtteignabilite(definition, erreurs);
        return erreurs;
    }

    private static void ValiderVariables(DefinitionProcessus definition, List<string> erreurs)
    {
        var noms = new HashSet<string>(StringComparer.Ordinal);
        foreach (var variable in definition.Variables)
        {
            if (string.IsNullOrWhiteSpace(variable.Nom))
            {
                erreurs.Add("Une variable n'a pas de nom.");
                continue;
            }

            if (VariablesProcessus.EstReserve(variable.Nom))
            {
                erreurs.Add($"Le nom de variable '{variable.Nom}' est réservé : les noms commençant par '_' sont réservés aux variables système.");
            }

            if (!noms.Add(variable.Nom))
            {
                erreurs.Add($"La variable '{variable.Nom}' est déclarée plusieurs fois.");
            }

            if (variable.Defaut is not null && variable.Defaut.Type != variable.Type)
            {
                erreurs.Add($"La valeur par défaut de la variable '{variable.Nom}' est de type {variable.Defaut.Type} au lieu de {variable.Type}.");
            }
        }
    }

    private static void ValiderNoeuds(DefinitionProcessus definition, List<string> erreurs)
    {
        var codes = new HashSet<string>(StringComparer.Ordinal);
        foreach (var noeud in definition.Noeuds)
        {
            if (!codes.Add(noeud.Code))
            {
                AjouterUnique(erreurs, $"Le code de nœud '{noeud.Code}' est utilisé plusieurs fois.");
            }
        }

        var debuts = definition.Noeuds.OfType<NoeudDebut>().Count();
        if (debuts != 1)
        {
            erreurs.Add(debuts == 0 ? "La définition n'a pas de nœud de début." : "La définition a plusieurs nœuds de début.");
        }

        if (!definition.Noeuds.OfType<NoeudFin>().Any())
        {
            erreurs.Add("La définition doit avoir au moins un nœud de fin.");
        }

        foreach (var noeud in definition.Noeuds)
        {
            var sorties = definition.Sorties(noeud.Code);
            switch (noeud)
            {
                case NoeudSystemique systemique:
                    ValiderAppel(definition, systemique.Commande.Nom, systemique.Commande.Parametres, $"commande du nœud '{noeud.Code}'", erreurs);
                    break;

                case NoeudAttente attente:
                    ValiderAttente(definition, attente, erreurs);
                    break;

                case NoeudInteractif interactif:
                    ValiderInteractif(definition, interactif, erreurs);
                    break;

                case NoeudDecision:
                    if (!sorties.Any(t => t.Requete is not null || t.Condition is not null))
                    {
                        erreurs.Add($"La décision '{noeud.Code}' doit avoir au moins une branche conditionnelle.");
                    }

                    if (sorties.Count(t => t.EstSinon) != 1)
                    {
                        erreurs.Add($"La décision '{noeud.Code}' doit avoir exactement une branche « sinon ».");
                    }

                    foreach (var transition in sorties.Where(t => t.Requete is not null))
                    {
                        ValiderAppel(definition, transition.Requete!.Nom, transition.Requete.Parametres, $"requête de la branche '{transition.NomSortie}' du nœud '{noeud.Code}'", erreurs);
                    }

                    foreach (var transition in sorties.Where(t => t.Condition is not null))
                    {
                        ValiderCondition(definition, transition.Condition!, $"condition de la branche '{transition.NomSortie}' du nœud '{noeud.Code}'", erreurs);
                    }

                    break;

                case NoeudSousProcessus sousProcessus:
                    ValiderSousProcessus(definition, sousProcessus, erreurs);
                    break;

                case NoeudFin when sorties.Count > 0:
                    erreurs.Add($"Le nœud de fin '{noeud.Code}' ne peut pas avoir de sortie.");
                    break;
            }

            var nomsSortie = sorties.Where(t => t.NomSortie is not null).GroupBy(t => t.NomSortie).Where(g => g.Count() > 1);
            foreach (var doublon in nomsSortie)
            {
                erreurs.Add($"Le nœud '{noeud.Code}' a plusieurs sorties nommées '{doublon.Key}'.");
            }

            if (noeud is not NoeudFin && sorties.Count == 0)
            {
                AjouterUnique(erreurs, $"Le nœud '{noeud.Code}' n'a pas de sortie : déclarez un nœud après lui, un lien Vers(...) ou une fin.");
            }
        }
    }

    private static void ValiderAttente(DefinitionProcessus definition, NoeudAttente attente, List<string> erreurs)
    {
        if (attente.Declencheurs.Count == 0)
        {
            erreurs.Add($"L'attente '{attente.Code}' doit avoir au moins un déclencheur.");
        }

        var echeances = attente.Declencheurs.OfType<DeclencheurEcheance>().ToList();
        if (echeances.Count > 1)
        {
            erreurs.Add($"L'attente '{attente.Code}' a plus d'une échéance.");
        }

        foreach (var echeance in echeances)
        {
            var sources = (echeance.DateAbsolue is null ? 0 : 1) + (echeance.Duree is null ? 0 : 1) + (echeance.Variable is null ? 0 : 1);
            if (sources != 1)
            {
                erreurs.Add($"L'échéance de l'attente '{attente.Code}' doit avoir exactement une source : date, durée ou variable.");
            }

            if (echeance.Variable is not null)
            {
                ValiderVariable(definition, echeance.Variable, TypeVariable.Date, $"échéance de l'attente '{attente.Code}'", erreurs);
            }
        }

        foreach (var doublon in attente.Declencheurs.OfType<DeclencheurSignal>().GroupBy(s => s.NomSignal).Where(g => g.Count() > 1))
        {
            erreurs.Add($"L'attente '{attente.Code}' attend plusieurs fois le signal '{doublon.Key}'.");
        }

        foreach (var signal in attente.Declencheurs.OfType<DeclencheurSignal>())
        {
            if (string.IsNullOrWhiteSpace(signal.NomSignal))
            {
                erreurs.Add($"Un signal de l'attente '{attente.Code}' n'a pas de nom.");
            }

            if (signal.VariableCorrelation is not null)
            {
                ValiderVariable(definition, signal.VariableCorrelation, null, $"corrélation du signal '{signal.NomSignal}' de l'attente '{attente.Code}'", erreurs);
            }
        }

        var sorties = definition.Sorties(attente.Code);
        foreach (var declencheur in attente.Declencheurs)
        {
            if (!sorties.Any(t => t.NomSortie == declencheur.NomSortie))
            {
                erreurs.Add($"Le déclencheur '{declencheur.NomSortie}' de l'attente '{attente.Code}' n'a pas de sortie.");
            }
        }

        foreach (var commande in attente.CommandesEntree)
        {
            ValiderAppel(definition, commande.Nom, commande.Parametres, $"commande d'entrée de l'attente '{attente.Code}'", erreurs);
        }
    }

    private static void ValiderInteractif(DefinitionProcessus definition, NoeudInteractif interactif, List<string> erreurs)
    {
        if (string.IsNullOrWhiteSpace(interactif.NomTache))
        {
            erreurs.Add($"Le nœud interactif '{interactif.Code}' n'a pas de nom de tâche.");
        }

        if (interactif.Assigne is { Source: SourceParametre.Variable, Variable: { } variableAssigne })
        {
            ValiderVariable(definition, variableAssigne, TypeVariable.Texte, $"assigné du nœud '{interactif.Code}'", erreurs);
        }

        if (interactif.VariableEcheance is not null)
        {
            ValiderVariable(definition, interactif.VariableEcheance, TypeVariable.Date, $"échéance du nœud '{interactif.Code}'", erreurs);
        }

        ValiderParametres(definition, interactif.Donnees, $"données du nœud '{interactif.Code}'", erreurs);
        foreach (var commande in interactif.CommandesEntree.Concat(interactif.CommandesSortie))
        {
            ValiderAppel(definition, commande.Nom, commande.Parametres, $"commande du nœud '{interactif.Code}'", erreurs);
        }
    }

    private static void ValiderSousProcessus(DefinitionProcessus definition, NoeudSousProcessus sousProcessus, List<string> erreurs)
    {
        if (string.IsNullOrWhiteSpace(sousProcessus.CleProcessus))
        {
            erreurs.Add($"Le sous-processus '{sousProcessus.Code}' n'indique pas la clé du processus enfant.");
        }

        if (sousProcessus.VariableCleAffaire is not null)
        {
            ValiderVariable(definition, sousProcessus.VariableCleAffaire, TypeVariable.Entier, $"clé d'affaire du sous-processus '{sousProcessus.Code}'", erreurs);
        }

        ValiderParametres(definition, sousProcessus.Entrees, $"mappage d'entrée du sous-processus '{sousProcessus.Code}'", erreurs);
        foreach (var doublon in sousProcessus.Entrees.GroupBy(e => e.Nom).Where(g => g.Count() > 1))
        {
            erreurs.Add($"Le sous-processus '{sousProcessus.Code}' remplit plusieurs fois la variable '{doublon.Key}' de l'enfant.");
        }

        foreach (var sortie in sousProcessus.Sorties)
        {
            ValiderVariable(definition, sortie.VariableParent, null, $"mappage de sortie du sous-processus '{sousProcessus.Code}'", erreurs);
        }
    }

    private static void ValiderAppel(DefinitionProcessus definition, string nom, IReadOnlyList<Parametre> parametres, string contexte, List<string> erreurs)
    {
        if (string.IsNullOrWhiteSpace(nom))
        {
            erreurs.Add($"Le nom de la {contexte} est vide.");
        }

        ValiderParametres(definition, parametres, contexte, erreurs);
        foreach (var doublon in parametres.GroupBy(p => p.Nom).Where(g => g.Count() > 1))
        {
            erreurs.Add($"Le paramètre '{doublon.Key}' de la {contexte} est déclaré plusieurs fois.");
        }
    }

    private static void ValiderParametres(DefinitionProcessus definition, IReadOnlyList<Parametre> parametres, string contexte, List<string> erreurs)
    {
        foreach (var parametre in parametres)
        {
            switch (parametre.Source)
            {
                case SourceParametre.Variable when parametre.Variable is null:
                    erreurs.Add($"Le paramètre '{parametre.Nom}' ({contexte}) ne désigne aucune variable.");
                    break;
                case SourceParametre.Variable:
                    ValiderVariable(definition, parametre.Variable!, null, $"paramètre '{parametre.Nom}' ({contexte})", erreurs);
                    break;
                case SourceParametre.Statique when parametre.Valeur is null:
                    erreurs.Add($"Le paramètre statique '{parametre.Nom}' ({contexte}) n'a pas de valeur.");
                    break;
            }
        }
    }

    private static void ValiderCondition(DefinitionProcessus definition, ConditionVariable condition, string contexte, List<string> erreurs)
    {
        if (!definition.Declarations.TryGetValue(condition.Variable, out var declaration))
        {
            erreurs.Add($"La variable '{condition.Variable}' ({contexte}) n'est pas déclarée.");
            return;
        }

        if (ConditionVariable.EstUnaire(condition.Operateur))
        {
            if (condition.Valeur is not null || condition.VariableComparee is not null)
            {
                erreurs.Add($"L'opérateur {condition.Operateur} ({contexte}) n'attend pas de valeur de comparaison.");
            }

            return;
        }

        if ((condition.Valeur is null) == (condition.VariableComparee is null))
        {
            erreurs.Add($"L'opérateur {condition.Operateur} ({contexte}) exige soit une valeur, soit une variable de comparaison.");
            return;
        }

        if (OperateurIncompatible(condition.Operateur, declaration.Type) is { } raison)
        {
            erreurs.Add($"L'opérateur {condition.Operateur} ({contexte}) ne s'applique pas à la variable '{condition.Variable}' de type {declaration.Type} : {raison}.");
            return;
        }

        TypeVariable typeOperande;
        string libelleOperande;
        if (condition.VariableComparee is { } comparee)
        {
            if (!definition.Declarations.TryGetValue(comparee, out var declarationComparee))
            {
                erreurs.Add($"La variable de comparaison '{comparee}' ({contexte}) n'est pas déclarée.");
                return;
            }

            typeOperande = declarationComparee.Type;
            libelleOperande = $"la variable '{comparee}'";
        }
        else
        {
            typeOperande = condition.Valeur!.Type;
            libelleOperande = "la valeur";
        }

        if (!OperandesCompatibles(declaration.Type, typeOperande))
        {
            erreurs.Add($"La variable '{condition.Variable}' ({contexte}) est de type {declaration.Type} ; {libelleOperande} de comparaison est de type {typeOperande}.");
        }
    }

    /// <summary>Raison pour laquelle un opérateur binaire ne s'applique pas à un type, ou <c>null</c>.</summary>
    internal static string? OperateurIncompatible(OperateurComparaison operateur, TypeVariable type) => operateur switch
    {
        _ when ConditionVariable.EstUnaire(operateur) => null,
        _ when type == TypeVariable.Json => "une variable JSON ne se teste que par EstRenseignee ou EstVide",
        OperateurComparaison.Contient or OperateurComparaison.CommencePar when type != TypeVariable.Texte => "réservé aux variables texte",
        OperateurComparaison.Superieur or OperateurComparaison.SuperieurOuEgal or OperateurComparaison.Inferieur or OperateurComparaison.InferieurOuEgal
            when type == TypeVariable.Booleen => "un booléen ne se compare que par égalité",
        _ => null,
    };

    /// <summary>Un entier et un décimal se comparent entre eux ; les autres types seulement avec eux-mêmes.</summary>
    internal static bool OperandesCompatibles(TypeVariable variable, TypeVariable operande)
        => variable == operande || (Numerique(variable) && Numerique(operande));

    private static bool Numerique(TypeVariable type) => type is TypeVariable.Entier or TypeVariable.Decimal;

    private static void ValiderVariable(DefinitionProcessus definition, string nom, TypeVariable? typeAttendu, string contexte, List<string> erreurs)
    {
        if (!definition.Declarations.TryGetValue(nom, out var declaration))
        {
            erreurs.Add($"La variable '{nom}' ({contexte}) n'est pas déclarée.");
        }
        else if (typeAttendu is not null && declaration.Type != typeAttendu)
        {
            erreurs.Add($"La variable '{nom}' ({contexte}) doit être de type {typeAttendu}, elle est de type {declaration.Type}.");
        }
    }

    private static void ValiderTransitions(DefinitionProcessus definition, List<string> erreurs)
    {
        var codes = definition.Noeuds.Select(n => n.Code).ToHashSet(StringComparer.Ordinal);
        foreach (var transition in definition.Transitions)
        {
            if (!codes.Contains(transition.Source))
            {
                erreurs.Add($"Une transition part du nœud inexistant '{transition.Source}'.");
            }

            if (!codes.Contains(transition.Destination))
            {
                erreurs.Add($"Le lien du nœud '{transition.Source}' mène au nœud inexistant '{transition.Destination}'.");
            }
        }
    }

    private static void ValiderAtteignabilite(DefinitionProcessus definition, List<string> erreurs)
    {
        var debut = definition.Noeuds.OfType<NoeudDebut>().FirstOrDefault();
        if (debut is null)
        {
            return;
        }

        var atteints = new HashSet<string>(StringComparer.Ordinal) { debut.Code };
        var aVisiter = new Queue<string>([debut.Code]);
        while (aVisiter.Count > 0)
        {
            foreach (var transition in definition.Sorties(aVisiter.Dequeue()))
            {
                if (atteints.Add(transition.Destination))
                {
                    aVisiter.Enqueue(transition.Destination);
                }
            }
        }

        foreach (var noeud in definition.Noeuds.Where(n => !atteints.Contains(n.Code)))
        {
            erreurs.Add($"Le nœud '{noeud.Code}' n'est jamais atteint depuis le début : reliez-le par un lien Vers(...) ou une branche.");
        }
    }

    private static void AjouterUnique(List<string> erreurs, string erreur)
    {
        if (!erreurs.Contains(erreur))
        {
            erreurs.Add(erreur);
        }
    }
}
