using Sentier.Definitions;
using Sentier.Tests.Unitaires.Support;
using Sentier.Variables;

namespace Sentier.Tests.Unitaires.Execution;

/// <summary>Nœuds d'attente : signaux, échéances, course entre déclencheurs.</summary>
public class AttentesTests : IDisposable
{
    private static readonly Variable<string> Decision = Variable.Texte("decision");
    private static readonly Variable<long> Dossier = Variable.Entier("dossier");
    private static readonly Variable<DateTime> Limite = Variable.Date("limite");

    private readonly HoteMemoire _hote = new();

    public AttentesTests()
    {
        _hote.Publier(ConstructeurProcessus.Nouveau("validation")
            .Variables(Decision, Dossier)
            .Attente("attendre", a => a
                .CommandeEntree("tracer")
                .Signal("VALIDE")
                .Signal("REFUSE", s => s.SansFusion()).Alors(b => b.Fin("refusee"))
                .Echeance(TimeSpan.FromDays(2)).Alors(b => b.Fin("expiree")))
            .Fin("validee")
            .Construire());
    }

    public void Dispose() => _hote.Dispose();

    [Fact]
    public async Task L_attente_suspend_et_cree_un_declencheur_par_sortie()
    {
        var resultat = await _hote.DemarrerAsync("validation", 10);

        Assert.Equal(StatutInstance.Suspendue, resultat.Statut);
        Assert.Equal("attendre", resultat.CodeNoeudCourant);
        Assert.Equal(["attendre:Entree"], _hote.Journal.Entrees);

        var attentes = await _hote.Suivi(s => s.ObtenirAttentesAsync(resultat.IdInstance!.Value));
        Assert.Equal(["VALIDE", "REFUSE", "echeance"], attentes.Select(a => a.NomSortie));
        Assert.Equal([TypeDeclencheur.Signal, TypeDeclencheur.Signal, TypeDeclencheur.Echeance], attentes.Select(a => a.TypeDeclencheur));
        Assert.Equal(HorlogeTest.Origine.AddDays(2), attentes[2].DateEcheance);

        var passage = (await _hote.Suivi(s => s.ObtenirHistoriqueAsync(resultat.IdInstance!.Value)))[^1];
        Assert.Equal("attendre", passage.CodeNoeud);
        Assert.Null(passage.DateSortie);
    }

    [Fact]
    public async Task Un_signal_reprend_l_instance_et_fusionne_sa_charge_utile()
    {
        var demarrage = await _hote.DemarrerAsync("validation", 10);
        _hote.Horloge.Avancer(TimeSpan.FromHours(1));

        var signal = await _hote.Sentier(s => s.EnvoyerSignalAsync("VALIDE", "validation", 10, new Dictionary<string, object?> { ["decision"] = "ok", ["inconnue"] = 1 }));

        Assert.Equal(1, signal.NombreInstancesReprises);
        Assert.False(signal.SansEffet);
        Assert.Equal("validee", signal.Resultats.Single().CodeFin);

        var etat = await _hote.InstanceAsync(demarrage.IdInstance);
        Assert.Equal("ok", etat.Variables["decision"]);
        Assert.Equal("VALIDE", etat.Variables[VariablesProcessus.VariableDeclencheur]);
        Assert.False(etat.Variables.ContainsKey("inconnue"));
        Assert.Empty(await _hote.Suivi(s => s.ObtenirAttentesAsync(demarrage.IdInstance!.Value)));

        var passage = (await _hote.Suivi(s => s.ObtenirHistoriqueAsync(demarrage.IdInstance!.Value))).Single(p => p.CodeNoeud == "attendre");
        Assert.Equal(("VALIDE", HorlogeTest.Origine.AddHours(1)), (passage.Resultat, passage.DateSortie));
    }

    [Fact]
    public async Task Un_signal_sans_fusion_ignore_la_charge_utile()
    {
        var demarrage = await _hote.DemarrerAsync("validation", 10);
        await _hote.Sentier(s => s.EnvoyerSignalAsync("REFUSE", "validation", 10, new Dictionary<string, object?> { ["decision"] = "non" }));

        var etat = await _hote.InstanceAsync(demarrage.IdInstance);
        Assert.Equal("refusee", etat.CodeFin);
        Assert.False(etat.Variables.ContainsKey("decision"));
    }

    [Fact]
    public async Task Un_signal_sans_instance_en_attente_est_sans_effet()
    {
        await _hote.DemarrerAsync("validation", 10);

        var autreCle = await _hote.Sentier(s => s.EnvoyerSignalAsync("VALIDE", "validation", 99));
        var autreNom = await _hote.Sentier(s => s.EnvoyerSignalAsync("INCONNU", "validation", 10));

        Assert.True(autreCle.SansEffet);
        Assert.Contains("Aucune instance", autreCle.Raison);
        Assert.True(autreNom.SansEffet);
    }

