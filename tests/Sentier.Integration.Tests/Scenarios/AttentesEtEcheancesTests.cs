using Sentier.Integration.Tests.Infrastructure;
using Sentier.Integration.Tests.Processus;

namespace Sentier.Integration.Tests.Scenarios;

/// <summary>Signaux, corrélation, échéances traitées par le batch du client, erreurs et reprise.</summary>
public sealed class AttentesEtEcheancesTests : IAsyncLifetime
{
    private const long Fournisseur = 7;
    private EnvironnementSentier _env = null!;

    public async Task InitializeAsync() => _env = await EnvironnementSentier.DemarrerAsync();

    public async Task DisposeAsync() => await _env.DisposeAsync();

    [Fact]
    public async Task L_instance_attend_un_signal_et_une_echeance()
    {
        var depart = await _env.DemarrerAsync(Relance.Cle, Fournisseur);

        Assert.Equal(StatutInstance.Suspendue, depart.Statut);
        Assert.Equal(Relance.NoeudAttente, depart.CodeNoeudCourant);
        var attentes = await _env.LireAsync(s => s.Suivi().ObtenirAttentesAsync(depart.IdInstance!.Value));
        Assert.Equal(3, attentes.Count);
        var echeance = Assert.Single(attentes, a => a.TypeDeclencheur == TypeDeclencheur.Echeance);
        Assert.Equal(_env.Horloge.MaintenantUtc + Relance.Delai, echeance.DateEcheance);
    }

    [Fact]
    public async Task Le_signal_reprend_l_instance_et_fusionne_sa_charge_utile()
    {
        var depart = await _env.DemarrerAsync(Relance.Cle, Fournisseur);

        var signal = await _env.EnvoyerSignalAsync(Relance.SignalReponse, Relance.Cle, Fournisseur, new Dictionary<string, object?> { ["reponse"] = "livraison lundi" });

        Assert.Equal(1, signal.NombreInstancesReprises);
        var resultat = Assert.Single(signal.Resultats);
        Assert.Equal("repondue", resultat.CodeFin);
        var instance = await _env.InstanceAsync(depart.IdInstance!.Value);
        Assert.Equal("livraison lundi", instance.Variables["reponse"]);
        Assert.Empty(await _env.LireAsync(s => s.Suivi().ObtenirAttentesAsync(depart.IdInstance!.Value)));
        Assert.Empty(await _env.LireAsync(s => s.Service().ObtenirEcheancesDuesAsync()));
    }

    [Fact]
    public async Task Chaque_signal_suit_sa_propre_sortie()
    {
        await _env.DemarrerAsync(Relance.Cle, Fournisseur);

        var signal = await _env.EnvoyerSignalAsync(Relance.SignalAbandon, Relance.Cle, Fournisseur);

        Assert.Equal("abandonnee", Assert.Single(signal.Resultats).CodeFin);
    }

    [Fact]
    public async Task Un_signal_sans_instance_en_attente_est_sans_effet()
    {
        var signal = await _env.EnvoyerSignalAsync(Relance.SignalReponse, Relance.Cle, Fournisseur);

        Assert.True(signal.SansEffet);
        Assert.NotNull(signal.Raison);
    }

    [Fact]
    public async Task Un_signal_mal_cible_est_refuse()
        => await Assert.ThrowsAsync<ArgumentException>(() =>
            _env.ExecuterAsync(s => s.Service().EnvoyerSignalAsync(new Signal { Nom = Relance.SignalReponse, CleAffaire = Fournisseur })));

    [Fact]
    public async Task L_echeance_n_est_due_qu_une_fois_atteinte()
    {
        await _env.DemarrerAsync(Relance.Cle, Fournisseur);

        _env.Horloge.Avancer(Relance.Delai - TimeSpan.FromMinutes(1));
        Assert.Empty(await _env.LireAsync(s => s.Service().ObtenirEcheancesDuesAsync()));

        _env.Horloge.Avancer(TimeSpan.FromMinutes(2));
        var due = Assert.Single(await _env.LireAsync(s => s.Service().ObtenirEcheancesDuesAsync()));
        Assert.Equal(Relance.Cle, due.CleProcessus);
        Assert.Equal(Fournisseur, due.CleAffaire);
    }

    [Fact]
    public async Task L_echeance_declenche_une_relance_puis_l_attente_reprend()
    {
        var depart = await _env.DemarrerAsync(Relance.Cle, Fournisseur);
        var id = depart.IdInstance!.Value;

        _env.Horloge.Avancer(Relance.Delai + TimeSpan.FromMinutes(1));
        var resultats = await _env.TraiterEcheancesAsync();
        _env.Horloge.Avancer(Relance.Delai + TimeSpan.FromMinutes(1));
        await _env.TraiterEcheancesAsync();

        var resultat = Assert.Single(resultats);
        Assert.Equal(StatutInstance.Suspendue, resultat.Statut);
        Assert.Equal(Relance.NoeudAttente, resultat.CodeNoeudCourant);
        var instance = await _env.InstanceAsync(id);
        Assert.Equal(2L, instance.Variables["nbRelances"]);
        Assert.Equal([$"relance:{Fournisseur}:1", $"relance:{Fournisseur}:2"], _env.Metier.Journal);

        // L'attente a été recréée avec une nouvelle échéance, relative au moment de la nouvelle entrée.
        var echeance = Assert.Single(await _env.LireAsync(s => s.Suivi().ObtenirAttentesAsync(id)), a => a.TypeDeclencheur == TypeDeclencheur.Echeance);
        Assert.Equal(_env.Horloge.MaintenantUtc + Relance.Delai, echeance.DateEcheance);

        var historique = await _env.HistoriqueAsync(id);
        Assert.Equal(["debut", Relance.NoeudAttente, "relancer", Relance.NoeudAttente, "relancer", Relance.NoeudAttente], historique.Select(p => p.CodeNoeud));
        Assert.Equal("echeance", historique[1].Resultat);
    }

