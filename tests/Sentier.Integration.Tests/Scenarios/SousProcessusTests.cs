using Sentier.Integration.Tests.Infrastructure;
using Sentier.Integration.Tests.Processus;

namespace Sentier.Integration.Tests.Scenarios;

/// <summary>Parent et enfant : mappages d'entrée et de sortie, fin de l'enfant, annulations en cascade.</summary>
public sealed class SousProcessusTests : IAsyncLifetime
{
    private const long NumeroCommande = 1001;
    private static readonly string[] Processus = [Commande.Cle, VerificationCommande.Cle];
    private EnvironnementSentier _env = null!;

    public async Task InitializeAsync() => _env = await EnvironnementSentier.DemarrerAsync();

    public async Task DisposeAsync() => await _env.DisposeAsync();

    private Task<ResultatExecution> DemarrerAsync(decimal montant)
        => _env.DemarrerAsync(Commande.Cle, NumeroCommande, new Dictionary<string, object?> { ["montant"] = montant });

    private async Task<EtatInstance> EnfantAsync()
    {
        var instances = await _env.LireAsync(s => s.Suivi().ObtenirInstancesParCleAffaireAsync(NumeroCommande, [VerificationCommande.Cle]));
        return Assert.Single(instances);
    }

    [Fact]
    public async Task Sous_le_seuil_parent_et_enfant_se_terminent_dans_le_meme_appel()
    {
        var resultat = await DemarrerAsync(250m);

        Assert.Equal(StatutInstance.Terminee, resultat.Statut);
        Assert.Equal("acceptee", resultat.CodeFin);

        var parent = await _env.InstanceAsync(resultat.IdInstance!.Value);
        Assert.Equal("ok", parent.Variables["resultat"]);

        var enfant = await EnfantAsync();
        Assert.Equal(StatutInstance.Terminee, enfant.Statut);
        Assert.Equal("validee", enfant.CodeFin);
        Assert.Equal(resultat.IdInstance, enfant.IdInstanceParent);
        Assert.Equal(250m, enfant.Variables["montant"]);
    }

    [Fact]
    public async Task Au_dessus_du_seuil_le_parent_attend_la_tache_de_l_enfant()
    {
        var resultat = await DemarrerAsync(5000m);

        Assert.Equal(StatutInstance.Suspendue, resultat.Statut);
        Assert.Equal("verifier", resultat.CodeNoeudCourant);

        var enfant = await EnfantAsync();
        Assert.Equal(StatutInstance.Suspendue, enfant.Statut);
        Assert.Equal("verification-manuelle", enfant.CodeNoeudCourant);
        var tache = await _env.TacheAsync(enfant.IdTacheCourante!.Value);
        Assert.Equal("controleur", tache!.Role);
        Assert.Equal(VerificationCommande.Cle, tache.CleProcessus);
    }

    [Fact]
    public async Task L_enfant_valide_reprend_le_parent_avec_le_mappage_de_sortie()
    {
        var depart = await DemarrerAsync(5000m);
        var enfant = await EnfantAsync();

        var fin = await _env.CompleterTacheAsync(enfant.IdTacheCourante!.Value, new Dictionary<string, object?> { ["resultat"] = "ok" });

        Assert.Equal("validee", fin.CodeFin);
        var parent = await _env.InstanceAsync(depart.IdInstance!.Value);
        Assert.Equal(StatutInstance.Terminee, parent.Statut);
        Assert.Equal("acceptee", parent.CodeFin);
        Assert.Equal("ok", parent.Variables["resultat"]);
    }

    [Fact]
    public async Task Le_code_de_fin_de_l_enfant_choisit_la_branche_du_parent()
    {
        var depart = await DemarrerAsync(5000m);
        var enfant = await EnfantAsync();

        await _env.CompleterTacheAsync(enfant.IdTacheCourante!.Value, new Dictionary<string, object?> { ["resultat"] = "fraude" });

        var parent = await _env.InstanceAsync(depart.IdInstance!.Value);
        Assert.Equal("refusee", parent.CodeFin);
        Assert.Equal("fraude", parent.Variables["resultat"]);
    }

    [Fact]
    public async Task Annuler_l_enfant_fait_suivre_au_parent_sa_branche_d_annulation()
    {
        var depart = await DemarrerAsync(5000m);
        var enfant = await EnfantAsync();

        await _env.ExecuterAsync(s => s.Service().AnnulerAsync(enfant.Id, "commande retirée"));

        var parent = await _env.InstanceAsync(depart.IdInstance!.Value);
        Assert.Equal(StatutInstance.Terminee, parent.Statut);
        Assert.Equal("annulee", parent.CodeFin);
        Assert.Equal("Annulee", (await _env.TacheAsync(enfant.IdTacheCourante!.Value))!.Statut);
    }

    [Fact]
    public async Task Annuler_le_parent_annule_l_enfant_et_sa_tache()
    {
        var depart = await DemarrerAsync(5000m);
        var enfant = await EnfantAsync();

        await _env.ExecuterAsync(s => s.Service().AnnulerAsync(depart.IdInstance!.Value, "client parti"));

        var instances = await _env.LireAsync(s => s.Suivi().ObtenirInstancesParCleAffaireAsync(NumeroCommande, Processus));
        Assert.All(instances, i => Assert.Equal(StatutInstance.Annulee, i.Statut));
        Assert.Equal("Annulee", (await _env.TacheAsync(enfant.IdTacheCourante!.Value))!.Statut);
    }
}
