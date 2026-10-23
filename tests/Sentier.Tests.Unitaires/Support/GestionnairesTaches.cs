namespace Sentier.Tests.Unitaires.Support;

/// <summary>Bassin de tâches en mémoire, partagé par les portées d'un hôte (le « stockage du client »).</summary>
public sealed class BassinTaches
{
    private readonly List<TacheEnregistree> _taches = [];

    public IReadOnlyList<TacheEnregistree> Taches
    {
        get
        {
            lock (_taches)
            {
                return _taches.ToList();
            }
        }
    }

    public TacheEnregistree Tache(long id) => Taches.Single(t => t.Id == id);

    internal TacheEnregistree Ajouter(DemandeTacheUtilisateur demande)
    {
        lock (_taches)
        {
            var tache = new TacheEnregistree(_taches.Count + 1, demande);
            _taches.Add(tache);
            return tache;
        }
    }
}

/// <summary>Tâche telle que stockée par le client.</summary>
public sealed class TacheEnregistree(long id, DemandeTacheUtilisateur demande)
{
    public long Id { get; } = id;

    public DemandeTacheUtilisateur Demande { get; } = demande;

    public string CodeNoeud { get; set; } = demande.CodeNoeud;

    public StatutTache Statut { get; set; } = StatutTache.Ouverte;

    public IReadOnlyDictionary<string, object?>? DonneesCompletion { get; set; }

    public string? Utilisateur { get; set; }

    public string? MotifAnnulation { get; set; }

    public int NombreCompletions { get; set; }

    public TacheUtilisateur VersSentier() => new() { Id = Id, IdInstance = Demande.IdInstance, CodeNoeud = CodeNoeud, Statut = Statut };
}

/// <summary>Gestionnaire de tâches du client, utilisé par les tests du moteur.</summary>
public sealed class GestionnaireTachesTest(BassinTaches bassin) : IGestionnaireTaches
{
    public Task<long> CreerTacheAsync(DemandeTacheUtilisateur demande, CancellationToken ct = default)
        => Task.FromResult(bassin.Ajouter(demande).Id);

    public Task<TacheUtilisateur?> ObtenirTacheAsync(long idTache, CancellationToken ct = default)
        => Task.FromResult(bassin.Taches.FirstOrDefault(t => t.Id == idTache)?.VersSentier());

    public Task<IReadOnlyList<TacheUtilisateur>> ObtenirTachesOuvertesAsync(IdInstance idInstance, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<TacheUtilisateur>>(bassin.Taches
            .Where(t => t.Demande.IdInstance == idInstance && t.Statut == StatutTache.Ouverte)
            .Select(t => t.VersSentier())
            .ToList());

    public Task CompleterTacheAsync(long idTache, IReadOnlyDictionary<string, object?>? donnees, string? utilisateur, CancellationToken ct = default)
    {
        var tache = bassin.Tache(idTache);
        tache.Statut = StatutTache.Completee;
        tache.DonneesCompletion = donnees;
        tache.Utilisateur = utilisateur;
        tache.NombreCompletions++;
        return Task.CompletedTask;
    }

    public Task AnnulerTacheAsync(long idTache, string motif, CancellationToken ct = default)
    {
        var tache = bassin.Tache(idTache);
        tache.Statut = StatutTache.Annulee;
        tache.MotifAnnulation = motif;
        return Task.CompletedTask;
    }

    public Task RenommerNoeudAsync(IdInstance idInstance, string ancienCode, string nouveauCode, CancellationToken ct = default)
    {
        foreach (var tache in bassin.Taches.Where(t => t.Demande.IdInstance == idInstance && t.CodeNoeud == ancienCode))
        {
            tache.CodeNoeud = nouveauCode;
        }

        return Task.CompletedTask;
    }
}

/// <summary>Seconde implémentation : sert à vérifier le choix du gestionnaire lors de l'enregistrement.</summary>
public sealed class AutreGestionnaireTaches : IGestionnaireTaches
{
    public Task<long> CreerTacheAsync(DemandeTacheUtilisateur demande, CancellationToken ct = default) => throw new NotSupportedException();

    public Task<TacheUtilisateur?> ObtenirTacheAsync(long idTache, CancellationToken ct = default) => throw new NotSupportedException();

    public Task<IReadOnlyList<TacheUtilisateur>> ObtenirTachesOuvertesAsync(IdInstance idInstance, CancellationToken ct = default) => throw new NotSupportedException();

    public Task CompleterTacheAsync(long idTache, IReadOnlyDictionary<string, object?>? donnees, string? utilisateur, CancellationToken ct = default) => throw new NotSupportedException();

    public Task AnnulerTacheAsync(long idTache, string motif, CancellationToken ct = default) => throw new NotSupportedException();

    public Task RenommerNoeudAsync(IdInstance idInstance, string ancienCode, string nouveauCode, CancellationToken ct = default) => throw new NotSupportedException();
}

/// <summary>Décorateur : ignoré par le balayage, car son constructeur reçoit un <see cref="IGestionnaireTaches"/>.</summary>
public sealed class DecorateurTaches(IGestionnaireTaches interne) : IGestionnaireTaches
{
    public Task<long> CreerTacheAsync(DemandeTacheUtilisateur demande, CancellationToken ct = default) => interne.CreerTacheAsync(demande, ct);

    public Task<TacheUtilisateur?> ObtenirTacheAsync(long idTache, CancellationToken ct = default) => interne.ObtenirTacheAsync(idTache, ct);

    public Task<IReadOnlyList<TacheUtilisateur>> ObtenirTachesOuvertesAsync(IdInstance idInstance, CancellationToken ct = default) => interne.ObtenirTachesOuvertesAsync(idInstance, ct);

    public Task CompleterTacheAsync(long idTache, IReadOnlyDictionary<string, object?>? donnees, string? utilisateur, CancellationToken ct = default) => interne.CompleterTacheAsync(idTache, donnees, utilisateur, ct);

    public Task AnnulerTacheAsync(long idTache, string motif, CancellationToken ct = default) => interne.AnnulerTacheAsync(idTache, motif, ct);

    public Task RenommerNoeudAsync(IdInstance idInstance, string ancienCode, string nouveauCode, CancellationToken ct = default) => interne.RenommerNoeudAsync(idInstance, ancienCode, nouveauCode, ct);
}