    [Fact]
    public async Task Un_signal_doit_cibler_une_cle_ou_une_correlation()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => _hote.Sentier(s => s.EnvoyerSignalAsync(new Signal { Nom = "X" })));
        await Assert.ThrowsAsync<ArgumentException>(() => _hote.Sentier(s => s.EnvoyerSignalAsync(new Signal { Nom = "X", CleAffaire = 1 })));
        await Assert.ThrowsAsync<ArgumentException>(() => _hote.Sentier(s => s.EnvoyerSignalAsync(new Signal { Nom = "X", CleProcessus = "p" })));
        await Assert.ThrowsAsync<ArgumentException>(() => _hote.Sentier(s => s.EnvoyerSignalAsync(new Signal { Nom = " ", CleCorrelation = "c" })));
    }

    [Fact]
    public async Task L_echeance_est_traitee_par_le_batch_une_seule_fois()
    {
        var demarrage = await _hote.DemarrerAsync("validation", 10);

        Assert.Empty(await _hote.Sentier(s => s.ObtenirEcheancesDuesAsync()));
        var attente = (await _hote.Suivi(s => s.ObtenirAttentesAsync(demarrage.IdInstance!.Value))).Single(a => a.TypeDeclencheur == TypeDeclencheur.Echeance);
        var tropTot = await _hote.Sentier(s => s.TraiterEcheanceAsync(attente.Id));
        Assert.True(tropTot.SansEffet);
        Assert.Contains("pas encore atteinte", tropTot.Raison);

        _hote.Horloge.Avancer(TimeSpan.FromDays(2));
        var dues = await _hote.Sentier(s => s.ObtenirEcheancesDuesAsync());
        var due = Assert.Single(dues);
        Assert.Equal((demarrage.IdInstance!.Value, "validation", 10L), (due.IdInstance, due.CleProcessus, due.CleAffaire));

        var traitee = await _hote.Sentier(s => s.TraiterEcheanceAsync(due.IdAttente));
        Assert.Equal("expiree", traitee.CodeFin);
        Assert.Equal("echeance", (await _hote.InstanceAsync(demarrage.IdInstance)).Variables[VariablesProcessus.VariableDeclencheur]);

        var encore = await _hote.Sentier(s => s.TraiterEcheanceAsync(due.IdAttente));
        Assert.True(encore.SansEffet);
        Assert.Empty(await _hote.Sentier(s => s.ObtenirEcheancesDuesAsync()));
    }

    [Fact]
    public async Task Le_signal_gagne_la_course_contre_l_echeance()
    {
        var demarrage = await _hote.DemarrerAsync("validation", 10);
        _hote.Horloge.Avancer(TimeSpan.FromDays(3));
        var due = Assert.Single(await _hote.Sentier(s => s.ObtenirEcheancesDuesAsync()));

        await _hote.Sentier(s => s.EnvoyerSignalAsync("VALIDE", "validation", 10));
        var perdante = await _hote.Sentier(s => s.TraiterEcheanceAsync(due.IdAttente));

        Assert.True(perdante.SansEffet);
        Assert.Equal("validee", (await _hote.InstanceAsync(demarrage.IdInstance)).CodeFin);
    }

    [Fact]
    public async Task Traiter_une_attente_de_signal_comme_echeance_est_sans_effet()
    {
        var demarrage = await _hote.DemarrerAsync("validation", 10);
        var signal = (await _hote.Suivi(s => s.ObtenirAttentesAsync(demarrage.IdInstance!.Value))).First(a => a.TypeDeclencheur == TypeDeclencheur.Signal);

        var resultat = await _hote.Sentier(s => s.TraiterEcheanceAsync(signal.Id));

        Assert.True(resultat.SansEffet);
        Assert.Contains("n'est pas une échéance", resultat.Raison);
    }

    [Fact]
    public async Task Correlation_fixe_ou_tiree_d_une_variable()
    {
        _hote.Publier(ConstructeurProcessus.Nouveau("correle")
            .InstancesMultiples()
            .Variables(Dossier)
            .Attente("attendre", a => a
                .Signal("PAR_VARIABLE", s => s.Correlation(Dossier)).Alors(b => b.Fin("par-variable"))
                .Signal("FIXE", s => s.Correlation("lot-1")))
            .Fin("fixe")
            .Construire());

        var a = await _hote.DemarrerAsync("correle", 1, ValeursVariables.Avec(Dossier, 500L));
        var b = await _hote.DemarrerAsync("correle", 2, ValeursVariables.Avec(Dossier, 600L));

        var parVariable = await _hote.Sentier(s => s.EnvoyerSignalCorrelationAsync("PAR_VARIABLE", "600"));
        Assert.Equal(b.IdInstance, parVariable.Resultats.Single().IdInstance);
        Assert.Equal("par-variable", parVariable.Resultats.Single().CodeFin);

        var mauvaiseCorrelation = await _hote.Sentier(s => s.EnvoyerSignalAsync(new Signal { Nom = "FIXE", CleProcessus = "correle", CleAffaire = 1, CleCorrelation = "lot-2" }));
        Assert.True(mauvaiseCorrelation.SansEffet);

        var fixe = await _hote.Sentier(s => s.EnvoyerSignalAsync(new Signal { Nom = "FIXE", CleProcessus = "correle", CleAffaire = 1, CleCorrelation = "lot-1" }));
        Assert.Equal(a.IdInstance, fixe.Resultats.Single().IdInstance);
    }

    [Fact]
    public async Task Une_correlation_tiree_d_une_variable_vide_est_une_erreur()
    {
        _hote.Publier(ConstructeurProcessus.Nouveau("correle-vide").Variables(Dossier).Attente("attendre", a => a.Signal("S", s => s.Correlation(Dossier))).Fin("fin").Construire());
        await Assert.ThrowsAsync<VariableInvalideException>(() => _hote.DemarrerAsync("correle-vide", 1));
    }

    [Fact]
    public async Task Echeance_a_date_fixe_ou_tiree_d_une_variable()
    {
        var date = new DateTime(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc);
        _hote.Publier(ConstructeurProcessus.Nouveau("dates")
            .Variables(Limite)
            .Attente("fixe", a => a.Echeance(date))
            .Attente("variable", a => a.Echeance(Limite))
            .Fin("fin")
            .Construire());

        var demarrage = await _hote.DemarrerAsync("dates", 1, ValeursVariables.Avec(Limite, date.AddDays(10)));
        Assert.Equal(date, (await _hote.Suivi(s => s.ObtenirAttentesAsync(demarrage.IdInstance!.Value))).Single().DateEcheance);

        _hote.Horloge.MaintenantUtc = date;
        var premiere = Assert.Single(await _hote.Sentier(s => s.ObtenirEcheancesDuesAsync()));
        await _hote.Sentier(s => s.TraiterEcheanceAsync(premiere.IdAttente));
        Assert.Equal(date.AddDays(10), (await _hote.Suivi(s => s.ObtenirAttentesAsync(demarrage.IdInstance!.Value))).Single().DateEcheance);
    }

    [Fact]
    public async Task Une_echeance_tiree_d_une_variable_vide_est_une_erreur()
    {
        _hote.Publier(ConstructeurProcessus.Nouveau("date-vide").Variables(Limite).Attente("w", a => a.Echeance(Limite)).Fin("fin").Construire());
        await Assert.ThrowsAsync<VariableInvalideException>(() => _hote.DemarrerAsync("date-vide", 1));
    }

    [Fact]
    public async Task Relance_en_boucle_jusqu_au_signal()
    {
        _hote.Publier(ConstructeurProcessus.Nouveau("relances")
            .Attente("attendre", a => a
                .Signal("REPONSE")
                .Echeance(TimeSpan.FromDays(1)).Alors(b => b.Systemique("relancer", "tracer").Vers("attendre")))
            .Fin("repondu")
            .Construire());

        var demarrage = await _hote.DemarrerAsync("relances", 1);
        for (var i = 0; i < 3; i++)
        {
            _hote.Horloge.Avancer(TimeSpan.FromDays(1));
            foreach (var due in await _hote.Sentier(s => s.ObtenirEcheancesDuesAsync()))
            {
                await _hote.Sentier(s => s.TraiterEcheanceAsync(due.IdAttente));
            }
        }

        Assert.Equal(3, _hote.Journal.Entrees.Count(e => e == "relancer:Execution"));
        Assert.Equal(StatutInstance.Suspendue, (await _hote.InstanceAsync(demarrage.IdInstance)).Statut);
        Assert.Equal(2, (await _hote.Suivi(s => s.ObtenirAttentesAsync(demarrage.IdInstance!.Value))).Count);

        await _hote.Sentier(s => s.EnvoyerSignalAsync("REPONSE", "relances", 1));
        Assert.Equal("repondu", (await _hote.InstanceAsync(demarrage.IdInstance)).CodeFin);
    }
}
