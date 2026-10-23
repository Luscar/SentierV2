#nullable enable
using System.Data;
using System.Globalization;
using System.Text.Json;
using Sentier;

namespace AT99.AgentService.OX.BureauVirtuel.Services;

/// <summary>
/// Implémentation de <see cref="IServiceProcessusAffaires"/> sur Sentier, à la place de l'agent OX (BPM externe).
/// Enregistrée en Scoped, dans la même portée que Sentier : connexion, transaction et lectures partagées.
/// </summary>
/// <remarks>
/// Correspondances :
/// <list type="bullet">
/// <item><c>TypeProcessus.Code</c> / <c>TypeSousProcessus.Code</c> = clé du processus Sentier.</item>
/// <item>Numéro d'identification système (numéro de déclaration…) = clé d'affaire. Le sous-processus
/// TACHE_EN_ATTENTE garde la clé d'affaire de son parent (comportement par défaut de <c>SousProcessus</c>).</item>
/// <item>NUMERO_PERIODE_DECLARATION = clé de corrélation des signaux de TACHE_EN_ATTENTE
/// (<c>.Signal(nom, options: s => s.Correlation(NumeroPeriodeDeclaration))</c>).</item>
/// <item>Code de tâche OX = code du nœud interactif Sentier.</item>
/// </list>
/// Sentier est synchrone : après un signal, le parent a déjà avancé jusqu'à sa prochaine tâche
/// dans la même transaction ; l'attente active de l'ancienne implémentation n'est plus nécessaire.
/// </remarks>
public class ServiceProcessusAffairesSentier : IServiceProcessusAffaires
{
    private static readonly StatutInstance[] StatutsActifs = [StatutInstance.EnCours, StatutInstance.Suspendue, StatutInstance.EnErreur];

    private static readonly List<TypeProcessus> TypesDeclaration =
    [
        TypeProcessus.DECLARATION_ORIGINALE,
        TypeProcessus.DECLARATION_ORIGINALE_SIMPLE,
        TypeProcessus.RECOTISATION_DECLARATION_ORIGINALE,
        TypeProcessus.DECLARATION_MODIFIEE,
        TypeProcessus.RECOTISATION_DECLARATION_MODIFIEE,
        TypeProcessus.DECLARATION_NON_PRODUCTION,
        TypeProcessus.DECLARATION_RELANCEE,
    ];

    private readonly IFabriqueProcessus fabriqueProcessus;
    private readonly IServiceTravailAsynchrone serviceTravailAsynchrone;
    private readonly IServiceSentier sentier;
    private readonly ISuiviSentier suivi;
    private readonly IAccesseurTransaction accesseurTransaction;
    private readonly IDbConnection connexion;

    public ServiceProcessusAffairesSentier(
        IFabriqueProcessus fabriqueProcessus,
        IServiceTravailAsynchrone serviceTravailAsynchrone,
        IServiceSentier sentier,
        ISuiviSentier suivi,
        IAccesseurTransaction accesseurTransaction,
        IDbConnection connexion)
    {
        this.fabriqueProcessus = fabriqueProcessus;
        this.serviceTravailAsynchrone = serviceTravailAsynchrone;
        this.sentier = sentier;
        this.suivi = suivi;
        this.accesseurTransaction = accesseurTransaction;
        this.connexion = connexion;
    }

    public Task DemarrerProcessusEventuellementAsync(TypeProcessus typeProcessus, long identifiant, string logon)
    {
        if (identifiant == default)
            throw new InvalidOperationException("Un identifiant est requis pour démarrer le processus d'affaires");

        // Inchangé : le travail asynchrone appelle ensuite DemarrerProcessusAsync.
        var demarrerInstanceProcessus = new DemarrerInstanceProcessus
        {
            TypeProcessus = typeProcessus,
            NumeroIdentificationSysteme = identifiant,
            LogonCreation = logon == ConstantesLogon.SYSTEME ? null : logon,
        };

        return serviceTravailAsynchrone.CreerTravailAsynchroneAsync(demarrerInstanceProcessus);
    }

    public Task<bool> EstProcessusEnCoursAsync(TypeProcessus typeProcessus, long identifiant)
        => ExisteProcessusEnCoursAsync(identifiant, [typeProcessus]);

