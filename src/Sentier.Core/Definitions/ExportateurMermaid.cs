using System.Text;

namespace Sentier.Definitions;

/// <summary>Export d'une définition en diagramme Mermaid (<c>flowchart</c>).</summary>
public static class ExportateurMermaid
{
    /// <summary>Retourne le diagramme Mermaid de la définition.</summary>
    public static string Exporter(DefinitionProcessus definition)
    {
        var sb = new StringBuilder();
        sb.AppendLine("flowchart TD");
        sb.AppendLine($"    %% {Echapper(definition.Nom)} ({definition.CleProcessus}{(definition.Version > 0 ? $" v{definition.Version}" : "")})");

        foreach (var noeud in definition.Noeuds)
        {
            sb.AppendLine($"    {Identifiant(noeud.Code)}{Forme(noeud)}");
        }

        foreach (var transition in definition.Transitions)
        {
            // Une sortie par défaut qui double une branche nommée vers le même nœud n'ajoute rien au diagramme.
            if (transition.NomSortie is null && definition.Sorties(transition.Source).Any(t => t.NomSortie is not null && t.Destination == transition.Destination))
            {
                continue;
            }

            var libelle = Libelle(transition);
            var fleche = libelle is null ? " --> " : $" -->|{Echapper(libelle)}| ";
            sb.AppendLine($"    {Identifiant(transition.Source)}{fleche}{Identifiant(transition.Destination)}");
        }

        sb.AppendLine("    classDef suspensif fill:#fff4d6,stroke:#c79a00;");
        sb.AppendLine("    classDef extremite fill:#e3f2e6,stroke:#2e7d32;");
        var suspensifs = definition.Noeuds.Where(n => n.EstSuspensif).Select(n => Identifiant(n.Code)).ToList();
        if (suspensifs.Count > 0)
        {
            sb.AppendLine($"    class {string.Join(",", suspensifs)} suspensif;");
        }

        var extremites = definition.Noeuds.Where(n => n is NoeudDebut or NoeudFin).Select(n => Identifiant(n.Code)).ToList();
        if (extremites.Count > 0)
        {
            sb.AppendLine($"    class {string.Join(",", extremites)} extremite;");
        }

        return sb.ToString();
    }

    private static string Forme(Noeud noeud)
    {
        var texte = Echapper(noeud.Nom ?? noeud.Code);
        return noeud switch
        {
            NoeudDebut => $"((\"{texte}\"))",
            NoeudFin => $"(((\"{texte}\")))",
            NoeudDecision => $"{{\"{texte}\"}}",
            NoeudAttente attente => $"{{{{\"{texte}<br/><small>{Echapper(DecrireAttente(attente))}</small>\"}}}}",
            NoeudInteractif interactif => $"[/\"{texte}<br/><small>tâche : {Echapper(interactif.NomTache)}</small>\"/]",
            NoeudSousProcessus sousProcessus => $"[[\"{texte}<br/><small>{Echapper(sousProcessus.CleProcessus)}</small>\"]]",
            NoeudSystemique systemique => $"[\"{texte}<br/><small>{Echapper(systemique.Commande.Nom)}</small>\"]",
            _ => $"[\"{texte}\"]",
        };
    }

    private static string DecrireAttente(NoeudAttente attente)
        => string.Join(" | ", attente.Declencheurs.Select(d => d switch
        {
            DeclencheurEcheance e when e.Duree is not null => $"délai {FormaterDuree(e.Duree.Value)}",
            DeclencheurEcheance e when e.DateAbsolue is not null => $"le {e.DateAbsolue:yyyy-MM-dd HH:mm}",
            DeclencheurEcheance e => $"date {e.Variable}",
            DeclencheurSignal s => $"signal {s.NomSignal}",
            _ => d.NomSortie,
        }));

    private static string FormaterDuree(TimeSpan duree)
    {
        var parties = new List<string>();
        if (duree.Days > 0)
        {
            parties.Add($"{duree.Days} j");
        }

        if (duree.Hours > 0)
        {
            parties.Add($"{duree.Hours} h");
        }

        if (duree.Minutes > 0)
        {
            parties.Add($"{duree.Minutes} min");
        }

        if (duree.Seconds > 0 || parties.Count == 0)
        {
            parties.Add($"{duree.Seconds} s");
        }

        return string.Join(" ", parties);
    }

    private static string? Libelle(Transition transition)
    {
        if (transition.Requete is not null)
        {
            var condition = $"{(transition.Requete.Inverser ? "non " : "")}{transition.Requete.Nom}";
            var parDefaut = (transition.Requete.Inverser ? ConstructeurDecision.PrefixeNon : "") + transition.Requete.Nom;
            return transition.NomSortie == parDefaut ? condition : $"{transition.NomSortie} : {condition}";
        }

        if (transition.Condition is not null)
        {
            var expression = transition.Condition.Expression();
            return transition.NomSortie == ConstructeurDecision.NomSortieParDefaut(transition.Condition) ? expression : $"{transition.NomSortie} : {expression}";
        }

        return transition.NomSortie;
    }

    private static string Identifiant(string code)
    {
        var sb = new StringBuilder("n_");
        foreach (var c in code)
        {
            sb.Append(char.IsAsciiLetterOrDigit(c) ? c : '_');
        }

        return sb.ToString();
    }

    private static string Echapper(string texte) => texte.Replace("\"", "#quot;").Replace("|", "#124;").Replace("<", "#lt;").Replace(">", "#gt;");
}
