using Sentier.Variables;

namespace Sentier.Definitions;

/// <summary>Validation d'un nœud sous-processus face à la définition de l'enfant (déploiement et démarrage).</summary>
public static class ValidateurSousProcessus
{
    /// <summary>Retourne les erreurs de mappage et de sorties du nœud.</summary>
    public static IReadOnlyList<string> Valider(DefinitionProcessus parent, NoeudSousProcessus noeud, DefinitionProcessus enfant)
    {
        var erreurs = new List<string>();
        var contexte = $"sous-processus '{noeud.Code}' ({enfant.CleProcessus})";

        foreach (var entree in noeud.Entrees)
        {
            if (!enfant.Declarations.TryGetValue(entree.Nom, out var cible))
            {
                erreurs.Add($"{contexte} : la variable '{entree.Nom}' n'est pas déclarée dans l'enfant.");
                continue;
            }

            TypeVariable? source = entree.Source switch
            {
                SourceParametre.Statique => entree.Valeur?.Type,
                SourceParametre.CleAffaire => TypeVariable.Entier,
                _ => entree.Variable is not null && parent.Declarations.TryGetValue(entree.Variable, out var declaration) ? declaration.Type : null,
            };

            if (source is not null && !ConvertisseurValeurs.SontCompatibles(source.Value, cible.Type))
            {
                erreurs.Add($"{contexte} : la variable '{entree.Nom}' de l'enfant est de type {cible.Type}, la source est de type {source}.");
            }
        }

        foreach (var obligatoire in enfant.Variables.Where(v => v.Obligatoire && v.Defaut is null))
        {
            if (noeud.Entrees.All(e => e.Nom != obligatoire.Nom))
            {
                erreurs.Add($"{contexte} : la variable obligatoire '{obligatoire.Nom}' de l'enfant n'est pas mappée.");
            }
        }

        foreach (var sortie in noeud.Sorties)
        {
            if (!enfant.Declarations.TryGetValue(sortie.VariableEnfant, out var source))
            {
                erreurs.Add($"{contexte} : la variable de sortie '{sortie.VariableEnfant}' n'est pas déclarée dans l'enfant.");
                continue;
            }

            if (parent.Declarations.TryGetValue(sortie.VariableParent, out var cible) && !ConvertisseurValeurs.SontCompatibles(source.Type, cible.Type))
            {
                erreurs.Add($"{contexte} : la variable '{sortie.VariableParent}' du parent est de type {cible.Type}, la variable '{sortie.VariableEnfant}' de l'enfant est de type {source.Type}.");
            }
        }

        var codesFin = enfant.CodesFin.ToHashSet(StringComparer.Ordinal);
        foreach (var transition in parent.Sorties(noeud.Code))
        {
            if (transition.NomSortie is { } codeFin && codeFin != NoeudSousProcessus.SortieAnnule && !codesFin.Contains(codeFin))
            {
                erreurs.Add($"{contexte} : le code de fin '{codeFin}' n'existe pas dans l'enfant (codes : {string.Join(", ", codesFin)}).");
            }
        }

        if (parent.Sortie(noeud.Code, null) is null)
        {
            foreach (var codeFin in codesFin.Where(c => parent.Sorties(noeud.Code).All(t => t.NomSortie != c)))
            {
                erreurs.Add($"{contexte} : le code de fin '{codeFin}' de l'enfant n'a ni branche ni sortie par défaut.");
            }
        }

        return erreurs;
    }
}
