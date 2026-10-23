using System.Data;
using Dapper;
using Exemples.DeclarationSousProcessus;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Sentier.DependencyInjection;
using Sentier.Persistance.Dapper;

namespace Sentier.Tests.Integration;

/// <summary>
/// Scénario de l'exemple <c>exemples/DeclarationSousProcessus</c>, rejoué et vérifié étape par étape :
/// un parent composé de quatre sous-processus (corrections, autorisation, signal, délai, retour comptable).
/// </summary>
public sealed class ExempleDeclarationTests : IDisposable
{
    private const long Numero = 1001;

    private static readonly string[] Processus =
        ["traitement-declaration", "identification-declaration", "conformite-declaration", "verification-declaration", "comptabilisation-declaration"];

    private readonly SqliteConnection _connexion = new("Data Source=:memory:");
    private readonly Simulateur _simulateur = new() { RejetsIdentification = 1, MontantAttendu = 1250.00m };
    private readonly HorlogeSimulee _horloge = new();
    private readonly ServiceProvider _fournisseur;

    public ExempleDeclarationTests()
    {
        _connexion.Open();
        var services = new ServiceCollection();
        services.AddSingleton<IDbConnection>(_connexion);
        services.AddSentier(o => o.UtiliserSqlite("SENTIER").BalayerAssemblyDe<TraitementDeclaration>());
        services.AddSingleton<IHorloge>(_horloge);
        services.AddSingleton<IDossiersDeclarations>(_simulateur);
        services.AddSingleton<IServiceIdentification>(_simulateur);
        services.AddSingleton<IServiceConformite>(_simulateur);
        services.AddSingleton<IServiceVerification>(_simulateur);
        services.AddSingleton<IServiceComptable>(_simulateur);
        _fournisseur = services.BuildServiceProvider();
    }

    public void Dispose()
    {
        _fournisseur.Dispose();
        _connexion.Dispose();
    }

    private async Task<T> Transaction<T>(Func<IServiceProvider, Task<T>> action)
    {
        using var portee = _fournisseur.CreateScope();
        using var transaction = _connexion.BeginTransaction();
        portee.ServiceProvider.GetRequiredService<IAccesseurTransaction>().Transaction = transaction;
        var resultat = await action(portee.ServiceProvider);
        transaction.Commit();
        return resultat;
    }

    private Task<T> Sentier<T>(Func<IServiceSentier, Task<T>> action) => Transaction(s => action(s.GetRequiredService<IServiceSentier>()));

    private Task<IReadOnlyList<EtatInstance>> Instances() => Transaction(s => s.GetRequiredService<ISuiviSentier>().ObtenirInstancesParCleAffaireAsync(Numero, Processus));

    /// <summary>Le sous-processus actif et son nœud courant (le parent attend sur le nœud du sous-processus).</summary>
    private async Task<(string Processus, string Noeud)> EnfantActif()
    {
        var actifs = (await Instances()).Where(i => i.IdInstanceParent is not null && i.Statut == StatutInstance.Suspendue).ToList();
        var enfant = Assert.Single(actifs);
        return (enfant.CleProcessus, enfant.CodeNoeudCourant);
    }

    private async Task CompleterTache()
    {
        var idTache = (await Instances()).Single(i => i.IdTacheCourante is not null).IdTacheCourante!.Value;
        var resultat = await Sentier(s => s.CompleterTacheAsync(idTache, utilisateur: "agent"));
        Assert.False(resultat.SansEffet);
    }

