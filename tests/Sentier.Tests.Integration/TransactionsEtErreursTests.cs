using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Sentier.Tests.Integration.Processus;
using Sentier.Tests.Integration.Support;
using Sentier.Variables;

namespace Sentier.Tests.Integration;

/// <summary>
/// Une seule transaction, celle du client : ce qu'elle annule, Sentier l'annule aussi, y compris les écritures
/// des commandes et du gestionnaire de tâches. Erreurs dans le batch, mise en erreur et reprise.
/// </summary>
public sealed class TransactionsEtErreursTests : IAsyncLifetime
{
    private const long Employe = 7;
    private const string Cle = "demande-conge";

    private HoteSqlite _hote = null!;

    public async Task InitializeAsync()
    {
        _hote = await HoteSqlite.DemarrerAsync();
        _hote.Connexion.Execute("INSERT INTO CLIENT_EMPLOYE (ID, NOM, SOLDE) VALUES (@Employe, 'Bob', 20)", new { Employe });
    }

    public Task DisposeAsync()
    {
        _hote.Dispose();
        return Task.CompletedTask;
    }

    private long SoldeEmploye() => _hote.Connexion.ExecuteScalar<long>("SELECT SOLDE FROM CLIENT_EMPLOYE WHERE ID = @Employe", new { Employe });

    private static ValeursVariables Jours(long jours) => ValeursVariables.Avec(DemandeConge.NbJours, jours);

    private async Task<ResultatExecution> DemarrerJusquALaValidationRh()
    {
        var demarrage = await _hote.Sentier(s => s.DemarrerAsync(Cle, Employe, Jours(4)));
        await _hote.Sentier(s => s.CompleterTacheAsync(demarrage.IdTacheCourante!.Value, new Dictionary<string, object?> { ["decision"] = "approuvee" }));
        return demarrage;
    }

    [Fact]
    public async Task Sans_transaction_les_ecritures_sont_refusees()
    {
        using var portee = _hote.CreerPortee();
        var service = portee.ServiceProvider.GetRequiredService<IServiceSentier>();

        await Assert.ThrowsAsync<TransactionRequiseException>(() => service.DemarrerAsync(Cle, Employe, Jours(1)));
        await Assert.ThrowsAsync<TransactionRequiseException>(() => service.DeployerDefinitionsAsync());
        Assert.Empty(await service.ObtenirEcheancesDuesAsync());
    }

    [Fact]
    public async Task Un_demarrage_annule_par_le_client_ne_laisse_aucune_trace()
    {
        var resultat = await _hote.PuisAnnulerAsync(s => s.GetRequiredService<IServiceSentier>().DemarrerAsync(Cle, Employe, Jours(4)));
        Assert.Equal(StatutInstance.Suspendue, resultat.Statut);

        // Instance, historique et tâche du client (même transaction) ont disparu.
        Assert.Equal(0, _hote.Compter("_INSTC_PROCS"));
        Assert.Equal(0, _hote.Compter("_HIST_NOEUD_PROCS"));
        Assert.Equal(0, _hote.Connexion.ExecuteScalar<long>("SELECT COUNT(*) FROM CLIENT_TACHE"));

        // Le démarrage peut être rejoué.
        Assert.False((await _hote.Sentier(s => s.DemarrerAsync(Cle, Employe, Jours(4)))).SansEffet);
    }

    [Fact]
    public async Task Une_completion_annulee_par_le_client_peut_etre_rejouee()
    {
        var demarrage = await _hote.Sentier(s => s.DemarrerAsync(Cle, Employe, Jours(4)));
        var idTache = demarrage.IdTacheCourante!.Value;

        await _hote.PuisAnnulerAsync(s => s.GetRequiredService<IServiceSentier>().CompleterTacheAsync(idTache, new Dictionary<string, object?> { ["decision"] = "approuvee" }));

        Assert.Equal("OUVERTE", GestionnaireTachesSql.Lire(_hote.Connexion, idTache).Statut);
        Assert.Equal("approbation", (await _hote.InstanceAsync(demarrage.IdInstance)).CodeNoeudCourant);
        Assert.Empty(JournalClient.Lire(_hote.Connexion, Employe));

        var rejouee = await _hote.Sentier(s => s.CompleterTacheAsync(idTache, new Dictionary<string, object?> { ["decision"] = "approuvee" }));
        Assert.Equal("attendre-rh", rejouee.CodeNoeudCourant);
    }

    [Fact]
    public async Task Une_commande_en_echec_annule_tout_le_travail_de_la_transaction()
    {
        var demarrage = await DemarrerJusquALaValidationRh();
        _hote.Pannes.Archivage = true;

        // Le débit du solde s'exécute avant l'archivage, dans la même transaction : il est annulé avec elle.
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _hote.Sentier(s => s.EnvoyerSignalAsync(DemandeConge.SignalRh, Cle, Employe)));
        Assert.Equal("Service d'archivage indisponible.", ex.Message);
        Assert.Equal(20, SoldeEmploye());
        Assert.Equal(0, _hote.Connexion.ExecuteScalar<long>("SELECT COUNT(*) FROM CLIENT_ARCHIVE"));
        var etat = await _hote.InstanceAsync(demarrage.IdInstance);
        Assert.Equal((StatutInstance.Suspendue, "attendre-rh"), (etat.Statut, etat.CodeNoeudCourant));
        Assert.Equal(2, (await _hote.Suivi(s => s.ObtenirAttentesAsync(demarrage.IdInstance!.Value))).Count);
        Assert.Empty(await _hote.Suivi(s => s.ObtenirInstancesParCleAffaireAsync(Employe, ["archivage-conge"])));

