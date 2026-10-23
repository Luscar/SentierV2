using System.Text.Json;
using Sentier.Commandes;
using Sentier.Definitions;
using Sentier.Tests.Unitaires.Support;

namespace Sentier.Tests.Unitaires.Commandes;

public class RegistreSentierTests
{
    private static readonly RegistreSentier Registre = RegistreSentier.Balayer([typeof(RegistreSentierTests).Assembly]);

    [Fact]
    public void Les_commandes_sont_nommees_par_attribut_ou_par_classe()
    {
        Assert.Equal(typeof(Tracer), Registre.TypeCommande("tracer"));
        Assert.Equal(typeof(EmettreFacture), Registre.TypeCommande("EmettreFacture"));
        Assert.Null(Registre.TypeCommande("Tracer"));
        Assert.Null(Registre.TypeCommande("inconnue"));
    }

    [Fact]
    public void Les_requetes_sont_nommees_par_attribut_ou_par_classe()
    {
        Assert.Equal(typeof(ParametreVrai), Registre.TypeRequete("vrai"));
        Assert.Equal(typeof(CompteurAtteint), Registre.TypeRequete("CompteurAtteint"));
        Assert.Null(Registre.TypeRequete("tracer"));
    }

    [Fact]
    public void Definitions_et_plans_sont_decouverts()
    {
        Assert.Contains(typeof(DemandeCongeTest), Registre.Definitions);
        Assert.Contains(typeof(DefinitionDynamique), Registre.Definitions);
        Assert.DoesNotContain(typeof(DefinitionSentier), Registre.Definitions);
        Assert.Contains(typeof(PlanDynamique), Registre.PlansMigration);
    }

    [Fact]
    public void Les_decorateurs_de_gestionnaire_de_taches_sont_ignores()
    {
        Assert.Contains(typeof(GestionnaireTachesTest), Registre.GestionnairesTaches);
        Assert.Contains(typeof(AutreGestionnaireTaches), Registre.GestionnairesTaches);
        Assert.DoesNotContain(typeof(DecorateurTaches), Registre.GestionnairesTaches);
    }

    [Fact]
    public void Un_assembly_balaye_deux_fois_ne_cree_pas_de_doublon()
    {
        var assembly = typeof(RegistreSentierTests).Assembly;
        var registre = RegistreSentier.Balayer([assembly, assembly]);
        Assert.Equal(Registre.Commandes.Count, registre.Commandes.Count);
        Assert.Equal(Registre.Definitions.Count, registre.Definitions.Count);
    }

    [Fact]
    public void Deux_commandes_de_meme_nom_sont_refusees()
    {
        var doublon = AssemblyDynamique.AvecCommande("tracer");
        var ex = Assert.Throws<SentierException>(() => RegistreSentier.Balayer([typeof(RegistreSentierTests).Assembly, doublon]));
        Assert.Contains("'tracer'", ex.Message);
    }

    [Fact]
    public void Resolution_des_noms()
    {
        Assert.Equal("tracer", NomsSentier.NomCommande(typeof(Tracer)));
        Assert.Equal("vrai", NomsSentier.NomRequete(typeof(ParametreVrai)));
        Assert.Equal("demande-conge", NomsSentier.CleProcessus(typeof(DemandeCongeTest)));
        Assert.Equal("ArchivageTest", NomsSentier.CleProcessus(typeof(ArchivageTest)));
    }
}

public class ParametresResolusTests
{
    private static ParametresResolus Parametres(Dictionary<string, object?> valeurs) => new("Commande 'x'", valeurs);

    [Fact]
    public void Lecture_et_conversion()
    {
        var p = Parametres(new() { ["entier"] = 5L, ["texte"] = "a", ["nul"] = null });
        Assert.Equal(5, p.Lire<int>("entier"));
        Assert.Equal("a", p.Lire<string>("texte"));
        Assert.True(p.Contient("entier"));
        Assert.False(p.Contient("nul"));
        Assert.False(p.Contient("absent"));
        Assert.Equal(9L, p.LireOuDefaut("absent", 9L));
        Assert.Equal(9L, p.LireOuDefaut("nul", 9L));
        Assert.Equal(3, p.Valeurs.Count);
    }

    [Fact]
    public void Un_parametre_absent_ou_mal_type_est_signale_avec_l_appel()
    {
        var p = Parametres(new() { ["texte"] = "abc" });
        var absent = Assert.Throws<ParametreInvalideException>(() => p.Lire<long>("absent"));
        Assert.Contains("Commande 'x'", absent.Message);
        Assert.Throws<ParametreInvalideException>(() => p.Lire<int>("texte"));
        Assert.Throws<ParametreInvalideException>(() => p.LireOuDefaut<int>("texte"));
    }

    [Fact]
    public void Lier_par_nom_de_propriete_sans_casse()
    {
        var p = Parametres(new() { ["numero"] = 12L, ["MONTANT"] = 4.5m, ["client"] = "ACME", ["urgente"] = null, ["inconnu"] = 1 });
        var facture = p.Lier<ParametresFacture>();
        Assert.Equal(12L, facture.Numero);
        Assert.Equal(4.5m, facture.Montant);
        Assert.Equal("ACME", facture.Client);
        Assert.False(facture.Urgente);
    }

    [Fact]
    public void Lier_signale_un_type_incompatible()
        => Assert.Throws<ParametreInvalideException>(() => Parametres(new() { ["numero"] = "douze" }).Lier<ParametresFacture>());

    [Fact]
    public void Lier_accepte_un_element_json()
    {
        var p = Parametres(new() { ["client"] = JsonSerializer.SerializeToElement("Bob") });
        Assert.Equal("Bob", p.Lier<ParametresFacture>().Client);
    }
}