    [Fact]
    public async Task La_declaration_traverse_chaque_sous_processus_jusqu_a_la_fin()
    {
        await Transaction(async s =>
        {
            await s.GetRequiredService<InstallateurSchemaSentier>().InstallerAsync();
            return await s.GetRequiredService<IServiceSentier>().DeployerDefinitionsAsync();
        });

        // Réception : la déclaration est enregistrée chez le client, puis le traitement démarre dans la même transaction.
        var demarrage = await Transaction(async s =>
        {
            await s.GetRequiredService<IDossiersDeclarations>().EnregistrerAsync(new Declaration(Numero, "2026-02", "ORIGINALE", 1205.00m), default);
            return await s.GetRequiredService<IServiceSentier>().DemarrerAsync("traitement-declaration", Numero);
        });
        Assert.Equal((StatutInstance.Suspendue, "identification"), (demarrage.Statut, demarrage.CodeNoeudCourant));
        Assert.Equal(("identification-declaration", "corriger-rejet-identification"), await EnfantActif());

        // Correction du rejet d'identification : l'identification est reprise et réussit ; la conformité rejette le montant.
        await CompleterTache();
        Assert.Equal(("conformite-declaration", "corriger-rejet-conformite"), await EnfantActif());

        // Correction du montant, puis autorisation refusée : retour en correction.
        _simulateur.CorrigerMontant(Numero, 1250.00m);
        await CompleterTache();
        Assert.Equal(("conformite-declaration", "autoriser-correction-conformite"), await EnfantActif());
        _simulateur.DeciderAutorisation(Numero, false);
        await CompleterTache();
        Assert.Equal(("conformite-declaration", "corriger-rejet-conformite"), await EnfantActif());

        // Correction reprise, puis autorisée : la conformité est revalidée et la vérification démarre.
        await CompleterTache();
        _simulateur.DeciderAutorisation(Numero, true);
        await CompleterTache();
        Assert.Equal(("verification-declaration", "attendre-retour-verification"), await EnfantActif());

        // Retour du système de vérification (signal par processus + clé d'affaire).
        var verification = await Sentier(s => s.EnvoyerSignalAsync(VerificationDeclaration.SignalRetourVerification, "verification-declaration", Numero));
        Assert.Equal(1, verification.NombreInstancesReprises);
        Assert.Equal(("comptabilisation-declaration", "attendre-comptabilisation"), await EnfantActif());

        // Délai de comptabilisation : rien n'est dû avant, le batch le traite après.
        Assert.Empty(await Sentier(s => s.ObtenirEcheancesDuesAsync()));
        _horloge.MaintenantUtc += ComptabilisationDeclaration.DelaiComptabilisation + TimeSpan.FromMinutes(1);
        var due = Assert.Single(await Sentier(s => s.ObtenirEcheancesDuesAsync()));
        Assert.Equal(("comptabilisation-declaration", Numero), (due.CleProcessus, due.CleAffaire));
        await Sentier(s => s.TraiterEcheanceAsync(due.IdAttente));
        Assert.Equal(("comptabilisation-declaration", "attendre-retour-comptable"), await EnfantActif());

        // Retour comptable avec rejet : correction, puis fin de la comptabilisation et du traitement.
        _simulateur.RecevoirRetourComptable(Numero, "CP-03");
        await Sentier(s => s.EnvoyerSignalAsync(ComptabilisationDeclaration.SignalRetourComptable, "comptabilisation-declaration", Numero));
        Assert.Equal(("comptabilisation-declaration", "corriger-rejet-comptable"), await EnfantActif());
        await CompleterTache();

        var instances = await Instances();
        Assert.All(instances, i => Assert.Equal(StatutInstance.Terminee, i.Statut));
        Assert.Equal(
            [
                ("traitement-declaration", "traitee"),
                ("identification-declaration", "identifiee"),
                ("conformite-declaration", "conforme"),
                ("verification-declaration", "verifiee"),
                ("comptabilisation-declaration", "comptabilisee"),
            ],
            instances.OrderBy(i => i.Id.Valeur).Select(i => (i.CleProcessus, i.CodeFin!)));

        var parent = instances.Single(i => i.IdInstanceParent is null);
        Assert.All(instances.Where(i => i != parent), i => Assert.Equal(parent.Id, i.IdInstanceParent));
        var historiqueParent = await Transaction(s => s.GetRequiredService<ISuiviSentier>().ObtenirHistoriqueAsync(parent.Id));
        Assert.Equal(["debut", "identification", "conformite", "verification", "comptabilisation", "traitee"], historiqueParent.Select(p => p.CodeNoeud));

        var conformite = instances.Single(i => i.CleProcessus == "conformite-declaration");
        var historiqueConformite = await Transaction(s => s.GetRequiredService<ISuiviSentier>().ObtenirHistoriqueAsync(conformite.Id));
        Assert.Equal(2, historiqueConformite.Count(p => p.CodeNoeud == "valider-conformite"));
        Assert.Equal(2, historiqueConformite.Count(p => p.CodeNoeud == "corriger-rejet-conformite"));
        Assert.Equal(2, historiqueConformite.Count(p => p.CodeNoeud == "autoriser-correction-conformite"));

        Assert.Equal(0L, _connexion.ExecuteScalar<long>("SELECT COUNT(*) FROM SENTIER_ATTEN_PROCS"));
    }
}
