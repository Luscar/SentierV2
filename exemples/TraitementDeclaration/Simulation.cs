using Sentier;

namespace Exemples.TraitementDeclaration;

/// <summary>
/// Services simulés pour la démonstration : chaque service rejette selon un scénario fixé d'avance.
/// Dans l'application, ces interfaces sont implémentées par les vrais services métier.
/// </summary>
public sealed class Simulateur : IServiceDeclarations, IServiceIdentification, IServiceConformite, ICalendrierComptable, IServiceComptable
{
    private int _identifications;
    private int _lots;

    /// <summary>Nombre d'identifications rejetées avant la première acceptée.</summary>
    public int RejetsIdentification { get; set; } = 1;

    /// <summary>Montant attendu par la conformité ; tout autre montant est rejeté.</summary>
    public decimal MontantAttendu { get; set; }

    /// <summary>Jour de comptabilisation du calendrier, chaque mois.</summary>
    public int JourComptabilisation { get; set; } = 15;

    public Task EnregistrerReceptionAsync(long numeroDeclaration, string numeroPeriode, string typeDeclaration, decimal montantDeclare, CancellationToken ct)
    {
        Journal.Ecrire($"Réception de la déclaration {numeroDeclaration} ({typeDeclaration}, période {numeroPeriode}, {montantDeclare:N2} $)");
        return Task.CompletedTask;
    }

    public Task<ResultatIdentification> IdentifierAsync(long numeroDeclaration, string numeroPeriode, CancellationToken ct)
    {
        var resultat = _identifications++ < RejetsIdentification
            ? new ResultatIdentification(null, "ID-01")
            : new ResultatIdentification(700_000 + numeroDeclaration, null);
        Journal.Ecrire(resultat.CodeRejet is null ? $"Identification : déclarant {resultat.NoIdentification}" : $"Identification rejetée : {resultat.CodeRejet}");
        return Task.FromResult(resultat);
    }

    public Task<string?> ValiderAsync(long numeroDeclaration, long noIdentification, string typeDeclaration, decimal montantDeclare, decimal toleranceEcart, CancellationToken ct)
    {
        var codeRejet = Math.Abs(montantDeclare - MontantAttendu) > toleranceEcart ? "CF-12" : null;
        Journal.Ecrire(codeRejet is null ? "Conformité : déclaration conforme" : $"Conformité rejetée : {codeRejet} (écart sur {montantDeclare:N2} $)");
        return Task.FromResult(codeRejet);
    }

    public Task<DateTime> ProchaineDateComptabilisationAsync(string numeroPeriode, DateTime apres, CancellationToken ct)
    {
        var date = new DateTime(apres.Year, apres.Month, JourComptabilisation, 6, 0, 0, DateTimeKind.Utc);
        if (date <= apres)
        {
            date = date.AddMonths(1);
        }

        Journal.Ecrire($"Calendrier comptable : prochaine comptabilisation le {date:yyyy-MM-dd}");
        return Task.FromResult(date);
    }

    public Task<string> TransmettreAsync(long numeroDeclaration, long noIdentification, string numeroPeriode, decimal montant, CancellationToken ct)
    {
        var noLot = $"LOT-{numeroDeclaration}-{++_lots}";
        Journal.Ecrire($"Comptabilisation : écritures transmises, lot {noLot}");
        return Task.FromResult(noLot);
    }
}

/// <summary>Horloge déplacée à la main, pour simuler le passage du temps jusqu'aux dates du calendrier.</summary>
public sealed class HorlogeSimulee : IHorloge
{
    public DateTime MaintenantUtc { get; set; } = new(2026, 3, 2, 9, 0, 0, DateTimeKind.Utc);
}

/// <summary>
/// Gestionnaire de tâches en mémoire. Dans l'application, il s'appuie sur le bassin de tâches existant
/// et utilise la même transaction que Sentier.
/// </summary>
public sealed class GestionnaireTachesMemoire : IGestionnaireTaches
{
    private static readonly List<(TacheUtilisateur Tache, DemandeTacheUtilisateur Demande)> Taches = [];

    public Task<long> CreerTacheAsync(DemandeTacheUtilisateur demande, CancellationToken ct = default)
    {
        var tache = new TacheUtilisateur { Id = Taches.Count + 1, IdInstance = demande.IdInstance, CodeNoeud = demande.CodeNoeud, Statut = StatutTache.Ouverte };
        Taches.Add((tache, demande));
        var donnees = string.Join(", ", demande.Donnees.Select(d => $"{d.Key}={d.Value}"));
        Journal.Ecrire($"Tâche {tache.Id} « {demande.Nom} » créée pour le rôle {demande.Role} ({donnees})");
        return Task.FromResult(tache.Id);
    }

    public Task<TacheUtilisateur?> ObtenirTacheAsync(long idTache, CancellationToken ct = default)
        => Task.FromResult(Taches.Select(t => t.Tache).FirstOrDefault(t => t.Id == idTache));

    public Task<IReadOnlyList<TacheUtilisateur>> ObtenirTachesOuvertesAsync(IdInstance idInstance, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<TacheUtilisateur>>(Taches.Select(t => t.Tache).Where(t => t.IdInstance == idInstance && t.Statut == StatutTache.Ouverte).ToList());

    public Task CompleterTacheAsync(long idTache, IReadOnlyDictionary<string, object?>? donnees, string? utilisateur, CancellationToken ct = default)
        => Changer(idTache, StatutTache.Completee, null);

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

public static class Journal
{
    public static void Ecrire(string message) => Console.WriteLine("    " + message);
}
