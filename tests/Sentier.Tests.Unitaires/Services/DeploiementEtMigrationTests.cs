using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Sentier.Definitions;
using Sentier.Migration;
using Sentier.Tests.Unitaires.Support;
using Sentier.Variables;

namespace Sentier.Tests.Unitaires.Services;

/// <summary>
/// Déploiement des définitions découvertes et migration des instances. La définition « dynamique »
/// change d'une version à l'autre par <see cref="ConfigurationDynamique"/>.
/// </summary>
public class DeploiementEtMigrationTests : IDisposable
{
    private const string Cle = DefinitionDynamique.Cle;
    private static readonly Variable<long> Montant = Variable.Entier("montant");
    private static readonly Variable<string> Note = Variable.Texte("note");

    private readonly HoteMemoire _hote = new();

    public void Dispose() => _hote.Dispose();

    private void Definir(Func<ConstructeurProcessus, ConstructeurProcessus> definition, bool migrationAutomatique = false)
        => _hote.Configuration.Definition = () =>
        {
            var constructeur = ConstructeurProcessus.Nouveau(Cle);
            if (migrationAutomatique)
            {
                constructeur.MigrationAutomatique();
            }

            return definition(constructeur).Construire();
        };

    private Task<RapportDeploiement> Deployer() => _hote.Sentier(s => s.DeployerDefinitionsAsync());

    private async Task<ResultatDeploiement> DeployerDynamique() => (await Deployer()).Definitions.Single(d => d.CleProcessus == Cle);

    [Fact]
    public async Task Le_premier_deploiement_cree_une_version_par_definition_decouverte()
    {
        var rapport = await Deployer();

        Assert.Equal(["ArchivageTest", "demande-conge", Cle], rapport.Definitions.Select(d => d.CleProcessus).Order());
        Assert.All(rapport.Definitions, d => Assert.Equal((EtatDeploiement.NouvelleVersion, 1), (d.Etat, d.Version)));
        Assert.Equal(3, _hote.Base.Definitions.Lignes.Count);
    }

    [Fact]
    public async Task Une_empreinte_identique_ne_cree_pas_de_version()
    {
        await Deployer();
        var rapport = await Deployer();

        Assert.All(rapport.Definitions, d => Assert.Equal((EtatDeploiement.Inchangee, 1), (d.Etat, d.Version)));
        Assert.Equal(3, _hote.Base.Definitions.Lignes.Count);
    }

    [Fact]
    public async Task Un_changement_cree_une_nouvelle_version_active()
    {
        await Deployer();
        Definir(p => p.Systemique("tracer", "tracer").Systemique("nouveau", "tracer").Fin("fin"));

        var resultat = await DeployerDynamique();

        Assert.Equal((EtatDeploiement.NouvelleVersion, 2), (resultat.Etat, resultat.Version));
        var lignes = _hote.Base.Definitions.Lignes.Where(l => l.CleProcessus == Cle).OrderBy(l => l.Version).ToList();
        Assert.Equal([false, true], lignes.Select(l => l.EstActive));
        Assert.NotEqual(lignes[0].Empreinte, lignes[1].Empreinte);
        Assert.Contains("(dynamique v2)", await _hote.Suivi(s => s.ExporterDiagrammeAsync(Cle)));
    }

    [Fact]
    public async Task Le_deploiement_demarre_une_definition_decouverte()
    {
        await Deployer();
        var refusee = await _hote.DemarrerAsync("demande-conge", 1, ValeursVariables.Avec(DemandeCongeTest.NbJours, 20L));
        var acceptee = await _hote.DemarrerAsync("demande-conge", 2, ValeursVariables.Avec(DemandeCongeTest.NbJours, 5L));

        Assert.Equal("refusee", refusee.CodeFin);
        Assert.Equal("approbation", acceptee.CodeNoeudCourant);
        Assert.Equal("Approuver la demande", _hote.Bassin.Tache(acceptee.IdTacheCourante!.Value).Demande.Nom);
    }

    [Fact]
    public async Task Une_definition_invalide_bloque_tout_le_deploiement()
    {
        Definir(p => p.Systemique("a", "commande-inconnue").Decision("d", d => d.Si("requete-inconnue").Sinon()).Fin("fin"));

        var ex = await Assert.ThrowsAsync<DefinitionInvalideException>(Deployer);

        Assert.Contains(ex.Erreurs, e => e.Contains("[dynamique] La commande 'commande-inconnue'"));
        Assert.Contains(ex.Erreurs, e => e.Contains("La requête 'requete-inconnue'"));
        Assert.Empty(_hote.Base.Definitions.Lignes);
    }

