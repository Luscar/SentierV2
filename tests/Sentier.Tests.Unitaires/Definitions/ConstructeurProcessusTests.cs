using Sentier.Definitions;
using Sentier.Tests.Unitaires.Support;
using Sentier.Variables;

namespace Sentier.Tests.Unitaires.Definitions;

public class ConstructeurProcessusTests
{
    private static readonly Variable<long> Compteur = Variable.Entier("compteur").ParDefaut(0);
    private static readonly Variable<string> Responsable = Variable.Texte("responsable");
    private static readonly Variable<DateTime> Limite = Variable.Date("limite");
    private static readonly Variable<long> Dossier = Variable.Entier("dossier");

    private static IEnumerable<(string Source, string Destination, string? Sortie)> Liens(DefinitionProcessus d)
        => d.Transitions.Select(t => (t.Source, t.Destination, t.NomSortie));

    [Fact]
    public void La_cle_est_obligatoire()
        => Assert.Throws<ArgumentException>(() => ConstructeurProcessus.Nouveau(" "));

    [Fact]
    public void Le_nom_vaut_la_cle_par_defaut()
    {
        var definition = ConstructeurProcessus.Nouveau("cle").Fin("fin").Construire();
        Assert.Equal("cle", definition.Nom);
        Assert.Equal("Joli nom", ConstructeurProcessus.Nouveau("cle", "Joli nom").Fin("fin").Construire().Nom);
    }

    [Fact]
    public void Le_debut_est_implicite_et_les_noeuds_s_enchainent()
    {
        var definition = ConstructeurProcessus.Nouveau("p")
            .Systemique("a", "tracer")
            .Systemique("b", "tracer")
            .Fin("fin")
            .Construire();

        Assert.Equal(["debut", "a", "b", "fin"], definition.Noeuds.Select(n => n.Code));
        Assert.Equal("debut", definition.NoeudDebut.Code);
        Assert.Equal([("debut", "a", null), ("a", "b", null), ("b", "fin", null)], Liens(definition));
        Assert.True(definition.InstanceUnique);
        Assert.False(definition.MigrationAutomatique);
        Assert.Equal(["fin"], definition.CodesFin);
    }

    [Fact]
    public void Un_debut_explicite_change_son_code()
    {
        var definition = ConstructeurProcessus.Nouveau("p").Debut("depart").Fin("fin").Construire();
        Assert.Equal("depart", definition.NoeudDebut.Code);
    }

    [Fact]
    public void Un_debut_explicite_doit_etre_le_premier_noeud()
    {
        var ex = Assert.Throws<DefinitionInvalideException>(() => ConstructeurProcessus.Nouveau("p").Systemique("a", "tracer").Debut().Fin("fin").Construire());
        Assert.Contains(ex.Erreurs, e => e.Contains("premier nœud"));
    }

    [Fact]
    public void Options_du_processus()
    {
        var definition = ConstructeurProcessus.Nouveau("p").InstancesMultiples().MigrationAutomatique().Fin("fin").Construire();
        Assert.False(definition.InstanceUnique);
        Assert.True(definition.MigrationAutomatique);
    }

    [Fact]
    public void Declaration_des_variables()
    {
        var definition = ConstructeurProcessus.Nouveau("p")
            .Variables(Compteur, Responsable)
            .Variables(Compteur)
            .Variable("note", TypeVariable.Decimal, obligatoire: true, defaut: 1)
            .Fin("fin")
            .Construire();

        Assert.Equal(["compteur", "responsable", "note"], definition.Variables.Select(v => v.Nom));
        Assert.Equal(1m, definition.Declarations["note"].Defaut!.VersObjet());
        Assert.True(definition.Declarations["note"].Obligatoire);
    }

    [Fact]
    public void Une_variable_redeclaree_differemment_est_une_erreur()
    {
        var ex = Assert.Throws<DefinitionInvalideException>(() => ConstructeurProcessus.Nouveau("p")
            .Variables(Compteur)
            .Variable("compteur", TypeVariable.Texte)
            .Fin("fin")
            .Construire());
        Assert.Contains(ex.Erreurs, e => e.Contains("de façons différentes"));
    }

