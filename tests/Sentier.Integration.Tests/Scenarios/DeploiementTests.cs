using Sentier.Integration.Tests.Infrastructure;
using Sentier.Integration.Tests.Processus;

namespace Sentier.Integration.Tests.Scenarios;

/// <summary>Installation, déploiement et versions des définitions.</summary>
public sealed class DeploiementTests : IAsyncLifetime
{
    private EnvironnementSentier _env = null!;

    public async Task InitializeAsync() => _env = await EnvironnementSentier.DemarrerAsync();

    public async Task DisposeAsync() => await _env.DisposeAsync();

    [Fact]
    public async Task Redeployer_des_definitions_identiques_ne_cree_aucune_version()
    {
        var rapport = await _env.DeployerAsync();

        Assert.NotEmpty(rapport.Definitions);
        Assert.All(rapport.Definitions, d =>
        {
            Assert.Equal(EtatDeploiement.Inchangee, d.Etat);
            Assert.Equal(1, d.Version);
        });
        Assert.Contains(rapport.Definitions, d => d.CleProcessus == DemandeConge.Cle);
    }

    [Fact]
    public async Task Une_definition_modifiee_cree_une_nouvelle_version_et_les_instances_gardent_la_leur()
    {
        var ancienne = await _env.DemarrerAsync(Evolutif.Cle, 1);

        _env.Configuration.EtapeControleAjoutee = true;
        await _env.RedemarrerAsync();
        var rapport = await _env.DeployerAsync();
        var nouvelle = await _env.DemarrerAsync(Evolutif.Cle, 2);

        var resultat = Assert.Single(rapport.Definitions, d => d.CleProcessus == Evolutif.Cle);
        Assert.Equal(EtatDeploiement.NouvelleVersion, resultat.Etat);
        Assert.Equal(2, resultat.Version);
        Assert.All(rapport.Definitions.Where(d => d.CleProcessus != Evolutif.Cle), d => Assert.Equal(EtatDeploiement.Inchangee, d.Etat));

        Assert.Equal(1, (await _env.InstanceAsync(ancienne.IdInstance!.Value)).VersionDefinition);
        Assert.Equal(2, (await _env.InstanceAsync(nouvelle.IdInstance!.Value)).VersionDefinition);
        Assert.Contains("controler", (await _env.HistoriqueAsync(nouvelle.IdInstance!.Value)).Select(p => p.CodeNoeud));

        // L'ancienne instance termine sur sa version d'origine.
        var fin = await _env.CompleterTacheAsync(ancienne.IdTacheCourante!.Value);
        Assert.Equal("fin", fin.CodeFin);
        Assert.DoesNotContain("controler", (await _env.HistoriqueAsync(ancienne.IdInstance!.Value)).Select(p => p.CodeNoeud));

        // Le diagramme suit la dernière version, ou celle demandée.
        Assert.Contains("controler", await _env.LireAsync(s => s.Suivi().ExporterDiagrammeAsync(Evolutif.Cle)));
        Assert.DoesNotContain("controler", await _env.LireAsync(s => s.Suivi().ExporterDiagrammeAsync(Evolutif.Cle, version: 1)));
    }

    [Fact]
    public async Task Le_comptage_des_instances_actives_distingue_les_versions()
    {
        await _env.DemarrerAsync(Evolutif.Cle, 1);
        _env.Configuration.EtapeControleAjoutee = true;
        await _env.RedemarrerAsync();
        await _env.DeployerAsync();
        await _env.DemarrerAsync(Evolutif.Cle, 2);
        await _env.DemarrerAsync(Evolutif.Cle, 3);

        var comptes = await _env.LireAsync(s => s.Suivi().CompterInstancesActivesAsync(Evolutif.Cle));

        Assert.Equal(1, comptes.Single(c => c.Version == 1).Nombre);
        Assert.Equal(2, comptes.Single(c => c.Version == 2).Nombre);
    }
}
