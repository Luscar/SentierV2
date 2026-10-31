using Sentier;

namespace Exemples.DeclarationSousProcessus;

/// <summary>
/// Gestionnaire de tâches en mémoire ; les tâches sont portées par les sous-processus. Dans l'application, il s'appuie sur le bassin de tâches existant
/// et utilise la même transaction que Sentier.
/// </summary>
public sealed class GestionnaireTachesMemoire : IGestionnaireTaches
{
    private static readonly List<(TacheUtilisateur Tache, DemandeTacheUtilisateur Demande)> Taches = [];

    public Task<long> CreerTacheAsync(DemandeTacheUtilisateur demande, CancellationToken ct = default)
    {
        var tache = new TacheUtilisateur { Id = Taches.Count + 1, IdInstance = demande.IdInstance, CodeNoeud = demande.CodeNoeud, Statut = StatutTache.Ouverte };
        Taches.Add((tache, demande));
        Journal.Ecrire($"Tâche {tache.Id} « {demande.Nom} » créée pour le rôle {demande.Role}");
        return Task.FromResult(tache.Id);
    }

    public Task<TacheUtilisateur?> ObtenirTacheAsync(long idTache, CancellationToken ct = default)
        => Task.FromResult(Taches.Select(t => t.Tache).FirstOrDefault(t => t.Id == idTache));

    public Task<IReadOnlyList<TacheUtilisateur>> ObtenirTachesOuvertesAsync(IdInstance idInstance, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<TacheUtilisateur>>(Taches.Select(t => t.Tache).Where(t => t.IdInstance == idInstance && t.Statut == StatutTache.Ouverte).ToList());

    public Task CompleterTacheAsync(long idTache, IReadOnlyDictionary<string, object?>? donnees, string? utilisateur, CancellationToken ct = default)
        => Changer(idTache, StatutTache.Completee, null);

    public Task AssignerTacheAsync(long idTache, string utilisateur, CancellationToken ct = default)
    {
        Journal.Ecrire($"Tâche {idTache} assignée à {utilisateur}");
        return Task.CompletedTask;
    }

    public Task AnnulerTacheAsync(long idTache, string motif, CancellationToken ct = default)
        => Changer(idTache, StatutTache.Annulee, null);

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

    private static Task Changer(long idTache, StatutTache statut, string? codeNoeud)
    {
        var i = Taches.FindIndex(t => t.Tache.Id == idTache);
        Taches[i] = (Copier(Taches[i].Tache, statut, codeNoeud), Taches[i].Demande);
        return Task.CompletedTask;
    }

    private static TacheUtilisateur Copier(TacheUtilisateur tache, StatutTache statut, string? codeNoeud)
        => new() { Id = tache.Id, IdInstance = tache.IdInstance, CodeNoeud = codeNoeud ?? tache.CodeNoeud, Statut = statut };
}