    [Fact]
    public void Decision_en_style_imbrique_et_plat()
    {
        var definition = ConstructeurProcessus.Nouveau("p")
            .Decision("choix", d => d
                .Si("vrai", p => p.Valeur("valeur", true)).Alors(b => b.Systemique("oui", "tracer"))
                .SiPas("vrai", p => p.Valeur("valeur", false)).Vers("fin-non")
                .Sinon())
            .Fin("fin")
            .Fin("fin-non")
            .Construire();

        var sorties = definition.Sorties("choix");
        Assert.Equal(3, sorties.Count);
        Assert.Equal(("vrai", "oui"), (sorties[0].NomSortie, sorties[0].Destination));
        Assert.False(sorties[0].Requete!.Inverser);
        Assert.Equal(("non-vrai", "fin-non"), (sorties[1].NomSortie, sorties[1].Destination));
        Assert.True(sorties[1].Requete!.Inverser);
        Assert.Equal(("sinon", "fin"), (sorties[2].NomSortie, sorties[2].Destination));
        Assert.True(sorties[2].EstSinon);

        // Le bloc imbriqué rejoint le nœud qui suit la décision.
        Assert.Contains(("oui", "fin", null), Liens(definition));
    }

    [Fact]
    public void Decision_avec_requete_typee_et_nom_de_sortie()
    {
        var definition = ConstructeurProcessus.Nouveau("p")
            .Variables(Compteur)
            .Decision("choix", d => d
                .Si<CompteurAtteint>(p => p.Variable(Compteur).Valeur("seuil", 3)).Nommee("atteint")
                .Sinon(b => b.Fin("non")))
            .Fin("oui")
            .Construire();

        var branche = definition.Sorties("choix")[0];
        Assert.Equal("atteint", branche.NomSortie);
        Assert.Equal("CompteurAtteint", branche.Requete!.Nom);
        Assert.Equal(["compteur", "seuil"], branche.Requete.Parametres.Select(p => p.Nom));
        Assert.Equal("non", definition.Transitions.Single(t => t.Source == "choix" && t.EstSinon).Destination);
    }

    [Fact]
    public void Une_branche_sans_declaration_prealable_est_une_erreur()
    {
        var ex = Assert.Throws<DefinitionInvalideException>(() => ConstructeurProcessus.Nouveau("p")
            .Decision("choix", d => d.Vers("fin").Si("vrai").Sinon())
            .Fin("fin")
            .Construire());
        Assert.Contains(ex.Erreurs, e => e.Contains("doit suivre la déclaration d'une branche"));
    }

    [Fact]
    public void Une_branche_a_deux_destinations_est_une_erreur()
    {
        var ex = Assert.Throws<DefinitionInvalideException>(() => ConstructeurProcessus.Nouveau("p")
            .Decision("choix", d => d.Si("vrai").Vers("fin").Vers("fin").Sinon())
            .Fin("fin")
            .Construire());
        Assert.Contains(ex.Erreurs, e => e.Contains("a déjà une destination"));
    }