    [Fact]
    public async Task Une_erreur_de_construction_est_rapportee()
    {
        _hote.Configuration.Definition = () => ConstructeurProcessus.Nouveau(Cle).Construire();
        var ex = await Assert.ThrowsAsync<DefinitionInvalideException>(Deployer);
        Assert.Contains(ex.Erreurs, e => e.StartsWith("[dynamique]") && e.Contains("aucun nœud"));
    }

    [Fact]
    public async Task Un_sous_processus_vers_un_processus_inexistant_ou_mal_mappe()
    {
        Definir(p => p.SousProcessus("a", "inexistant").SousProcessus("b", "demande-conge").Fin("fin"));

        var ex = await Assert.ThrowsAsync<DefinitionInvalideException>(Deployer);

        Assert.Contains(ex.Erreurs, e => e.Contains("Le processus enfant 'inexistant'"));
        Assert.Contains(ex.Erreurs, e => e.Contains("la variable obligatoire 'nbJours'"));
    }

    [Fact]
    public async Task Un_noeud_interactif_exige_un_gestionnaire()
    {
        using var hote = new HoteMemoire(apresSentier: s => s.RemoveAll<IGestionnaireTaches>());
        var ex = await Assert.ThrowsAsync<DefinitionInvalideException>(() => hote.Sentier(s => s.DeployerDefinitionsAsync()));
        Assert.Contains(ex.Erreurs, e => e.Contains("[demande-conge] Le nœud interactif 'approbation' exige un IGestionnaireTaches"));
    }

    [Fact]
    public async Task Migration_automatique_d_un_changement_compatible()
    {
        Definir(p => p.Variables(Montant).AttendreSignal("attendre", "GO").Fin("fin"), migrationAutomatique: true);
        await Deployer();
        var instance = await _hote.DemarrerAsync(Cle, 1, ValeursVariables.Avec(Montant, 10L));

        // v2 : nouvelle variable avec défaut, nouveau déclencheur et nouveau nœud après l'attente.
        Definir(p => p
            .Variables(Montant, Note.ParDefaut("aucune"))
            .Attente("attendre", a => a.Signal("GO").Echeance(TimeSpan.FromDays(1)).Alors(b => b.Fin("expire")))
            .Systemique("nouveau", "tracer")
            .Fin("fin"), migrationAutomatique: true);
        var resultat = await DeployerDynamique();

        Assert.Equal((EtatDeploiement.NouvelleVersion, 2, 1, 0), (resultat.Etat, resultat.Version, resultat.InstancesMigrees, resultat.InstancesRestees));
        var etat = await _hote.InstanceAsync(instance.IdInstance);
        Assert.Equal(2, etat.VersionDefinition);
        Assert.Equal("aucune", etat.Variables["note"]);
        Assert.Equal(10L, etat.Variables["montant"]);

        var attentes = await _hote.Suivi(s => s.ObtenirAttentesAsync(instance.IdInstance!.Value));
        Assert.Equal(["GO", "echeance"], attentes.Select(a => a.NomSortie));

        var trace = Assert.Single(_hote.Base.Migrations.Traces);
        Assert.Equal(ModeMigration.Auto, trace.Mode);
        using (var detail = JsonDocument.Parse(trace.Detail!))
        {
            var actions = detail.RootElement.GetProperty("actions").EnumerateArray().Select(a => a.GetString()).ToList();
            Assert.Equal(["nouveaux déclencheurs créés : echeance", "variable 'note' initialisée à sa valeur par défaut"], actions);
        }

        await _hote.Sentier(s => s.EnvoyerSignalAsync("GO", Cle, 1));
        Assert.Equal(["nouveau:Execution"], _hote.Journal.Entrees);
    }