        // Le service revient : le même signal termine la demande, le solde n'est débité qu'une fois.
        _hote.Pannes.Archivage = false;
        var signal = await _hote.Sentier(s => s.EnvoyerSignalAsync(DemandeConge.SignalRh, Cle, Employe));
        Assert.Equal("approuvee", signal.Resultats.Single().CodeFin);
        Assert.Equal(16, SoldeEmploye());
    }

    [Fact]
    public async Task Le_batch_marque_l_instance_en_erreur_puis_la_reprise_la_relance()
    {
        var demarrage = await DemarrerJusquALaValidationRh();
        var autre = await _hote.Sentier(s => s.DemarrerAsync(Cle, 8, ValeursVariables.Avec(DemandeConge.NbJours, 1L)));
        _hote.Pannes.Relance = true;
        _hote.Horloge.Avancer(TimeSpan.FromDays(3));

        // L'échéance échoue : sa transaction est annulée, l'erreur est marquée dans une transaction séparée.
        Assert.Equal(0, await _hote.ExecuterBatchEcheancesAsync());
        var enErreur = Assert.Single(await _hote.Suivi(s => s.ObtenirInstancesEnErreurAsync()));
        Assert.Equal((demarrage.IdInstance!.Value, "Messagerie indisponible.", "attendre-rh"), (enErreur.Id, enErreur.MessageErreur, enErreur.CodeNoeudCourant));
        Assert.Equal(_hote.Horloge.MaintenantUtc, enErreur.DateErreur);
        Assert.Equal(2, (await _hote.Suivi(s => s.ObtenirAttentesAsync(demarrage.IdInstance!.Value))).Count);
        Assert.Equal("refusee", autre.CodeFin);

        // Une instance en erreur est ignorée par le batch…
        _hote.Pannes.Relance = false;
        Assert.Equal(0, await _hote.ExecuterBatchEcheancesAsync());

        // … jusqu'à sa reprise : l'échéance déjà atteinte est traitée tout de suite.
        var reprise = await _hote.Sentier(s => s.ReprendreAsync(demarrage.IdInstance!.Value));
        Assert.Equal((StatutInstance.Suspendue, "attendre-rh"), (reprise.Statut, reprise.CodeNoeudCourant));
        var etat = await _hote.InstanceAsync(demarrage.IdInstance);
        Assert.Null(etat.MessageErreur);
        Assert.Equal(1L, etat.Variables["relances"]);
        Assert.Empty(await _hote.Suivi(s => s.ObtenirInstancesEnErreurAsync()));
        Assert.Equal(_hote.Horloge.MaintenantUtc.AddDays(2), (await _hote.Suivi(s => s.ObtenirAttentesAsync(demarrage.IdInstance!.Value))).Single(a => a.TypeDeclencheur == TypeDeclencheur.Echeance).DateEcheance);
    }

    [Fact]
    public async Task Une_instance_en_erreur_recoit_encore_signaux_et_taches()
    {
        var demarrage = await _hote.Sentier(s => s.DemarrerAsync(Cle, Employe, Jours(4)));
        await _hote.Sentier(s => s.MarquerEnErreurAsync(demarrage.IdInstance!.Value, "incident"));

        var completion = await _hote.Sentier(s => s.CompleterTacheAsync(demarrage.IdTacheCourante!.Value, new Dictionary<string, object?> { ["decision"] = "approuvee" }));
        Assert.Equal((StatutInstance.Suspendue, "attendre-rh"), (completion.Statut, completion.CodeNoeudCourant));

        await _hote.Sentier(s => s.MarquerEnErreurAsync(demarrage.IdInstance!.Value, "autre incident"));
        var signal = await _hote.Sentier(s => s.EnvoyerSignalAsync(DemandeConge.SignalRh, Cle, Employe));
        Assert.Equal("approuvee", signal.Resultats.Single().CodeFin);
    }

    [Fact]
    public async Task Les_types_de_variables_survivent_a_la_base()
    {
        var formule = "Premium « été »";
        var demarrage = await _hote.Sentier(s => s.DemarrerAsync("abonnement", 55, ValeursVariables.Avec(Abonnement.Formule, formule).Et(Abonnement.Rabais, 12.345m)));

        var etat = await _hote.InstanceAsync(demarrage.IdInstance);
        Assert.Equal(formule, etat.Variables["formule"]);
        Assert.Equal(12.345m, Assert.IsType<decimal>(etat.Variables["rabais"]));
        Assert.Equal(DateTimeKind.Utc, etat.DateCreation.Kind);
        Assert.Equal(HorlogeSimulee.Origine, etat.DateCreation);
    }
}
