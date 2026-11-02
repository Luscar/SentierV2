using Sentier.Definitions;
using Sentier.Variables;

namespace Sentier.Core.Tests.Construction;

public sealed class ConstructeurProcessusTests
{
    private static readonly Variable<decimal> Montant = Variable.Decimal("montant");
    private static readonly Variable<bool> Urgent = Variable.Booleen("urgent");
    private static readonly Variable<string> Commentaire = Variable.Texte("commentaire");

    [Fact]
    public void Enchainer_deux_noeuds_cree_la_transition_et_un_debut_implicite()
    {
        var definition = ConstructeurProcessus.Nouveau("simple")
            .Systemique<CommandeFictive>("a")
            .Systemique<AutreCommandeFictive>("b")
            .Fin("fin")
            .Construire();

        Assert.Equal(["debut", "a", "b", "fin"], definition.Noeuds.Select(n => n.Code));
        Assert.IsType<NoeudDebut>(definition.NoeudDebut);
        Assert.Equal(
            [("debut", "a"), ("a", "b"), ("b", "fin")],
            definition.Transitions.Select(t => (t.Source, t.Destination)));
        Assert.Equal("simple", definition.Nom);
        Assert.Equal(["fin"], definition.CodesFin);
        Assert.Equal(nameof(AutreCommandeFictive), ((NoeudSystemique)definition.Noeud("b")).Commande.Nom);
    }

    [Fact]
    public void Les_options_par_defaut_sont_instance_unique_sans_migration_automatique()
    {
        var parDefaut = ConstructeurProcessus.Nouveau("p").Fin("fin").Construire();
        var options = ConstructeurProcessus.Nouveau("p").InstancesMultiples().MigrationAutomatique().Fin("fin").Construire();

        Assert.True(parDefaut.InstanceUnique);
        Assert.False(parDefaut.MigrationAutomatique);
        Assert.False(options.InstanceUnique);
        Assert.True(options.MigrationAutomatique);
    }

    [Fact]
    public void Vers_cree_une_boucle_vers_un_noeud_declare_avant()
    {
        var definition = ConstructeurProcessus.Nouveau("boucle")
            .Decision("est-complet", d => d
                .Si<RequeteFictive>().Vers("traiter")
                .Sinon("completer"))
            .Interactif("completer", "Compléter le dossier").Vers("est-complet")
            .Systemique<CommandeFictive>("traiter")
            .Fin("fin")
            .Construire();

        Assert.Contains(definition.Transitions, t => t.Source == "completer" && t.Destination == "est-complet");
        Assert.Contains(definition.Transitions, t => t.Source == "est-complet" && t.Destination == "traiter" && t.NomSortie == nameof(RequeteFictive));
        Assert.Contains(definition.Transitions, t => t.Source == "est-complet" && t.Destination == "completer" && t.EstSinon);
    }

    [Fact]
    public void Une_branche_sans_destination_rejoint_le_noeud_suivant()
    {
        var definition = ConstructeurProcessus.Nouveau("convergence")
            .Variables(Montant)
            .Decision("montant-eleve", d => d
                .Si(Montant).SuperieurA(10_000m).Alors(b => b.Interactif("approbation-direction"))
                .Sinon())
            .Systemique<CommandeFictive>("payer")
            .Fin("fin")
            .Construire();

        Assert.Contains(definition.Transitions, t => t.Source == "approbation-direction" && t.Destination == "payer");
        Assert.Contains(definition.Transitions, t => t.Source == "montant-eleve" && t.Destination == "payer" && t.EstSinon);
        var condition = Assert.Single(definition.Sorties("montant-eleve"), t => t.Condition is not null);
        Assert.Equal("approbation-direction", condition.Destination);
        Assert.Equal(OperateurComparaison.Superieur, condition.Condition!.Operateur);
    }

    [Fact]
    public void SiPas_prefixe_le_nom_de_sortie_et_Nommee_le_remplace()
    {
        var definition = ConstructeurProcessus.Nouveau("noms")
            .Decision("choix", d => d
                .SiPas<RequeteFictive>().Vers("a")
                .Si<RequeteFictive>().Nommee("oui").Vers("b")
                .Sinon("a"))
            .Fin("a")
            .Fin("b")
            .Construire();

        var noms = definition.Sorties("choix").Select(t => t.NomSortie).ToList();
        Assert.Contains("non-" + nameof(RequeteFictive), noms);
        Assert.Contains("oui", noms);
        Assert.Contains("sinon", noms);
    }