    [Fact]
    public async Task Un_noeud_disparu_laisse_l_instance_sur_sa_version_avec_un_avertissement()
    {
        Definir(p => p.AttendreSignal("attendre", "GO").Fin("fin"), migrationAutomatique: true);
        await Deployer();
        var instance = await _hote.DemarrerAsync(Cle, 1);

        Definir(p => p.AttendreSignal("attente-renommee", "GO").Fin("fin"), migrationAutomatique: true);
        var resultat = await DeployerDynamique();

        Assert.Equal((EtatDeploiement.NouvelleVersionIncompatible, 0, 1), (resultat.Etat, resultat.InstancesMigrees, resultat.InstancesRestees));
        Assert.Contains(resultat.Avertissements, a => a.Contains("'attendre' disparaît dans la v2") && a.Contains("1 instance(s)"));
        Assert.Equal(1, (await _hote.InstanceAsync(instance.IdInstance)).VersionDefinition);

        // L'instance poursuit sur sa version d'origine ; les nouvelles instances prennent la v2.
        await _hote.Sentier(s => s.EnvoyerSignalAsync("GO", Cle, 1));
        Assert.Equal("fin", (await _hote.InstanceAsync(instance.IdInstance)).CodeFin);
        Assert.Equal("attente-renommee", (await _hote.DemarrerAsync(Cle, 2)).CodeNoeudCourant);

        var inchangee = await DeployerDynamique();
        Assert.Equal((EtatDeploiement.Inchangee, 0), (inchangee.Etat, inchangee.InstancesRestees));
    }

    [Fact]
    public async Task Sans_migration_automatique_les_instances_restent()
    {
        Definir(p => p.AttendreSignal("attendre", "GO").Fin("fin"));
        await Deployer();
        await _hote.DemarrerAsync(Cle, 1);

        Definir(p => p.AttendreSignal("attendre", "GO").Systemique("nouveau", "tracer").Fin("fin"));
        var resultat = await DeployerDynamique();

        Assert.Equal((EtatDeploiement.NouvelleVersion, 0, 1), (resultat.Etat, resultat.InstancesMigrees, resultat.InstancesRestees));
    }

    [Fact]
    public async Task Changement_de_type_de_variable_ou_de_declencheur_bloque_la_migration()
    {
        Definir(p => p.Variables(Montant).Attente("attendre", a => a.Signal("GO")).Fin("fin"), migrationAutomatique: true);
        await Deployer();
        await _hote.DemarrerAsync(Cle, 1);

        Definir(p => p.Variable("montant", TypeVariable.Texte).Attente("attendre", a => a.Signal("GO")).Fin("fin"), migrationAutomatique: true);
        Assert.Equal(1, (await DeployerDynamique()).InstancesRestees);

        var simulation = await _hote.Sentier(s => s.SimulerMigrationAsync(new DemandeMigration { CleProcessus = Cle }));
        Assert.Contains("change (Entier → Texte)", Assert.Single(simulation.Instances).Raison);

        Definir(p => p.Variables(Montant).Attente("attendre", a => a.Signal("AUTRE")).Fin("fin"), migrationAutomatique: true);
        await DeployerDynamique();
        simulation = await _hote.Sentier(s => s.SimulerMigrationAsync(new DemandeMigration { CleProcessus = Cle }));
        Assert.Contains("déclencheurs de l'attente 'attendre' sont modifiés", Assert.Single(simulation.Instances).Raison);
    }

