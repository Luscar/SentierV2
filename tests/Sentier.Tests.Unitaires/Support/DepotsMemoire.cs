using Sentier.Definitions;
using Sentier.Persistance;

namespace Sentier.Tests.Unitaires.Support;

// Dépôts en mémoire : ils reproduisent la sémantique des dépôts Dapper (copies à la lecture comme à l'écriture,
// verrou optimiste, index unique partiel) sans aucune base de données.

/// <summary>Base en mémoire partagée par les dépôts d'un même hôte.</summary>
public sealed class BaseMemoire
{
    public BaseMemoire()
    {
        Definitions = new DepotDefinitionsMemoire();
        Instances = new DepotInstancesMemoire(Definitions);
        Historique = new DepotHistoriqueMemoire();
        Attentes = new DepotAttentesMemoire(Instances);
        Migrations = new DepotMigrationsMemoire();
    }

    public DepotDefinitionsMemoire Definitions { get; }

    public DepotInstancesMemoire Instances { get; }

    public DepotHistoriqueMemoire Historique { get; }

    public DepotAttentesMemoire Attentes { get; }

    public DepotMigrationsMemoire Migrations { get; }

    /// <summary>
    /// Instantané de toute la base : l'action retournée la restaure, comme l'annulation d'une transaction.
    /// </summary>
    public Action Sauvegarder()
    {
        var restaurations = new[] { Definitions.Sauvegarder(), Instances.Sauvegarder(), Historique.Sauvegarder(), Attentes.Sauvegarder(), Migrations.Sauvegarder() };
        return () =>
        {
            foreach (var restaurer in restaurations)
            {
                restaurer();
            }
        };
    }
}

public sealed class DepotDefinitionsMemoire : IDepotDefinitions
{
    private readonly List<DefinitionStockee> _lignes = [];
    private readonly object _verrou = new();

    public IReadOnlyList<DefinitionStockee> Lignes
    {
        get
        {
            lock (_verrou)
            {
                return _lignes.ToList();
            }
        }
    }

    internal Action Sauvegarder()
    {
        lock (_verrou)
        {
            var copie = _lignes.Select(Copier).ToList();
            return () =>
            {
                lock (_verrou)
                {
                    _lignes.Clear();
                    _lignes.AddRange(copie.Select(Copier));
                }
            };
        }
    }

    private static DefinitionStockee Copier(DefinitionStockee d) => new()
    {
        Id = d.Id,
        CleProcessus = d.CleProcessus,
        Version = d.Version,
        Nom = d.Nom,
        InstanceUnique = d.InstanceUnique,
        MigrationAuto = d.MigrationAuto,
        Contenu = d.Contenu,
        Empreinte = d.Empreinte,
        EstActive = d.EstActive,
        DateCreation = d.DateCreation,
    };

    /// <summary>Publie directement une définition construite en mémoire (sans passer par le déploiement).</summary>
    public DefinitionStockee Publier(DefinitionProcessus definition, DateTime? date = null)
    {
        var contenu = SerialisationDefinition.Serialiser(definition);
        var stockee = new DefinitionStockee
        {
            CleProcessus = definition.CleProcessus,
            Version = (Lignes.Where(l => l.CleProcessus == definition.CleProcessus).Select(l => l.Version).DefaultIfEmpty().Max()) + 1,
            Nom = definition.Nom,
            InstanceUnique = definition.InstanceUnique,
            MigrationAuto = definition.MigrationAutomatique,
            Contenu = contenu,
            Empreinte = SerialisationDefinition.CalculerEmpreinte(contenu),
            EstActive = true,
            DateCreation = date ?? new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        };
        InsererAsync(stockee, default).GetAwaiter().GetResult();
        return stockee;
    }

    public Task<DefinitionStockee?> ObtenirDerniereAsync(string cleProcessus, CancellationToken ct)
        => Task.FromResult(Lignes.Where(l => l.CleProcessus == cleProcessus).OrderByDescending(l => l.Version).FirstOrDefault());

    public Task<IdDefinition?> ObtenirIdActifAsync(string cleProcessus, CancellationToken ct)
        => Task.FromResult(Lignes.Where(l => l.CleProcessus == cleProcessus && l.EstActive).OrderByDescending(l => l.Version).Select(l => (IdDefinition?)l.Id).FirstOrDefault());

    public Task<DefinitionStockee?> ObtenirAsync(IdDefinition id, CancellationToken ct)
        => Task.FromResult(Lignes.FirstOrDefault(l => l.Id == id));

    public Task<DefinitionStockee?> ObtenirParVersionAsync(string cleProcessus, int version, CancellationToken ct)
        => Task.FromResult(Lignes.FirstOrDefault(l => l.CleProcessus == cleProcessus && l.Version == version));