    [Fact]
    public void Une_attente_peut_combiner_signaux_et_echeance()
    {
        var definition = ConstructeurProcessus.Nouveau("attente")
            .Attente("attendre-rh", a => a
                .Signal("VALIDATION_RH")
                .Signal("REFUS_RH").Vers("refusee")
                .Echeance(TimeSpan.FromDays(2)).Alors(b => b.Systemique<CommandeFictive>("relancer").Vers("attendre-rh")))
            .Fin("acceptee")
            .Fin("refusee")
            .Construire();

        var attente = Assert.IsType<NoeudAttente>(definition.Noeud("attendre-rh"));
        Assert.True(attente.EstSuspensif);
        Assert.Equal(3, attente.Declencheurs.Count);
        Assert.Equal("refusee", definition.Sortie("attendre-rh", "REFUS_RH")!.Destination);
        Assert.Equal("acceptee", definition.Sortie("attendre-rh", "VALIDATION_RH")!.Destination);
        Assert.Equal("relancer", definition.Sortie("attendre-rh", "echeance")!.Destination);
    }

    [Fact]
    public void Le_noeud_interactif_porte_role_echeance_et_donnees()
    {
        var definition = ConstructeurProcessus.Nouveau("tache")
            .Variables(Montant)
            .Interactif("approbation", "Approuver", i => i
                .Role("gestionnaire")
                .Echeance(TimeSpan.FromDays(3))
                .Donnees(Montant)
                .CommandeEntree<CommandeFictive>()
                .CommandeSortie<AutreCommandeFictive>())
            .Fin("fin")
            .Construire();

        var noeud = Assert.IsType<NoeudInteractif>(definition.Noeud("approbation"));
        Assert.Equal("Approuver", noeud.NomTache);
        Assert.Equal("gestionnaire", noeud.Role);
        Assert.Equal(TimeSpan.FromDays(3), noeud.DureeEcheance);
        Assert.Equal("montant", Assert.Single(noeud.Donnees).Nom);
        Assert.Equal(nameof(CommandeFictive), Assert.Single(noeud.CommandesEntree).Nom);
        Assert.Equal(nameof(AutreCommandeFictive), Assert.Single(noeud.CommandesSortie).Nom);
    }

    [Fact]
    public void La_classe_DefinitionSentier_tire_la_cle_le_nom_et_les_variables_de_la_classe()
    {
        var definition = ((IDefinitionSentier)new DemandeFictive()).Definir();

        Assert.Equal("demande-fictive", definition.CleProcessus);
        Assert.Equal("Demande fictive", definition.Nom);
        Assert.Equal(["nbJours", "solde", "montant"], definition.Variables.Select(v => v.Nom));
        Assert.True(definition.Declarations["nbJours"].Obligatoire);
        var sousProcessus = Assert.IsType<NoeudSousProcessus>(definition.Noeud("enfant"));
        Assert.Equal("enfant-fictif", sousProcessus.CleProcessus);
    }

    [Fact]
    public void Sans_attribut_la_cle_est_le_nom_de_la_classe()
    {
        var definition = ((IDefinitionSentier)new ProcessusSansAttribut()).Definir();

        Assert.Equal(nameof(ProcessusSansAttribut), definition.CleProcessus);
        Assert.Equal(nameof(ProcessusSansAttribut), definition.Nom);
    }

    [Fact]
    public void Toutes_les_definitions_de_l_assembly_se_construisent()
    {
        var types = typeof(DemandeFictive).Assembly.GetTypes()
            .Where(t => typeof(DefinitionSentier).IsAssignableFrom(t) && !t.IsAbstract)
            .ToList();

        Assert.NotEmpty(types);
        foreach (var type in types)
        {
            ((IDefinitionSentier)Activator.CreateInstance(type)!).Definir();
        }
    }

    [Fact]
    public void Trouver_un_noeud_inexistant_retourne_null_ou_leve_une_erreur()
    {
        var definition = ConstructeurProcessus.Nouveau("p").Fin("fin").Construire();

        Assert.Null(definition.TrouverNoeud("absent"));
        Assert.Throws<SentierException>(() => definition.Noeud("absent"));
    }

    [Fact]
    public void Les_conditions_sur_variables_acceptent_tous_les_operateurs_applicables()
    {
        var definition = ConstructeurProcessus.Nouveau("conditions")
            .Variables(Montant, Urgent, Commentaire)
            .Decision("aiguiller", d => d
                .Si(Montant).SuperieurOuEgalA(10m).Vers("fin")
                .Si(Urgent).EstVrai().Vers("fin")
                .Si(Commentaire).Contient("x").Vers("fin")
                .Si(Commentaire).CommencePar("y").Vers("fin")
                .Si(Commentaire).EstRenseignee().Vers("fin")
                .Si(Montant).EgalA(Montant).Nommee("egal").Vers("fin")
                .Sinon("fin"))
            .Fin("fin")
            .Construire();

        Assert.Equal(7, definition.Sorties("aiguiller").Count);
    }
}