    [Fact]
    public async Task Simulation_puis_migration_par_plan()
    {
        Definir(p => p.Variables(Montant).Interactif("ancienne-tache").Fin("fin"));
        await Deployer();
        var a = await _hote.DemarrerAsync(Cle, 1, ValeursVariables.Avec(Montant, 5L));
        var b = await _hote.DemarrerAsync(Cle, 2, ValeursVariables.Avec(Montant, 6L));

        var total = Variable.Entier("total");
        Definir(p => p
            .Variables(total, Note.Obligatoire())
            .Interactif("nouvelle-tache")
            .Systemique("suite", "tracer")
            .Fin("fin"));
        await Deployer();
        _hote.Configuration.Plan = plan => plan
            .Noeud("ancienne-tache", "nouvelle-tache", r => r.ConserverTaches())
            .RenommerVariable("montant", "total")
            .ValeurVariable(Note, "migrée");

        var simulation = await _hote.Sentier(s => s.SimulerMigrationAsync(new DemandeMigration { CleProcessus = Cle }));

        Assert.True(simulation.Simulation);
        Assert.Equal((2, 2, 0), (simulation.VersionCible, simulation.NombreMigrables, simulation.NombreBloquees));
        Assert.Contains("nœud 'ancienne-tache' → 'nouvelle-tache'", simulation.Instances[0].Actions);
        Assert.Contains("variable 'montant' renommée en 'total'", simulation.Instances[0].Actions);
        Assert.Equal(1, (await _hote.InstanceAsync(a.IdInstance)).VersionDefinition);
        Assert.Empty(_hote.Base.Migrations.Traces);

        var migration = await _hote.Sentier(s => s.MigrerAsync(new DemandeMigration { CleProcessus = Cle, Instances = [a.IdInstance!.Value], Utilisateur = "admin" }));

        Assert.False(migration.Simulation);
        Assert.Equal(1, migration.NombreMigrables);
        var etat = await _hote.InstanceAsync(a.IdInstance);
        Assert.Equal((2, "nouvelle-tache"), (etat.VersionDefinition, etat.CodeNoeudCourant));
        Assert.Equal(5L, etat.Variables["total"]);
        Assert.Equal("migrée", etat.Variables["note"]);
        Assert.False(etat.Variables.ContainsKey("montant"));
        Assert.Equal("nouvelle-tache", _hote.Bassin.Tache(a.IdTacheCourante!.Value).CodeNoeud);

        var trace = Assert.Single(_hote.Base.Migrations.Traces);
        Assert.Equal((ModeMigration.Plan, "admin"), (trace.Mode, trace.Utilisateur));
        using var detail = JsonDocument.Parse(trace.Detail!);
        Assert.Equal("nouvelle-tache", detail.RootElement.GetProperty("noeudCible").GetString());

        // La tâche conservée reprend l'instance migrée sur sa nouvelle version.
        await _hote.Sentier(s => s.CompleterTacheAsync(a.IdTacheCourante!.Value));
        Assert.Equal("fin", (await _hote.InstanceAsync(a.IdInstance)).CodeFin);
        Assert.Equal(["suite:Execution"], _hote.Journal.Entrees);

        // Une seconde migration de la même instance est refusée ; l'autre est encore migrable.
        var encore = await _hote.Sentier(s => s.SimulerMigrationAsync(new DemandeMigration { CleProcessus = Cle, Instances = [a.IdInstance!.Value, b.IdInstance!.Value, new IdInstance(999)] }));
        Assert.Contains("n'est pas au repos", encore.Instances[0].Raison);
        Assert.True(encore.Instances[1].Migrable);
        Assert.Equal("Instance introuvable.", encore.Instances[2].Raison);
    }

    [Fact]
    public async Task Migration_par_plan_avec_reprise_a_un_autre_noeud()
    {
        Definir(p => p.Interactif("revoir").Fin("fin"));
        await Deployer();
        var instance = await _hote.DemarrerAsync(Cle, 1);

        Definir(p => p.Interactif("revoir").Systemique("verifier", "tracer").Fin("fin"));
        await Deployer();
        _hote.Configuration.Plan = plan => plan.PourNoeud("revoir", r => r.PuisReprendreA("verifier"));

        var rapport = await _hote.Sentier(s => s.MigrerAsync(new DemandeMigration { CleProcessus = Cle, CodeNoeud = "revoir", VersionSource = 1 }));

        Assert.Equal(1, rapport.NombreMigrables);
        Assert.Equal(StatutTache.Annulee, _hote.Bassin.Tache(instance.IdTacheCourante!.Value).Statut);
        Assert.Equal(["verifier:Execution"], _hote.Journal.Entrees);
        var etat = await _hote.InstanceAsync(instance.IdInstance);
        Assert.Equal(("fin", 2), (etat.CodeFin, etat.VersionDefinition));
    }

    [Fact]
    public async Task Migration_par_plan_avec_commande_de_calcul_et_attentes_recreees()
    {
        Definir(p => p.Variables(Montant).AttendreDelai("attendre", TimeSpan.FromDays(10)).Fin("fin"));
        await Deployer();
        var instance = await _hote.DemarrerAsync(Cle, 1, ValeursVariables.Avec(Montant, 3L));
        _hote.Horloge.Avancer(TimeSpan.FromDays(1));

        Definir(p => p.Variables(Montant, Note.Obligatoire()).AttendreDelai("attendre", TimeSpan.FromDays(2)).Fin("fin"));
        await Deployer();
        _hote.Configuration.Plan = plan => plan
            .PourNoeud("attendre", r => r.RecreerAttentes())
            .CalculerVariables("ecrire", c => c.Valeur("variable", "note").Valeur("valeur", "calculée"));

        var rapport = await _hote.Sentier(s => s.MigrerAsync(new DemandeMigration { CleProcessus = Cle }));

        Assert.True(Assert.Single(rapport.Instances).Migrable);
        var etat = await _hote.InstanceAsync(instance.IdInstance);
        Assert.Equal("calculée", etat.Variables["note"]);
        Assert.Equal(_hote.Horloge.MaintenantUtc.AddDays(2), Assert.Single(await _hote.Suivi(s => s.ObtenirAttentesAsync(instance.IdInstance!.Value))).DateEcheance);
    }