    public Task InsererAsync(DefinitionStockee definition, CancellationToken ct)
    {
        lock (_verrou)
        {
            definition.Id = new IdDefinition(_lignes.Count + 1);
            foreach (var active in _lignes.Where(l => l.CleProcessus == definition.CleProcessus))
            {
                active.EstActive = false;
            }

            _lignes.Add(definition);
        }

        return Task.CompletedTask;
    }
}

public sealed class DepotInstancesMemoire(DepotDefinitionsMemoire definitions) : IDepotInstances
{
    private readonly Dictionary<long, InstanceProcessus> _lignes = [];
    private readonly object _verrou = new();

    /// <summary>Copie de toutes les instances, pour les assertions.</summary>
    public IReadOnlyList<InstanceProcessus> Toutes
    {
        get
        {
            lock (_verrou)
            {
                return _lignes.Values.OrderBy(i => i.Id.Valeur).Select(Copier).ToList();
            }
        }
    }

    internal Action Sauvegarder()
    {
        lock (_verrou)
        {
            var copie = _lignes.Values.Select(Copier).ToList();
            return () =>
            {
                lock (_verrou)
                {
                    _lignes.Clear();
                    foreach (var instance in copie)
                    {
                        _lignes[instance.Id.Valeur] = Copier(instance);
                    }
                }
            };
        }
    }

    public Task<InstanceProcessus?> ObtenirAsync(IdInstance id, CancellationToken ct)
    {
        lock (_verrou)
        {
            return Task.FromResult(_lignes.TryGetValue(id.Valeur, out var ligne) ? Copier(ligne) : null);
        }
    }

    public Task InsererAsync(InstanceProcessus instance, CancellationToken ct)
    {
        lock (_verrou)
        {
            if (instance.InstanceUnique && _lignes.Values.Any(i => i.InstanceUnique && i.EstActive && i.CleProcessus == instance.CleProcessus && i.CleAffaire == instance.CleAffaire))
            {
                throw new InvalidOperationException("Violation de l'index unique partiel des instances actives.");
            }

            instance.Id = new IdInstance(_lignes.Count + 1);
            _lignes[instance.Id.Valeur] = Copier(instance);
        }

        return Task.CompletedTask;
    }