    [Fact]
    public async Task Une_echeance_deja_gagnee_par_un_signal_est_sans_effet()
    {
        var depart = await _env.DemarrerAsync(Relance.Cle, Fournisseur);
        _env.Horloge.Avancer(Relance.Delai + TimeSpan.FromMinutes(1));
        var due = Assert.Single(await _env.LireAsync(s => s.Service().ObtenirEcheancesDuesAsync()));

        await _env.EnvoyerSignalAsync(Relance.SignalReponse, Relance.Cle, Fournisseur);
        var resultat = await _env.ExecuterAsync(s => s.Service().TraiterEcheanceAsync(due.IdAttente));

        Assert.True(resultat.SansEffet);
        Assert.Equal("repondue", (await _env.InstanceAsync(depart.IdInstance!.Value)).CodeFin);
    }

    [Fact]
    public async Task Une_commande_en_echec_annule_la_transaction_marque_l_erreur_puis_la_reprise_repart()
    {
        var depart = await _env.DemarrerAsync(Relance.Cle, Fournisseur);
        var id = depart.IdInstance!.Value;
        _env.Metier.RelanceEnPanne = true;
        _env.Horloge.Avancer(Relance.Delai + TimeSpan.FromMinutes(1));

        var resultats = await _env.TraiterEcheancesAsync();

        // La transaction de l'échéance a été annulée : rien n'a avancé, l'erreur a été marquée à part.
        Assert.Empty(resultats);
        var enErreur = await _env.InstanceAsync(id);
        Assert.Equal(StatutInstance.EnErreur, enErreur.Statut);
        Assert.Equal(Relance.NoeudAttente, enErreur.CodeNoeudCourant);
        Assert.Contains("courriel indisponible", enErreur.MessageErreur);
        Assert.Equal(id, Assert.Single(await _env.LireAsync(s => s.Suivi().ObtenirInstancesEnErreurAsync())).Id);

        // Le batch ignore les instances en erreur.
        Assert.Empty(await _env.LireAsync(s => s.Service().ObtenirEcheancesDuesAsync()));

        // Le service est rétabli : la reprise traite tout de suite l'échéance déjà atteinte.
        _env.Metier.RelanceEnPanne = false;
        var reprise = await _env.ExecuterAsync(s => s.Service().ReprendreAsync(id));

        Assert.Equal(StatutInstance.Suspendue, reprise.Statut);
        var repris = await _env.InstanceAsync(id);
        Assert.Equal(1L, repris.Variables["nbRelances"]);
        Assert.Null(repris.MessageErreur);
        Assert.Empty(await _env.LireAsync(s => s.Suivi().ObtenirInstancesEnErreurAsync()));
    }

    [Fact]
    public async Task Reprendre_une_instance_qui_n_est_pas_en_erreur_est_sans_effet()
    {
        var depart = await _env.DemarrerAsync(Relance.Cle, Fournisseur);

        Assert.True((await _env.ExecuterAsync(s => s.Service().ReprendreAsync(depart.IdInstance!.Value))).SansEffet);
    }

    [Fact]
    public async Task Le_signal_correle_cible_l_instance_par_sa_cle_metier()
    {
        var facture1 = await _env.DemarrerAsync(PaiementFacture.Cle, 1, new Dictionary<string, object?> { ["numeroFacture"] = "F-001" });
        var facture2 = await _env.DemarrerAsync(PaiementFacture.Cle, 1, new Dictionary<string, object?> { ["numeroFacture"] = "F-002" });

        var signal = await _env.ExecuterAsync(s => s.Service().EnvoyerSignalCorrelationAsync(
            PaiementFacture.SignalPaiement, "F-002", new Dictionary<string, object?> { ["montantPaye"] = 99.95m }));

        Assert.Equal(1, signal.NombreInstancesReprises);
        Assert.Equal(StatutInstance.Suspendue, (await _env.InstanceAsync(facture1.IdInstance!.Value)).Statut);
        var payee = await _env.InstanceAsync(facture2.IdInstance!.Value);
        Assert.Equal("payee", payee.CodeFin);
        Assert.Equal(99.95m, payee.Variables["montantPaye"]);
    }

    [Fact]
    public async Task Une_boucle_infinie_est_arretee_et_la_transaction_annulee()
    {
        await Assert.ThrowsAsync<BoucleInfinieException>(() => _env.DemarrerAsync(BoucleSansFin.Cle, 1));

        Assert.Empty(await _env.LireAsync(s => s.Suivi().ObtenirInstancesParCleAffaireAsync(1, [BoucleSansFin.Cle])));
    }

    [Fact]
    public async Task La_meme_boucle_se_termine_quand_la_condition_le_permet()
    {
        var resultat = await _env.DemarrerAsync(BoucleSansFin.Cle, 1, new Dictionary<string, object?> { ["continuer"] = false });

        Assert.Equal("fin", resultat.CodeFin);
    }
}
