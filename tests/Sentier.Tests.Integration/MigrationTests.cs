using System.Text.Json;
using Dapper;
using Sentier.Migration;
using Sentier.Tests.Integration.Processus;
using Sentier.Tests.Integration.Support;
using Sentier.Variables;

namespace Sentier.Tests.Integration;

/// <summary>
/// Livraisons successives d'un processus sur une base réelle : migration automatique d'un changement compatible,
/// puis migration par plan d'un changement incompatible, avec les traces en base.
/// </summary>
public sealed class MigrationTests : IAsyncLifetime
{
    private const string Cle = "abonnement";

    private HoteSqlite _hote = null!;

    public async Task InitializeAsync() => _hote = await HoteSqlite.DemarrerAsync();

    public Task DisposeAsync()
    {
        _hote.Dispose();
        return Task.CompletedTask;
    }

    private Task<ResultatExecution> Abonner(long client) => _hote.Sentier(s => s.DemarrerAsync(Cle, client, ValeursVariables.Avec(Abonnement.Formule, "mensuelle")));

    private async Task<ResultatDeploiement> Livrer(int version)
    {
        _hote.Versions.Abonnement = version;
        var rapport = await _hote.Sentier(s => s.DeployerDefinitionsAsync());
        Assert.All(rapport.Definitions.Where(d => d.CleProcessus != Cle), d => Assert.Equal(EtatDeploiement.Inchangee, d.Etat));
        return rapport.Definitions.Single(d => d.CleProcessus == Cle);
    }

