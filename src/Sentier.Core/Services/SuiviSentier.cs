using Sentier.Definitions;
using Sentier.Execution;
using Sentier.Persistance;

namespace Sentier.Services;

/// <summary>Implémentation de <see cref="ISuiviSentier"/>.</summary>
internal sealed class SuiviSentier(
    MoteurSentier moteur,
    FournisseurDefinitions definitions,
    IDepotInstances instances,
    IDepotHistorique historique,
    IDepotAttentes attentes) : ISuiviSentier
{
    public async Task<EtatInstance?> ObtenirInstanceAsync(IdInstance idInstance, CancellationToken ct = default)
    {
        var instance = await instances.ObtenirAsync(idInstance, ct);
        return instance is null ? null : await VersEtatAsync(instance, ct);
    }

    public async Task<IReadOnlyList<EtatInstance>> ObtenirInstancesParCleAffaireAsync(long cleAffaire, IReadOnlyCollection<string> clesProcessus, IReadOnlyCollection<StatutInstance>? statuts = null, CancellationToken ct = default)
    {
        if (clesProcessus.Count == 0)
        {
            throw new ArgumentException("Une clé d'affaire est toujours accompagnée d'au moins une clé de processus.", nameof(clesProcessus));
        }

        return await VersEtatsAsync(await instances.RechercherAsync(cleAffaire, clesProcessus, statuts, ct), ct);
    }

    public Task<IReadOnlyList<PassageNoeud>> ObtenirHistoriqueAsync(IdInstance idInstance, CancellationToken ct = default)
        => historique.ObtenirAsync(idInstance, ct);

    public Task<IReadOnlyList<Attente>> ObtenirAttentesAsync(IdInstance idInstance, CancellationToken ct = default)
        => attentes.ObtenirParInstanceAsync(idInstance, ct);

    public async Task<IReadOnlyList<TacheUtilisateur>> ObtenirTachesOuvertesAsync(IdInstance idInstance, CancellationToken ct = default)
        => moteur.TachesOptionnelles is { } taches ? await taches.ObtenirTachesOuvertesAsync(idInstance, ct) : [];

    public async Task<IReadOnlyList<EtatInstance>> ObtenirInstancesEnErreurAsync(int page = 0, int taille = 50, CancellationToken ct = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(page);
        ArgumentOutOfRangeException.ThrowIfLessThan(taille, 1);
        return await VersEtatsAsync(await instances.ObtenirEnErreurAsync(page * taille, taille, ct), ct);
    }

    public Task<IReadOnlyList<CompteInstancesActives>> CompterInstancesActivesAsync(string cleProcessus, CancellationToken ct = default)
        => instances.CompterActivesAsync(cleProcessus, ct);

    public async Task<string> ExporterDiagrammeAsync(string cleProcessus, int? version = null, CancellationToken ct = default)
    {
        var definition = version is { } numero
            ? await definitions.ObtenirParVersionAsync(cleProcessus, numero, ct) ?? throw new ProcessusIntrouvableException(cleProcessus, numero)
            : await definitions.ObtenirActiveAsync(cleProcessus, ct) ?? throw new ProcessusIntrouvableException(cleProcessus);
        return ExportateurMermaid.Exporter(definition);
    }

    private async Task<IReadOnlyList<EtatInstance>> VersEtatsAsync(IReadOnlyList<InstanceProcessus> liste, CancellationToken ct)
    {
        var etats = new List<EtatInstance>(liste.Count);
        foreach (var instance in liste)
        {
            etats.Add(await VersEtatAsync(instance, ct));
        }

        return etats;
    }

    private async Task<EtatInstance> VersEtatAsync(InstanceProcessus instance, CancellationToken ct)
    {
        var definition = await definitions.ObtenirAsync(instance.IdDefinition, ct);
        return new EtatInstance
        {
            Id = instance.Id,
            CleProcessus = instance.CleProcessus,
            IdDefinition = instance.IdDefinition,
            VersionDefinition = definition.Version,
            CleAffaire = instance.CleAffaire,
            Statut = instance.Statut,
            CodeNoeudCourant = instance.CodeNoeudCourant,
            IdTacheCourante = instance.IdTacheCourante,
            CodeFin = instance.CodeFin,
            IdInstanceParent = instance.IdInstanceParent,
            Variables = moteur.ChargerVariables(instance, definition).Valeurs,
            DateCreation = instance.DateCreation,
            DateMaj = instance.DateMaj,
            DateFin = instance.DateFin,
            MessageErreur = instance.MessageErreur,
            DateErreur = instance.DateErreur,
        };
    }
}
