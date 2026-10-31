using System.Text.Json;
using Sentier.Definitions;
using Sentier.Execution;
using Sentier.Persistance;
using Sentier.Variables;

namespace Sentier.Services;

/// <summary>Implémentation de <see cref="ISuiviSentier"/>.</summary>
internal sealed class SuiviSentier(
    MoteurSentier moteur,
    FournisseurDefinitions definitions,
    IDepotInstances instances,
    IDepotHistorique historique,
    IDepotAttentes attentes,
    ISerialiseurVariablesSentier serialiseur) : ISuiviSentier
{
    /// <summary>Taille d'un lot de candidats lu en base pendant une recherche par variables.</summary>
    private const int TailleLotRecherche = 200;

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

    public Task<IReadOnlyList<EtatInstance>> RechercherParVariableAsync<T>(string cleProcessus, Variable<T> variable, T valeur, IReadOnlyCollection<StatutInstance>? statuts = null, int tailleMax = 100, CancellationToken ct = default)
        => RechercherParVariablesAsync(
            new RechercheParVariables
            {
                CleProcessus = cleProcessus,
                Filtres = [FiltreVariable.Egal(variable, valeur)],
                Statuts = statuts,
                TailleMax = tailleMax,
            },
            ct);

    public async Task<IReadOnlyList<EtatInstance>> RechercherParVariablesAsync(RechercheParVariables recherche, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(recherche);
        ArgumentException.ThrowIfNullOrWhiteSpace(recherche.CleProcessus);
        ArgumentOutOfRangeException.ThrowIfLessThan(recherche.TailleMax, 1);
        if (recherche.Filtres is not { Count: > 0 })
        {
            throw new ArgumentException("Une recherche par variables exige au moins un filtre.", nameof(recherche));
        }

        foreach (var filtre in recherche.Filtres)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(filtre.Nom);
            if (!ConditionVariable.EstUnaire(filtre.Operateur) && filtre.Valeur is null
                && filtre.Operateur is not (OperateurComparaison.Egal or OperateurComparaison.Different))
            {
                throw new ArgumentException($"Le filtre {filtre.Operateur} sur '{filtre.Nom}' exige une valeur.", nameof(recherche));
            }
        }

        var motifs = serialiseur is SerialiseurVariablesJson ? recherche.Filtres.Select(MotifPrefiltre).OfType<string>().ToList() : [];
        var filtresParVersion = new Dictionary<IdDefinition, IReadOnlyList<(FiltreVariable Filtre, object? Operande)>>();
        var resultats = new List<EtatInstance>();
        IdInstance? avant = null;
        while (resultats.Count < recherche.TailleMax)
        {
            var lot = await instances.ObtenirLotParProcessusAsync(recherche.CleProcessus, recherche.Statuts, motifs, avant, TailleLotRecherche, ct);
            foreach (var instance in lot)
            {
                var definition = await definitions.ObtenirAsync(instance.IdDefinition, ct);
                if (!filtresParVersion.TryGetValue(instance.IdDefinition, out var filtres))
                {
                    filtres = recherche.Filtres.Select(f => (f, Operande(f, definition))).ToList();
                    filtresParVersion[instance.IdDefinition] = filtres;
                }

                var variables = moteur.ChargerVariables(instance, definition);
                if (filtres.All(f => EvaluateurComparaison.Evaluer(f.Filtre.Operateur, variables.Lire(f.Filtre.Nom), f.Operande)))
                {
                    resultats.Add(VersEtat(instance, definition, variables));
                    if (resultats.Count == recherche.TailleMax)
                    {
                        break;
                    }
                }
            }

            if (lot.Count < TailleLotRecherche)
            {
                break;
            }

            avant = lot[^1].Id;
        }

        return resultats;
    }

    /// <summary>Valeur de comparaison d'un filtre, convertie dans le type déclaré par une version de la définition.</summary>
    private static object? Operande(FiltreVariable filtre, DefinitionProcessus definition)
    {
        if (ConditionVariable.EstUnaire(filtre.Operateur) || filtre.Valeur is null
            || !definition.Declarations.TryGetValue(filtre.Nom, out var declaration))
        {
            return ConditionVariable.EstUnaire(filtre.Operateur) ? null : filtre.Valeur;
        }

        if (ValidateurDefinition.OperateurIncompatible(filtre.Operateur, declaration.Type) is { } raison)
        {
            throw new ArgumentException($"Le filtre {filtre.Operateur} ne s'applique pas à la variable '{filtre.Nom}' de type {declaration.Type} (version {definition.Version}) : {raison}.");
        }

        var type = filtre.Operateur is OperateurComparaison.Contient or OperateurComparaison.CommencePar
            ? TypeVariable.Texte
            : declaration.Type == TypeVariable.Entier && filtre.Valeur is decimal or double or float ? TypeVariable.Decimal : declaration.Type;
        try
        {
            return ConvertisseurValeurs.Normaliser(type, filtre.Valeur, filtre.Nom);
        }
        catch (VariableInvalideException ex)
        {
            throw new ArgumentException($"Filtre sur '{filtre.Nom}' (version {definition.Version}) : {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Motif <c>LIKE</c> qui préfiltre en base le JSON des variables écrit par <see cref="SerialiseurVariablesJson"/> :
    /// la clé pour un filtre qui exige une valeur, la paire clé/valeur pour une égalité exacte (texte, entier, booléen).
    /// <c>null</c> si le filtre peut être vrai pour une variable absente.
    /// </summary>
    private static string? MotifPrefiltre(FiltreVariable filtre)
    {
        if (filtre.Operateur is OperateurComparaison.EstVide or OperateurComparaison.Different
            || (filtre.Operateur == OperateurComparaison.Egal && filtre.Valeur is null))
        {
            return null;
        }

        // Valeur sans guillemet fermant ni séparateur : le motif reste un préfixe de la valeur stockée
        // (un décimal 10 peut être écrit 10.00), et le filtre exact tranche ensuite.
        var cle = JsonSerializer.Serialize(filtre.Nom) + ":";
        var valeur = filtre.Operateur == OperateurComparaison.Egal && filtre.Valeur is string or long or int or short or bool
            ? JsonSerializer.Serialize(filtre.Valeur, filtre.Valeur.GetType()).TrimEnd('"')
            : "";
        return "%" + EchapperLike(cle + valeur) + "%";
    }

    private static string EchapperLike(string texte) => texte.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");

    public Task<IReadOnlyList<PassageNoeud>> ObtenirHistoriqueAsync(IdInstance idInstance, CancellationToken ct = default)
        => historique.ObtenirAsync(idInstance, ct);

    public Task<IReadOnlyList<Attente>> ObtenirAttentesAsync(IdInstance idInstance, CancellationToken ct = default)
        => attentes.ObtenirParInstanceAsync(idInstance, ct);

    public async Task<IReadOnlyList<TacheSentier>> ObtenirTachesOuvertesAsync(IdInstance idInstance, CancellationToken ct = default)
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
        return VersEtat(instance, definition, moteur.ChargerVariables(instance, definition));
    }

    private static EtatInstance VersEtat(InstanceProcessus instance, DefinitionProcessus definition, VariablesProcessus variables)
        => new EtatInstance
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
            Variables = variables.Valeurs,
            DateCreation = instance.DateCreation,
            DateMaj = instance.DateMaj,
            DateFin = instance.DateFin,
            MessageErreur = instance.MessageErreur,
            DateErreur = instance.DateErreur,
        };
}