    [Fact]
    public async Task Livraisons_v1_v2_v3_avec_migration_automatique_puis_par_plan()
    {
        // v1 : deux clients ; le second paie tout de suite et termine sur la v1.
        var actif = await Abonner(1);
        var paye = await Abonner(2);
        await _hote.Sentier(s => s.EnvoyerSignalAsync(Abonnement.SignalPaiement, Cle, 2));

        // v2 : nouvelle échéance et nouveau nœud, compatibles : l'instance au repos est migrée au déploiement.
        _hote.Horloge.Avancer(TimeSpan.FromDays(1));
        var v2 = await Livrer(2);

        Assert.Equal((EtatDeploiement.NouvelleVersion, 2, 1, 0), (v2.Etat, v2.Version, v2.InstancesMigrees, v2.InstancesRestees));
        Assert.Equal(2, (await _hote.InstanceAsync(actif.IdInstance)).VersionDefinition);
        Assert.Equal(1, (await _hote.InstanceAsync(paye.IdInstance)).VersionDefinition);
        var echeance = (await _hote.Suivi(s => s.ObtenirAttentesAsync(actif.IdInstance!.Value))).Single(a => a.TypeDeclencheur == TypeDeclencheur.Echeance);
        Assert.Equal(_hote.Horloge.MaintenantUtc.AddDays(30), echeance.DateEcheance);

        // v3 : le nœud d'attente est renommé ; l'instance ne peut pas suivre seule.
        var v3 = await Livrer(3);

        Assert.Equal((EtatDeploiement.NouvelleVersionIncompatible, 3, 0, 1), (v3.Etat, v3.Version, v3.InstancesMigrees, v3.InstancesRestees));
        Assert.Contains(v3.Avertissements, a => a.Contains("'attendre-paiement' disparaît dans la v3"));
        Assert.Equal([(2, "attendre-paiement", 1)], (await _hote.Suivi(s => s.CompterInstancesActivesAsync(Cle))).Select(c => (c.Version, c.CodeNoeud, c.Nombre)));

        // Simulation avec le plan : rien n'est écrit.
        var simulation = await _hote.Sentier(s => s.SimulerMigrationAsync(new DemandeMigration { CleProcessus = Cle }));
        var prevue = Assert.Single(simulation.Instances);
        Assert.True(prevue.Migrable);
        Assert.Equal((2, 3), (prevue.VersionSource, prevue.VersionCible));
        Assert.Contains("nœud 'attendre-paiement' → 'attendre-reglement'", prevue.Actions);
        Assert.Equal(2, (await _hote.InstanceAsync(actif.IdInstance)).VersionDefinition);

        // Migration par plan : nœud et attentes renommés, valeur existante conservée.
        var migration = await _hote.Sentier(s => s.MigrerAsync(new DemandeMigration { CleProcessus = Cle, VersionSource = 2, TailleMax = 10, Utilisateur = "exploitation" }));

        Assert.Equal(1, migration.NombreMigrables);
        var etat = await _hote.InstanceAsync(actif.IdInstance);
        Assert.Equal((3, "attendre-reglement", StatutInstance.Suspendue), (etat.VersionDefinition, etat.CodeNoeudCourant, etat.Statut));
        Assert.Equal(0m, etat.Variables["rabais"]);
        var attentes = await _hote.Suivi(s => s.ObtenirAttentesAsync(actif.IdInstance!.Value));
        Assert.All(attentes, a => Assert.Equal("attendre-reglement", a.CodeNoeud));
        Assert.Equal(echeance.DateEcheance, attentes.Single(a => a.TypeDeclencheur == TypeDeclencheur.Echeance).DateEcheance);

        // Traces de migration en base : une automatique, une par plan.
        var traces = _hote.Connexion.Query<(string Mode, string Detail, string? Utilisateur)>(
            $"SELECT MODE_MIGRA, DETL, LOGON_UTIL FROM {HoteSqlite.Prefixe}_MIGRA_PROCS ORDER BY NO_SEQ_MIGRA_PROCS").ToList();
        Assert.Equal(["AUTO", "PLAN"], traces.Select(t => t.Mode));
        Assert.Equal("exploitation", traces[1].Utilisateur);
        using (var detail = JsonDocument.Parse(traces[1].Detail))
        {
            Assert.Equal("attendre-reglement", detail.RootElement.GetProperty("noeudCible").GetString());
        }

        // L'instance migrée poursuit sur la v3.
        var fin = await _hote.Sentier(s => s.EnvoyerSignalAsync(Abonnement.SignalPaiement, Cle, 1));
        Assert.Equal("actif", fin.Resultats.Single().CodeFin);
        Assert.Equal([("abonnement-confirme", "mensuelle", null)], JournalClient.Lire(_hote.Connexion, 1));
        Assert.Empty(await _hote.Suivi(s => s.CompterInstancesActivesAsync(Cle)));

        // Une nouvelle livraison identique ne crée rien.
        var inchangee = await Livrer(3);
        Assert.Equal((EtatDeploiement.Inchangee, 3), (inchangee.Etat, inchangee.Version));
        Assert.Equal(3, _hote.Compter("_DEFIN_PROCS", $"CLE = '{Cle}'"));
        Assert.Equal(1, _hote.Compter("_DEFIN_PROCS", $"CLE = '{Cle}' AND IND_ACTIV = 1"));
    }

    [Fact]
    public async Task L_echeance_creee_par_la_migration_est_traitee_par_le_batch()
    {
        var abonnement = await Abonner(1);
        await Livrer(2);

        _hote.Horloge.Avancer(TimeSpan.FromDays(31));
        Assert.Equal(1, await _hote.ExecuterBatchEcheancesAsync());

        Assert.Equal("expire", (await _hote.InstanceAsync(abonnement.IdInstance)).CodeFin);
    }

    [Fact]
    public async Task Migration_par_lots()
    {
        for (var client = 1; client <= 5; client++)
        {
            await Abonner(client);
        }

        await Livrer(2);
        await Livrer(3);

        var premierLot = await _hote.Sentier(s => s.MigrerAsync(new DemandeMigration { CleProcessus = Cle, TailleMax = 2 }));
        var secondLot = await _hote.Sentier(s => s.MigrerAsync(new DemandeMigration { CleProcessus = Cle, TailleMax = 10 }));

        Assert.Equal(2, premierLot.NombreMigrables);
        Assert.Equal(3, secondLot.NombreMigrables);
        Assert.Equal([(3, "attendre-reglement", 5)], (await _hote.Suivi(s => s.CompterInstancesActivesAsync(Cle))).Select(c => (c.Version, c.CodeNoeud, c.Nombre)));
    }
}
