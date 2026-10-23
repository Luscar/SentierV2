using Sentier.Definitions;
using Sentier.Tests.Unitaires.Support;
using Sentier.Variables;

namespace Sentier.Tests.Unitaires.Definitions;

public class SerialisationEtExportTests
{
    private static readonly Variable<long> Dossier = Variable.Entier("dossier").Obligatoire();
    private static readonly Variable<string> Agent = Variable.Texte("agent").ParDefaut("inconnu");

    private static DefinitionProcessus Complete() => ConstructeurProcessus.Nouveau("complet", "Processus \"complet\"")
        .Variables(Dossier, Agent)
        .Systemique("preparer", "tracer", p => p.Valeur("message", "début").CleAffaire("dossier"))
        .Decision("verifier", d => d
            .SiPas("vrai", p => p.Valeur("valeur", false)).Nommee("rejet").Alors(b => b.Fin("rejete"))
            .Si("vrai", p => p.Valeur("valeur", true))
            .Sinon())
        .Attente("attendre", a => a
            .Signal("GO", s => s.Correlation(Dossier))
            .Echeance(TimeSpan.FromDays(1) + TimeSpan.FromHours(2) + TimeSpan.FromMinutes(3)).Alors(b => b.Fin("expire")))
        .Attente("date", a => a.Echeance(new DateTime(2027, 1, 2, 3, 4, 0, DateTimeKind.Utc)))
        .Interactif("traiter", "Traiter | réviser", i => i.Role("agent").Assigne(Agent).Donnees(Dossier))
        .SousProcessus<ArchivageTest>("archiver")
        .Fin("termine")
        .Construire();

    [Fact]
    public void Aller_retour_json_identique()
    {
        var definition = Complete();
        var json = SerialisationDefinition.Serialiser(definition);
        var relue = SerialisationDefinition.Deserialiser(json);

        Assert.Equal(json, SerialisationDefinition.Serialiser(relue));
        Assert.Equal(definition.Noeuds.Select(n => (n.Code, n.Type)), relue.Noeuds.Select(n => (n.Code, n.Type)));
        Assert.Equal(definition.Transitions.Count, relue.Transitions.Count);
        Assert.Equal("inconnu", relue.Declarations["agent"].Defaut!.VersObjet());
        Assert.Null(relue.Id);
        Assert.Equal(0, relue.Version);
    }

    [Fact]
    public void Le_json_utilise_des_discriminants_et_du_camel_case()
    {
        var json = SerialisationDefinition.Serialiser(Complete());
        Assert.Contains("\"type\":\"SYSTEMIQUE\"", json);
        Assert.Contains("\"type\":\"SOUS_PROCESSUS\"", json);
        Assert.Contains("\"type\":\"ECHEANCE\"", json);
        Assert.Contains("\"cleProcessus\":\"complet\"", json);
        Assert.Contains("\"source\":\"CleAffaire\"", json);
        Assert.DoesNotContain("\"variableCorrelation\":null", json);
    }

    [Fact]
    public void Serialiser_un_noeud_ou_un_declencheur()
    {
        var definition = Complete();
        Assert.StartsWith("{\"type\":\"ATTENTE\"", SerialisationDefinition.Serialiser(definition.Noeud("attendre")));
        Assert.StartsWith("{\"type\":\"SIGNAL\"", SerialisationDefinition.Serialiser(((NoeudAttente)definition.Noeud("attendre")).Declencheurs[0]));
    }

    [Fact]
    public void Un_contenu_vide_est_refuse()
        => Assert.Throws<SentierException>(() => SerialisationDefinition.Deserialiser("null"));

    [Fact]
    public void L_empreinte_est_stable_et_sensible_au_contenu()
    {
        var a = SerialisationDefinition.CalculerEmpreinte(SerialisationDefinition.Serialiser(Complete()));
        var b = SerialisationDefinition.CalculerEmpreinte(SerialisationDefinition.Serialiser(Complete()));
        var autre = SerialisationDefinition.CalculerEmpreinte(SerialisationDefinition.Serialiser(ConstructeurProcessus.Nouveau("complet").Fin("fin").Construire()));

        Assert.Equal(a, b);
        Assert.NotEqual(a, autre);
        Assert.Equal(64, a.Length);
    }