    [Fact]
    public async Task Migration_vers_une_version_absente_ou_anterieure()
    {
        Definir(p => p.AttendreSignal("attendre", "GO").Fin("fin"));
        await Deployer();
        Definir(p => p.AttendreSignal("attendre", "GO").Systemique("x", "tracer").Fin("fin"));
        await Deployer();
        var instance = await _hote.DemarrerAsync(Cle, 1);

        await Assert.ThrowsAsync<ProcessusIntrouvableException>(() => _hote.Sentier(s => s.SimulerMigrationAsync(new DemandeMigration { CleProcessus = Cle, VersionCible = 9 })));
        await Assert.ThrowsAsync<ProcessusIntrouvableException>(() => _hote.Sentier(s => s.SimulerMigrationAsync(new DemandeMigration { CleProcessus = "inconnu" })));

        var versAncienne = await _hote.Sentier(s => s.SimulerMigrationAsync(new DemandeMigration { CleProcessus = Cle, VersionCible = 1, Instances = [instance.IdInstance!.Value] }));
        Assert.Contains("antérieure", versAncienne.Instances.Single().Raison);

        var memeVersion = await _hote.Sentier(s => s.SimulerMigrationAsync(new DemandeMigration { CleProcessus = Cle, Instances = [instance.IdInstance!.Value] }));
        Assert.Contains("déjà sur la version cible", memeVersion.Instances.Single().Raison);
    }

    [Fact]
    public async Task Un_plan_incoherent_avec_la_cible_est_refuse()
    {
        Definir(p => p.AttendreSignal("attendre", "GO").Fin("fin"));
        await Deployer();
        await _hote.DemarrerAsync(Cle, 1);
        Definir(p => p.AttendreSignal("attendre", "GO").Systemique("x", "tracer").Fin("fin"));
        await Deployer();

        _hote.Configuration.Plan = plan => plan.Noeud("attendre", "inexistant");
        await Assert.ThrowsAsync<MigrationInvalideException>(() => _hote.Sentier(s => s.SimulerMigrationAsync(new DemandeMigration { CleProcessus = Cle })));

        _hote.Configuration.Plan = plan => plan.CalculerVariables("commande-inconnue");
        await Assert.ThrowsAsync<MigrationInvalideException>(() => _hote.Sentier(s => s.SimulerMigrationAsync(new DemandeMigration { CleProcessus = Cle })));

        _hote.Configuration.Plan = plan => plan.ValeurVariable("absente", 1);
        await Assert.ThrowsAsync<MigrationInvalideException>(() => _hote.Sentier(s => s.SimulerMigrationAsync(new DemandeMigration { CleProcessus = Cle })));

        // Le plan ne couvre pas la version source : la migration est automatique (et compatible ici).
        _hote.Configuration.Plan = plan => plan.Noeud("attendre", "inexistant");
        _hote.Configuration.PlanVersionMin = 5;
        Assert.True(Assert.Single((await _hote.Sentier(s => s.SimulerMigrationAsync(new DemandeMigration { CleProcessus = Cle }))).Instances).Migrable);
    }

    [Fact]
    public async Task Migrer_exige_une_transaction_mais_pas_la_simulation()
    {
        Definir(p => p.AttendreSignal("attendre", "GO").Fin("fin"));
        await Deployer();
        using var portee = _hote.CreerPortee();
        var service = portee.ServiceProvider.GetRequiredService<IServiceSentier>();

        await Assert.ThrowsAsync<TransactionRequiseException>(() => service.MigrerAsync(new DemandeMigration { CleProcessus = Cle }));
        await Assert.ThrowsAsync<TransactionRequiseException>(() => service.DeployerDefinitionsAsync());
        Assert.Empty((await service.SimulerMigrationAsync(new DemandeMigration { CleProcessus = Cle })).Instances);
    }
}
