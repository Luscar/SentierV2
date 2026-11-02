using Sentier.Definitions;

namespace Sentier.Core.Tests.Definitions;

public sealed class ExportateurMermaidTests
{
    private static readonly DefinitionProcessus Definition = ((IDefinitionSentier)new DemandeFictive()).Definir();

    [Fact]
    public void Produit_un_flowchart_avec_un_titre()
    {
        var mermaid = ExportateurMermaid.Exporter(Definition);

        Assert.StartsWith("flowchart TD", mermaid);
        Assert.Contains("Demande fictive (demande-fictive)", mermaid);
    }

    [Fact]
    public void Chaque_noeud_a_sa_forme()
    {
        var mermaid = ExportateurMermaid.Exporter(Definition);

        Assert.Contains("((\"debut\"))", mermaid);                       // début : cercle
        Assert.Contains("(((\"approuvee\")))", mermaid);                 // fin : double cercle
        Assert.Contains("{\"solde-suffisant\"}", mermaid);               // décision : losange
        Assert.Contains("tâche : Approuver la demande", mermaid);        // interactif
        Assert.Contains("enfant-fictif", mermaid);                       // sous-processus
        Assert.Contains(nameof(CommandeFictive), mermaid);               // systémique
    }

    [Fact]
    public void Les_transitions_nommees_portent_leur_libelle()
    {
        var mermaid = ExportateurMermaid.Exporter(Definition);

        // Une sortie renommée affiche son nom puis l'expression de la condition.
        Assert.Contains("|insuffisant : nbJours #gt; solde|", mermaid);
        Assert.Contains("|sinon|", mermaid);
    }

    [Fact]
    public void Les_noeuds_suspensifs_et_les_extremites_sont_classes()
    {
        var mermaid = ExportateurMermaid.Exporter(Definition);

        Assert.Contains("classDef suspensif", mermaid);
        Assert.Matches(@"class [^\n]*suspensif;", mermaid);
        Assert.Matches(@"class [^\n]*extremite;", mermaid);
    }
}