    public Task MettreAJourAsync(InstanceProcessus instance, CancellationToken ct)
    {
        lock (_verrou)
        {
            if (!_lignes.TryGetValue(instance.Id.Valeur, out var existante) || existante.Version != instance.Version)
            {
                throw new ConflitConcurrenceException(instance.Id, instance.Version);
            }

            instance.Version++;
            _lignes[instance.Id.Valeur] = Copier(instance);
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<InstanceProcessus>> ObtenirActivesAsync(string cleProcessus, long cleAffaire, CancellationToken ct)
        => Lister(i => i.CleProcessus == cleProcessus && i.CleAffaire == cleAffaire && i.EstActive);

    public Task<IReadOnlyList<InstanceProcessus>> RechercherAsync(long cleAffaire, IReadOnlyCollection<string> clesProcessus, IReadOnlyCollection<StatutInstance>? statuts, CancellationToken ct)
        => Lister(i => i.CleAffaire == cleAffaire && clesProcessus.Contains(i.CleProcessus) && (statuts is null || statuts.Contains(i.Statut)));

    public Task<IReadOnlyList<InstanceProcessus>> ObtenirEnfantsActifsAsync(IdInstance idParent, CancellationToken ct)
        => Lister(i => i.IdInstanceParent == idParent && i.EstActive);

    public async Task<IReadOnlyList<InstanceProcessus>> ObtenirEnErreurAsync(int decalage, int taille, CancellationToken ct)
        => (await Lister(i => i.Statut == StatutInstance.EnErreur))
            .OrderByDescending(i => i.DateErreur).ThenBy(i => i.Id.Valeur).Skip(decalage).Take(taille).ToList();

    public async Task<IReadOnlyList<CompteInstancesActives>> CompterActivesAsync(string cleProcessus, CancellationToken ct)
    {
        var actives = await Lister(i => i.CleProcessus == cleProcessus && i.EstActive);
        var comptes = new List<CompteInstancesActives>();
        foreach (var groupe in actives.GroupBy(i => (i.IdDefinition, i.CodeNoeudCourant)))
        {
            var definition = await definitions.ObtenirAsync(groupe.Key.IdDefinition, ct);
            comptes.Add(new CompteInstancesActives(groupe.Key.IdDefinition, definition!.Version, groupe.Key.CodeNoeudCourant, groupe.Count()));
        }

        return comptes.OrderBy(c => c.Version).ThenBy(c => c.CodeNoeud, StringComparer.Ordinal).ToList();
    }

    public async Task<IReadOnlyList<InstanceProcessus>> ObtenirAuReposAsync(IdDefinition idDefinition, string? codeNoeud, int taille, CancellationToken ct)
        => (await Lister(i => i.IdDefinition == idDefinition && i.EstAuRepos && (codeNoeud is null || i.CodeNoeudCourant == codeNoeud))).Take(taille).ToList();

    public Task RenommerNoeudParentAsync(IdInstance idParent, string ancienCode, string nouveauCode, CancellationToken ct)
    {
        lock (_verrou)
        {
            foreach (var enfant in _lignes.Values.Where(i => i.IdInstanceParent == idParent && i.CodeNoeudParent == ancienCode && i.EstActive))
            {
                enfant.CodeNoeudParent = nouveauCode;
            }
        }

        return Task.CompletedTask;
    }

    private Task<IReadOnlyList<InstanceProcessus>> Lister(Func<InstanceProcessus, bool> filtre)
    {
        lock (_verrou)
        {
            return Task.FromResult<IReadOnlyList<InstanceProcessus>>(_lignes.Values.Where(filtre).OrderBy(i => i.Id.Valeur).Select(Copier).ToList());
        }
    }

    private static InstanceProcessus Copier(InstanceProcessus i) => new()
    {
        Id = i.Id,
        IdDefinition = i.IdDefinition,
        CleProcessus = i.CleProcessus,
        IdInstanceParent = i.IdInstanceParent,
        CodeNoeudParent = i.CodeNoeudParent,
        CleAffaire = i.CleAffaire,
        InstanceUnique = i.InstanceUnique,
        Statut = i.Statut,
        CodeFin = i.CodeFin,
        CodeNoeudCourant = i.CodeNoeudCourant,
        IdTacheCourante = i.IdTacheCourante,
        VariablesJson = i.VariablesJson,
        MessageErreur = i.MessageErreur,
        DateErreur = i.DateErreur,
        Version = i.Version,
        DateCreation = i.DateCreation,
        DateMaj = i.DateMaj,
        DateFin = i.DateFin,
    };
}

public sealed class DepotHistoriqueMemoire : IDepotHistorique
{
    private readonly List<PassageNoeud> _lignes = [];
    private readonly object _verrou = new();

    internal Action Sauvegarder()
    {
        lock (_verrou)
        {
            var copie = _lignes.Select(Copier).ToList();
            return () =>
            {
                lock (_verrou)
                {
                    _lignes.Clear();
                    _lignes.AddRange(copie.Select(Copier));
                }
            };
        }
    }

    public Task InsererAsync(PassageNoeud passage, CancellationToken ct)
    {
        lock (_verrou)
        {
            passage.Id = new IdPassageNoeud(_lignes.Count + 1);
            _lignes.Add(Copier(passage));
        }

        return Task.CompletedTask;
    }

    public Task FermerPassagesOuvertsAsync(IdInstance idInstance, DateTime dateSortie, string? resultat, CancellationToken ct)
    {
        lock (_verrou)
        {
            foreach (var passage in _lignes.Where(p => p.IdInstance == idInstance && p.DateSortie is null))
            {
                passage.DateSortie = dateSortie;
                passage.Resultat = resultat;
            }
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<PassageNoeud>> ObtenirAsync(IdInstance idInstance, CancellationToken ct)
    {
        lock (_verrou)
        {
            return Task.FromResult<IReadOnlyList<PassageNoeud>>(_lignes.Where(p => p.IdInstance == idInstance).Select(Copier).ToList());
        }
    }

    private static PassageNoeud Copier(PassageNoeud p) => new()
    {
        Id = p.Id,
        IdInstance = p.IdInstance,
        CodeNoeud = p.CodeNoeud,
        TypeNoeud = p.TypeNoeud,
        DateEntree = p.DateEntree,
        DateSortie = p.DateSortie,
        Resultat = p.Resultat,
    };
}

public sealed class DepotAttentesMemoire(DepotInstancesMemoire instances) : IDepotAttentes
{
    private readonly List<Attente> _lignes = [];
    private readonly object _verrou = new();
    private long _sequence;

    public IReadOnlyList<Attente> Toutes
    {
        get
        {
            lock (_verrou)
            {
                return _lignes.Select(Copier).ToList();
            }
        }
    }

    internal Action Sauvegarder()
    {
        lock (_verrou)
        {
            var copie = _lignes.Select(Copier).ToList();
            var sequence = _sequence;
            return () =>
            {
                lock (_verrou)
                {
                    _lignes.Clear();
                    _lignes.AddRange(copie.Select(Copier));
                    _sequence = sequence;
                }
            };
        }
    }

    public Task InsererAsync(Attente attente, CancellationToken ct)
    {
        lock (_verrou)
        {
            attente.Id = new IdAttente(++_sequence);
            _lignes.Add(Copier(attente));
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<Attente>> ObtenirParInstanceAsync(IdInstance idInstance, CancellationToken ct)
        => Lister(a => a.IdInstance == idInstance);

    public Task<Attente?> VerrouillerAsync(IdAttente id, CancellationToken ct)
    {
        lock (_verrou)
        {
            return Task.FromResult(_lignes.Where(a => a.Id == id).Select(Copier).FirstOrDefault());
        }
    }

    public Task<int> SupprimerParNoeudAsync(IdInstance idInstance, string codeNoeud, CancellationToken ct)
    {
        lock (_verrou)
        {
            return Task.FromResult(_lignes.RemoveAll(a => a.IdInstance == idInstance && a.CodeNoeud == codeNoeud));
        }
    }

    public Task<int> SupprimerParInstanceAsync(IdInstance idInstance, CancellationToken ct)
    {
        lock (_verrou)
        {
            return Task.FromResult(_lignes.RemoveAll(a => a.IdInstance == idInstance));
        }
    }

    public async Task<IReadOnlyList<EcheanceDue>> ObtenirEcheancesDuesAsync(DateTime maintenant, int tailleMax, CancellationToken ct)
    {
        var dues = new List<EcheanceDue>();
        foreach (var attente in (await Lister(a => a.TypeDeclencheur == TypeDeclencheur.Echeance && a.DateEcheance <= maintenant))
                     .OrderBy(a => a.DateEcheance).ThenBy(a => a.Id.Valeur))
        {
            var instance = await instances.ObtenirAsync(attente.IdInstance, ct);
            if (instance?.Statut == StatutInstance.Suspendue && dues.Count < tailleMax)
            {
                dues.Add(new EcheanceDue(attente.Id, attente.IdInstance, instance.CleProcessus, instance.CleAffaire, attente.DateEcheance!.Value));
            }
        }

        return dues;
    }

    public async Task<IReadOnlyList<Attente>> RechercherSignalAsync(Signal signal, CancellationToken ct)
    {
        var resultat = new List<Attente>();
        foreach (var attente in await Lister(a => a.TypeDeclencheur == TypeDeclencheur.Signal && a.NomSignal == signal.Nom))
        {
            var instance = await instances.ObtenirAsync(attente.IdInstance, ct);
            if (instance is null || !instance.EstAuRepos || instance.CodeNoeudCourant != attente.CodeNoeud)
            {
                continue;
            }

            if (signal.CleProcessus is not null && (instance.CleProcessus != signal.CleProcessus || instance.CleAffaire != signal.CleAffaire))
            {
                continue;
            }

            if (signal.CleCorrelation is not null && attente.CleCorrelation != signal.CleCorrelation)
            {
                continue;
            }

            resultat.Add(attente);
        }

        return resultat;
    }

    public Task RenommerNoeudAsync(IdInstance idInstance, string ancienCode, string nouveauCode, CancellationToken ct)
    {
        lock (_verrou)
        {
            foreach (var attente in _lignes.Where(a => a.IdInstance == idInstance && a.CodeNoeud == ancienCode))
            {
                attente.CodeNoeud = nouveauCode;
            }
        }

        return Task.CompletedTask;
    }

    private Task<IReadOnlyList<Attente>> Lister(Func<Attente, bool> filtre)
    {
        lock (_verrou)
        {
            return Task.FromResult<IReadOnlyList<Attente>>(_lignes.Where(filtre).OrderBy(a => a.Id.Valeur).Select(Copier).ToList());
        }
    }

    private static Attente Copier(Attente a) => new()
    {
        Id = a.Id,
        IdInstance = a.IdInstance,
        CodeNoeud = a.CodeNoeud,
        TypeDeclencheur = a.TypeDeclencheur,
        NomSortie = a.NomSortie,
        DateEcheance = a.DateEcheance,
        NomSignal = a.NomSignal,
        CleCorrelation = a.CleCorrelation,
        DateCreation = a.DateCreation,
    };
}

public sealed class DepotMigrationsMemoire : IDepotMigrations
{
    private readonly List<TraceMigration> _lignes = [];

    public IReadOnlyList<TraceMigration> Traces => _lignes.ToList();

    internal Action Sauvegarder()
    {
        var copie = _lignes.ToList();
        return () =>
        {
            _lignes.Clear();
            _lignes.AddRange(copie);
        };
    }

    public Task InsererAsync(TraceMigration trace, CancellationToken ct)
    {
        trace.Id = new IdMigration(_lignes.Count + 1);
        _lignes.Add(trace);
        return Task.CompletedTask;
    }
}
