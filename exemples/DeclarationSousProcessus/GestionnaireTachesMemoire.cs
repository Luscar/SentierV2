using Sentier;

namespace Exemples.DeclarationSousProcessus;

/// <summary>
/// Gestionnaire de tâches en mémoire ; les tâches sont portées par les sous-processus. Dans l'application, il s'appuie sur le bassin de tâches existant
/// et utilise la même transaction que Sentier.
/// </summary>
public sealed class GestionnaireTachesMemoire : IGestionnaireTaches
{
    private static readonly List<(TacheSentier Tache, DemandeTacheSentier Demande)> Taches = [];

    public Task<long> CreerTacheAsync(DemandeTacheSentier demande, CancellationToken ct = default)
    {
        var tache = new TacheSentier { Id = Taches.Count + 1, IdInstance = demande.IdInstance, CodeNoeud = demande.CodeNoeud, Statut = StatutTacheSentier.Ouverte };
        Taches.Add((tache, demande));
        Journal.Ecrire($"Tâche {tache.Id} « {demande.NomTache} » créée pour le rôle {demande.Role}");
        return Task.FromResult(tache.Id);
    }

    public Task<TacheSentier?> ObtenirTacheAsync(long idTache, CancellationToken ct = default)
        => Task.FromResult(Taches.Select(t => t.Tache).FirstOrDefault(t => t.Id == idTache));

    public Task<IReadOnlyList<TacheSentier>> ObtenirTachesOuvertesAsync(IdInstance idInstance, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<TacheSentier>>(Taches.Select(t => t.Tache).Where(t => t.IdInstance == idInstance && t.Statut == StatutTacheSentier.Ouverte).ToList());

    public Task CompleterTacheAsync(long idTache, IReadOnlyDictionary<string, object?>? donnees, string? utilisateur, CancellationToken ct = default)
        => Changer(idTache, StatutTacheSentier.Completee, null);

    public Task AssignerTacheAsync(long idTache, string utilisateur, CancellationToken ct = default)
    {
        Journal.Ecrire($"Tâche {idTache} assignée à {utilisateur}");
        return Task.CompletedTask;
    }

    public Task AnnulerTacheAsync(long idTache, string motif, CancellationToken ct = default)
        => Changer(idTache, StatutTacheSentier.Annulee, null);

    public Task RenommerNoeudAsync(IdInstance idInstance, string ancienCode, string nouveauCode, CancellationToken ct = default)
    {
        for (var i = 0; i < Taches.Count; i++)
        {
            if (Taches[i].Tache.IdInstance == idInstance && Taches[i].Tache.CodeNoeud == ancienCode)
            {
                Taches[i] = (Copier(Taches[i].Tache, Taches[i].Tache.Statut, nouveauCode), Taches[i].Demande);
            }
        }

        return Task.CompletedTask;
    }

    private static Task Changer(long idTache, StatutTacheSentier statut, string? codeNoeud)
    {
        var i = Taches.FindIndex(t => t.Tache.Id == idTache);
        Taches[i] = (Copier(Taches[i].Tache, statut, codeNoeud), Taches[i].Demande);
        return Task.CompletedTask;
    }

    private static TacheSentier Copier(TacheSentier tache, StatutTacheSentier statut, string? codeNoeud)
        => new() { Id = tache.Id, IdInstance = tache.IdInstance, CodeNoeud = codeNoeud ?? tache.CodeNoeud, Statut = statut };
}