    [Fact]
    public void Attente_avec_echeance_signaux_et_boucle()
    {
        var definition = ConstructeurProcessus.Nouveau("p")
            .Variables(Dossier)
            .Attente("attendre", a => a
                .CommandeEntree("tracer")
                .Signal("VALIDATION", s => s.Correlation(Dossier))
                .Signal("REFUS", s => s.Correlation("fixe").SansFusion()).Nommee("refus").Alors(b => b.Fin("refusee"))
                .Echeance(TimeSpan.FromDays(2)).Alors(b => b.Systemique("relancer", "tracer").Vers("attendre")))
            .Fin("validee")
            .Construire();

        var attente = Assert.IsType<NoeudAttente>(definition.Noeud("attendre"));
        Assert.True(attente.EstSuspensif);
        Assert.Single(attente.CommandesEntree);
        Assert.Equal(["VALIDATION", "refus", "echeance"], attente.Declencheurs.Select(d => d.NomSortie));

        var validation = Assert.IsType<DeclencheurSignal>(attente.Declencheurs[0]);
        Assert.Equal("dossier", validation.VariableCorrelation);
        Assert.True(validation.FusionnerChargeUtile);

        var refus = Assert.IsType<DeclencheurSignal>(attente.Declencheurs[1]);
        Assert.Equal("fixe", refus.CleCorrelation);
        Assert.False(refus.FusionnerChargeUtile);

        Assert.Equal(TimeSpan.FromDays(2), Assert.IsType<DeclencheurEcheance>(attente.Declencheurs[2]).Duree);

        Assert.Contains(("attendre", "validee", "VALIDATION"), Liens(definition));
        Assert.Contains(("attendre", "refusee", "refus"), Liens(definition));
        Assert.Contains(("attendre", "relancer", "echeance"), Liens(definition));
        Assert.Contains(("relancer", "attendre", null), Liens(definition));
    }

    [Fact]
    public void Raccourcis_d_attente_et_echeances_par_date_ou_variable()
    {
        var date = new DateTime(2027, 1, 1, 0, 0, 0, DateTimeKind.Unspecified);
        var definition = ConstructeurProcessus.Nouveau("p")
            .Variables(Limite)
            .AttendreDelai("delai", TimeSpan.FromHours(1))
            .AttendreSignal("signal", "GO")
            .Attente("date", a => a.Echeance(date))
            .Attente("variable", a => a.Echeance(Limite))
            .Fin("fin")
            .Construire();

        Assert.Equal(TimeSpan.FromHours(1), ((DeclencheurEcheance)((NoeudAttente)definition.Noeud("delai")).Declencheurs.Single()).Duree);
        Assert.Equal("GO", ((DeclencheurSignal)((NoeudAttente)definition.Noeud("signal")).Declencheurs.Single()).NomSignal);
        var absolue = ((DeclencheurEcheance)((NoeudAttente)definition.Noeud("date")).Declencheurs.Single()).DateAbsolue!.Value;
        Assert.Equal(DateTimeKind.Utc, absolue.Kind);
        Assert.Equal("limite", ((DeclencheurEcheance)((NoeudAttente)definition.Noeud("variable")).Declencheurs.Single()).Variable);
    }

    [Fact]
    public void Interactif_complet()
    {
        var definition = ConstructeurProcessus.Nouveau("p")
            .Variables(Responsable, Limite, Compteur)
            .Interactif("tache", "Traiter le dossier", i => i
                .Role("agent")
                .Assigne(Responsable)
                .Echeance(Limite)
                .Donnees(Compteur)
                .Donnees(p => p.Valeur("source", "web").CleAffaire("dossier"))
                .CommandeEntree("tracer")
                .CommandeSortie<Tracer>(p => p.Valeur("message", "fin")))
            .Interactif("simple")
            .Interactif("nomme", "Nom de tâche")
            .Interactif("fixe", i => i.Assigne("alice").Echeance(TimeSpan.FromDays(1)))
            .Fin("fin")
            .Construire();

        var tache = Assert.IsType<NoeudInteractif>(definition.Noeud("tache"));
        Assert.Equal("Traiter le dossier", tache.NomTache);
        Assert.Equal("agent", tache.Role);
        Assert.Equal(("responsable", true), (tache.Assigne!.Variable, tache.Assigne.Optionnel));
        Assert.Equal("limite", tache.VariableEcheance);
        Assert.Equal(["compteur", "source", "dossier"], tache.Donnees.Select(d => d.Nom));
        Assert.Equal("tracer", tache.CommandesEntree.Single().Nom);
        Assert.Equal("tracer", tache.CommandesSortie.Single().Nom);

        Assert.Equal("simple", ((NoeudInteractif)definition.Noeud("simple")).NomTache);
        Assert.Equal("Nom de tâche", ((NoeudInteractif)definition.Noeud("nomme")).NomTache);
        var fixe = (NoeudInteractif)definition.Noeud("fixe");
        Assert.Equal("alice", fixe.Assigne!.Valeur!.VersObjet());
        Assert.Equal(TimeSpan.FromDays(1), fixe.DureeEcheance);
    }