    [Fact]
    public void Acces_aux_noeuds_et_sorties()
    {
        var definition = Complete();
        Assert.Null(definition.TrouverNoeud("absent"));
        Assert.Throws<SentierException>(() => definition.Noeud("absent"));
        Assert.Empty(definition.Sorties("termine"));
        Assert.Equal("rejete", definition.Sortie("verifier", "rejet")!.Destination);
        Assert.Equal("attendre", definition.Sortie("verifier", "vrai")!.Destination);
        Assert.Null(definition.Sortie("verifier", "inconnue"));
        Assert.Equal("verifier", definition.Sortie("preparer", null)!.Destination);
        Assert.Equal("verifier", definition.Sortie("preparer", "nimporte")!.Destination);
        Assert.Equal(["rejete", "expire", "termine"], definition.CodesFin);
    }

    [Fact]
    public void Un_noeud_de_debut_absent_est_signale()
    {
        var definition = new DefinitionProcessus { CleProcessus = "p", Nom = "p" };
        Assert.Throws<DefinitionInvalideException>(() => definition.NoeudDebut);
    }

    [Fact]
    public void Export_mermaid()
    {
        var mermaid = ExportateurMermaid.Exporter(Complete());

        Assert.StartsWith("flowchart TD", mermaid);
        Assert.Contains("%% Processus #quot;complet#quot; (complet)", mermaid);
        Assert.Contains("n_debut((\"debut\"))", mermaid);
        Assert.Contains("n_termine(((\"termine\")))", mermaid);
        Assert.Contains("n_verifier{\"verifier\"}", mermaid);
        Assert.Contains("n_preparer[\"preparer<br/><small>tracer</small>\"]", mermaid);
        Assert.Contains("n_attendre{{\"attendre<br/><small>signal GO #124; délai 1 j 2 h 3 min</small>\"}}", mermaid);
        Assert.Contains("le 2027-01-02 03:04", mermaid);
        Assert.Contains("tâche : Traiter #124; réviser", mermaid);
        Assert.Contains("n_archiver[[\"archiver<br/><small>ArchivageTest</small>\"]]", mermaid);
        Assert.Contains("n_verifier -->|rejet : non vrai| n_rejete", mermaid);
        Assert.Contains("n_verifier -->|vrai| n_attendre", mermaid);
        Assert.Contains("n_debut --> n_preparer", mermaid);
        Assert.Contains("class n_attendre,n_date,n_traiter,n_archiver suspensif;", mermaid);
        Assert.Contains("class n_debut,n_rejete,n_expire,n_termine extremite;", mermaid);
    }

    [Fact]
    public void Export_mermaid_omet_la_sortie_par_defaut_qui_double_une_branche()
    {
        var definition = ConstructeurProcessus.Nouveau("p")
            .SousProcessus("archiver", "ArchivageTest", s => s.SiFin("archive"))
            .Fin("fin")
            .Construire();
        var mermaid = ExportateurMermaid.Exporter(definition);
        Assert.Contains("n_archiver -->|archive| n_fin", mermaid);
        Assert.DoesNotContain("n_archiver --> n_fin", mermaid);
    }

    [Fact]
    public void Export_mermaid_d_une_version_deployee_et_des_codes_speciaux()
    {
        var definition = ConstructeurProcessus.Nouveau("p").Systemique("étape 1", "tracer").AttendreDelai("court", TimeSpan.FromSeconds(30)).Fin("fin").Construire();
        definition.Version = 3;
        var mermaid = ExportateurMermaid.Exporter(definition);
        Assert.Contains("(p v3)", mermaid);
        Assert.Contains("n__tape_1", mermaid);
        Assert.Contains("délai 30 s", mermaid);
    }
}