    public Task<bool> EstProcessusDeclarationEnCoursAsync(long numeroDeclaration)
        => ExisteProcessusEnCoursAsync(numeroDeclaration, TypesDeclaration);

    public Task DemarrerProcessusAsync(TypeProcessus typeProcessus, long numeroIdentificationSysteme, string logonCreation, TypeFormulaire typeFormulaire)
        => DemarrerProcessusInterneAsync(typeProcessus, numeroIdentificationSysteme, logonCreation, "", typeFormulaire);

    public Task<long> DemarrerProcessusSynchroneAsync(TypeProcessus typeProcessus, long numeroIdentificationSysteme, string logonCreation, TypeFormulaire typeFormulaire)
        => DemarrerProcessusInterneAsync(typeProcessus, numeroIdentificationSysteme, logonCreation, logonCreation, typeFormulaire);

    public Task TerminerTacheAsync<T>(long numeroTache, T contenu)
        => EnTransactionAsync(async () =>
        {
            // Sentier fusionne le contenu dans les variables, exécute les commandes de sortie,
            // puis termine la tâche dans le bassin OX par le gestionnaire de tâches.
            var resultat = await sentier.CompleterTacheAsync(numeroTache, VersDonnees(contenu));
            if (resultat.SansEffet)
                throw new InvalidOperationException(resultat.Raison);

            return resultat;
        });

    public Task<long?> TerminerTacheSiEnAttenteAsync<T>(long numeroDeclaration, string[] nomsSignaux, TypeSousProcessus typeSousProcessus, T contenu)
        => EnTransactionAsync(async () =>
        {
            var clesProcessus = TypesDeclaration.Select(t => t.Code).Append(typeSousProcessus.Code).ToList();
            var processus = await suivi.ObtenirInstancesParCleAffaireAsync(numeroDeclaration, clesProcessus, StatutsActifs);
            if (processus.Count == 0)
                return (long?)null;

            var sousProcessus = processus.SingleOrDefault(x => x.CleProcessus == typeSousProcessus.Code);
            if (sousProcessus is null)
                return processus.SingleOrDefault(x => x.CleProcessus != typeSousProcessus.Code && x.IdInstanceParent is null)?.Id.Valeur;

            var signalAEmettre = (await ObtenirSignauxEnAttenteAsync(sousProcessus.Id)).SingleOrDefault(nomsSignaux.Contains)
                ?? throw new InvalidOperationException($"Il n'y a pas de signal pour le {sousProcessus.CleProcessus} avec l'ID {sousProcessus.Id}");

            var resultat = await sentier.EnvoyerSignalAsync(new Signal
            {
                Nom = signalAEmettre,
                CleProcessus = typeSousProcessus.Code,
                CleAffaire = numeroDeclaration,
                ChargeUtile = VersDonnees(contenu),
            });
            if (resultat.SansEffet)
                throw new InvalidOperationException(resultat.Raison);

            return sousProcessus.IdInstanceParent?.Valeur;
        });

    public Task TerminerTacheEnAttenteAsync(long numeroPeriodeDeclaration, string nomSignal)
        => EnTransactionAsync(async () =>
        {
            var resultat = await sentier.EnvoyerSignalCorrelationAsync(nomSignal, numeroPeriodeDeclaration.ToString(CultureInfo.InvariantCulture));
            if (resultat.SansEffet)
                throw new InvalidOperationException($"Il n'y a pas de tâche d'attente active pour le signal {nomSignal} et la période {numeroPeriodeDeclaration} : {resultat.Raison}");

            return resultat;
        });