    [Fact]
    public void Sous_processus_avec_mappages_et_branches_de_fin()
    {
        var enfantCompteur = Variable.Entier("compteur");
        var definition = ConstructeurProcessus.Nouveau("parent")
            .Variables(Compteur, Dossier)
            .SousProcessus("enfant", "processus-enfant", s => s
                .CleAffaire(Dossier)
                .Entree(enfantCompteur, Compteur)
                .EntreeValeur(Variable.Texte("origine"), "parent")
                .EntreeCleAffaire(Variable.Entier("cleParent"))
                .Sortie(enfantCompteur, Compteur)
                .SiFin("rejete", b => b.Fin("rejet"))
                .SiAnnule("annulation"))
            .Fin("ok")
            .Fin("annulation")
            .Construire();

        var noeud = Assert.IsType<NoeudSousProcessus>(definition.Noeud("enfant"));
        Assert.Equal("processus-enfant", noeud.CleProcessus);
        Assert.Equal("dossier", noeud.VariableCleAffaire);
        Assert.Equal([SourceParametre.Variable, SourceParametre.Statique, SourceParametre.CleAffaire], noeud.Entrees.Select(e => e.Source));
        Assert.Equal(("compteur", "compteur"), (noeud.Sorties.Single().VariableEnfant, noeud.Sorties.Single().VariableParent));

        Assert.Contains(("enfant", "rejet", "rejete"), Liens(definition));
        Assert.Contains(("enfant", "annulation", "annule"), Liens(definition));
        Assert.Contains(("enfant", "ok", null), Liens(definition));
    }

    [Fact]
    public void Sous_processus_par_classe()
    {
        var definition = ConstructeurProcessus.Nouveau("p").SousProcessus<ArchivageTest>("archiver").Fin("fin").Construire();
        Assert.Equal("ArchivageTest", ((NoeudSousProcessus)definition.Noeud("archiver")).CleProcessus);
    }

    [Fact]
    public void Un_sous_processus_sans_sortie_est_une_erreur()
    {
        var ex = Assert.Throws<DefinitionInvalideException>(() => ConstructeurProcessus.Nouveau("p")
            .Fin("fin")
            .SousProcessus("enfant", "x")
            .Construire());
        Assert.Contains(ex.Erreurs, e => e.Contains("'enfant'") && e.Contains("n'a pas de sortie"));
    }

    [Fact]
    public void Vers_et_libelle()
    {
        var definition = ConstructeurProcessus.Nouveau("p")
            .Systemique("a", "tracer").Libelle("Étape A").Vers("c")
            .Systemique("b", "tracer").Vers("fin")
            .Systemique("c", "tracer").Vers("b")
            .Fin("fin")
            .Construire();

        Assert.Equal("Étape A", definition.Noeud("a").Nom);
        Assert.Equal([("debut", "a", null), ("a", "c", null), ("b", "fin", null), ("c", "b", null)], Liens(definition));
    }

    [Fact]
    public void Un_noeud_ne_peut_avoir_qu_un_lien()
    {
        var ex = Assert.Throws<DefinitionInvalideException>(() => ConstructeurProcessus.Nouveau("p")
            .Systemique("a", "tracer").Vers("fin").Vers("fin")
            .Fin("fin")
            .Construire());
        Assert.Contains(ex.Erreurs, e => e.Contains("a déjà un lien"));
    }

    [Fact]
    public void Une_fin_ne_peut_pas_avoir_de_lien()
    {
        var ex = Assert.Throws<DefinitionInvalideException>(() => ConstructeurProcessus.Nouveau("p").Fin("fin").Vers("fin").Construire());
        Assert.Contains(ex.Erreurs, e => e.Contains("ne peut pas avoir de sortie"));
    }

    [Fact]
    public void Un_processus_vide_est_invalide()
    {
        var ex = Assert.Throws<DefinitionInvalideException>(() => ConstructeurProcessus.Nouveau("p").Construire());
        Assert.Contains(ex.Erreurs, e => e.Contains("aucun nœud"));
        Assert.Equal("p", ex.CleProcessus);
    }

    [Fact]
    public void Un_code_en_double_est_une_erreur()
    {
        var ex = Assert.Throws<DefinitionInvalideException>(() => ConstructeurProcessus.Nouveau("p").Systemique("a", "tracer").Systemique("a", "tracer").Fin("fin").Construire());
        Assert.Contains(ex.Erreurs, e => e.Contains("'a' est utilisé plusieurs fois"));
    }

    [Fact]
    public void Un_dernier_noeud_sans_sortie_est_une_erreur()
    {
        var ex = Assert.Throws<DefinitionInvalideException>(() => ConstructeurProcessus.Nouveau("p").Fin("fin").Systemique("a", "tracer").Construire());
        Assert.Contains(ex.Erreurs, e => e.Contains("'a' n'a pas de sortie"));
    }

    [Fact]
    public void Un_noeud_inatteignable_est_une_erreur()
    {
        var ex = Assert.Throws<DefinitionInvalideException>(() => ConstructeurProcessus.Nouveau("p")
            .Fin("fin")
            .Systemique("orphelin", "tracer").Vers("fin")
            .Construire());
        Assert.Contains(ex.Erreurs, e => e.Contains("'orphelin' n'est jamais atteint"));
    }

    [Fact]
    public void Parametres_de_tous_les_types_de_source()
    {
        var optionnelle = Variable.Texte("commentaire");
        var definition = ConstructeurProcessus.Nouveau("p")
            .Variables(Compteur, optionnelle)
            .Systemique("a", "tracer", p => p
                .Valeur("statique", 1.5m)
                .Variable(Compteur)
                .Variable("alias", Compteur)
                .Variable("parNom", "compteur")
                .Variables(Compteur, optionnelle)
                .VariableOptionnelle(optionnelle)
                .VariableOptionnelle("avecDefaut", optionnelle, "rien")
                .CleAffaire("cle"))
            .Fin("fin");

        // Les deux « Variables » redéclarent « compteur » : la validation le signale.
        var ex = Assert.Throws<DefinitionInvalideException>(() => definition.Construire());
        Assert.Contains(ex.Erreurs, e => e.Contains("'compteur'") && e.Contains("plusieurs fois"));
    }

    [Fact]
    public void Parametres_resolus_dans_l_appel()
    {
        var optionnelle = Variable.Texte("commentaire");
        var definition = ConstructeurProcessus.Nouveau("p")
            .Variables(Compteur, optionnelle)
            .Systemique("a", "tracer", p => p
                .Valeur("statique", 1.5m)
                .Variable("alias", Compteur)
                .VariableOptionnelle(optionnelle)
                .VariableOptionnelle("avecDefaut", optionnelle, "rien")
                .CleAffaire("cle"))
            .Fin("fin")
            .Construire();

        var parametres = ((NoeudSystemique)definition.Noeud("a")).Commande.Parametres;
        Assert.Equal(SourceParametre.Statique, parametres[0].Source);
        Assert.Equal(TypeVariable.Decimal, parametres[0].Valeur!.Type);
        Assert.Equal(("alias", "compteur"), (parametres[1].Nom, parametres[1].Variable));
        Assert.True(parametres[2].Optionnel);
        Assert.Equal("rien", parametres[3].Defaut!.VersObjet());
        Assert.Equal(SourceParametre.CleAffaire, parametres[4].Source);
    }

    [Fact]
    public void Systemique_par_type_de_commande()
    {
        var definition = ConstructeurProcessus.Nouveau("p").Systemique<EmettreFacture>("facturer").Fin("fin").Construire();
        Assert.Equal("EmettreFacture", ((NoeudSystemique)definition.Noeud("facturer")).Commande.Nom);
    }
}