    public Task<long> AvancerATacheSuivanteAsync<T>(long numeroDeclaration, string[] nomsSignaux, T contenu, string codeProchaineTache)
        => EnTransactionAsync(async () =>
        {
            var numeroProcessus = await TerminerTacheSiEnAttenteAsync(numeroDeclaration, nomsSignaux, TypeSousProcessus.TACHE_EN_ATTENTE, contenu)
                ?? throw new InvalidOperationException("Il n'y a pas de processus principal actif pour le numéro de déclaration " + numeroDeclaration);

            // Même transaction : le parent a déjà avancé ; il doit être suspendu sur la tâche attendue.
            var parent = await suivi.ObtenirInstanceAsync(IdInstance.Depuis(numeroProcessus));
            if (parent is null || parent.IdTacheCourante is null || parent.CodeNoeudCourant != codeProchaineTache)
                throw new InvalidOperationException($"La tâche « {codeProchaineTache} » n'as pas été trouvée pour le processus avec l'ID " + numeroProcessus);

            return numeroProcessus;
        });

    private async Task<long> DemarrerProcessusInterneAsync(
        TypeProcessus typeProcessus,
        long identifiant,
        string logonCreation,
        string logonAssigne = "",
        TypeFormulaire? typeFormulaire = null)
    {
        ProcessusAT processus = fabriqueProcessus.Creer(typeProcessus, identifiant, logonCreation, logonAssigne, typeFormulaire);

        try
        {
            return await EnTransactionAsync(async () =>
            {
                if (await EstProcessusEnCoursAsync(typeProcessus, identifiant))
                    throw new InvalidOperationException($"Un processus d'affaire du type {typeProcessus.Code} est déjà en cours pour l'identifiant {identifiant}");

                var resultat = await sentier.DemarrerAsync(
                    processus.TypeProcessus.Code,
                    identifiant,
                    processus.VariablesProcessus.ToDictionary(v => v.Key, v => (object?)v.Value),
                    utilisateur: string.IsNullOrEmpty(logonCreation) ? null : logonCreation);

                return resultat.IdInstance!.Value.Valeur;
            });
        }
        catch (Exception ex)
        {
            throw new ProcessusException("DemarrerProcessusAsync", processus, ex.Message);
        }
    }

    private async Task<bool> ExisteProcessusEnCoursAsync(long identifiant, List<TypeProcessus> typesProcessus)
    {
        if (typesProcessus == null || typesProcessus.Count == 0)
            throw new ArgumentNullException(nameof(typesProcessus));

        var processus = await suivi.ObtenirInstancesParCleAffaireAsync(identifiant, typesProcessus.Select(t => t.Code).ToList(), StatutsActifs);
        return processus.Count != 0;
    }

    private async Task<List<string>> ObtenirSignauxEnAttenteAsync(IdInstance idInstance)
        => (await suivi.ObtenirAttentesAsync(idInstance))
            .Where(a => a.TypeDeclencheur == TypeDeclencheur.Signal && a.NomSignal is not null)
            .Select(a => a.NomSignal!)
            .Distinct()
            .ToList();

    /// <summary>
    /// Contenu d'une tâche ou d'un signal vers les données Sentier : une entrée par propriété JSON, fusionnée
    /// dans la variable déclarée du même nom (les autres sont ignorées). Utiliser les mêmes options de
    /// sérialisation que l'ancien appel (<c>ObjetSerialiseJson&lt;T&gt;</c>) pour garder les mêmes noms.
    /// </summary>
    private static IReadOnlyDictionary<string, object?>? VersDonnees<T>(T contenu)
    {
        if (contenu is null)
            return null;

        var json = JsonSerializer.SerializeToElement(contenu);
        if (json.ValueKind != JsonValueKind.Object)
            throw new ArgumentException($"Le contenu doit être un objet JSON (reçu : {json.ValueKind}).", nameof(contenu));

        return json.EnumerateObject().ToDictionary(p => p.Name, p => (object?)p.Value);
    }

    /// <summary>
    /// Exécute dans la transaction courante du client, ou dans une transaction ouverte ici et validée à la fin
    /// (les appelants de l'agent OX n'avaient pas de transaction).
    /// </summary>
    private async Task<TResultat> EnTransactionAsync<TResultat>(Func<Task<TResultat>> action)
    {
        if (accesseurTransaction.Transaction is not null)
            return await action();

        using var transaction = connexion.BeginTransaction();
        accesseurTransaction.Transaction = transaction;
        try
        {
            var resultat = await action();
            transaction.Commit();
            return resultat;
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
        finally
        {
            accesseurTransaction.Transaction = null;
        }
    }
}
