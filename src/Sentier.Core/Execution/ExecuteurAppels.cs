using Microsoft.Extensions.DependencyInjection;
using Sentier.Commandes;
using Sentier.Definitions;
using Sentier.Variables;

namespace Sentier.Execution;

/// <summary>Exécution des commandes et évaluation des requêtes, avec résolution des paramètres et intercepteurs.</summary>
internal sealed class ExecuteurAppels(
    IServiceProvider services,
    RegistreSentier registre,
    IEnumerable<IIntercepteurCommandeSentier> intercepteursCommande,
    IEnumerable<IIntercepteurRequeteSentier> intercepteursRequete)
{
    private readonly IIntercepteurCommandeSentier[] _intercepteursCommande = intercepteursCommande.ToArray();
    private readonly IIntercepteurRequeteSentier[] _intercepteursRequete = intercepteursRequete.ToArray();

    public async Task ExecuterCommandesAsync(
        IEnumerable<AppelCommande> appels,
        ContexteNoeud contexte,
        MomentCommande moment,
        IReadOnlyDictionary<string, object?>? donneesTache,
        CancellationToken ct)
    {
        foreach (var appel in appels)
        {
            await ExecuterCommandeAsync(appel, contexte.Instance, contexte.Noeud.Code, contexte.Variables, moment, donneesTache, contexte.Utilisateur, ct);
        }
    }

    public async Task ExecuterCommandeAsync(
        AppelCommande appel,
        InstanceProcessus instance,
        string codeNoeud,
        VariablesProcessus variables,
        MomentCommande moment,
        IReadOnlyDictionary<string, object?>? donneesTache,
        string? utilisateur,
        CancellationToken ct)
    {
        var type = registre.TypeCommande(appel.Nom) ?? throw new CommandeIntrouvableException(appel.Nom);
        var commande = (ICommandeSentier)services.GetRequiredService(type);
        var libelle = $"Commande '{appel.Nom}' du nœud '{codeNoeud}'";
        var parametres = new ParametresResolus(libelle, ResoudreParametres(appel.Parametres, instance, variables, libelle));
        var contexte = new ContexteCommandeSentier(instance, codeNoeud, moment, parametres, variables, donneesTache, utilisateur);

        Func<Task> executer = () => commande.ExecuterAsync(contexte, ct);
        for (var i = _intercepteursCommande.Length - 1; i >= 0; i--)
        {
            var intercepteur = _intercepteursCommande[i];
            var suivant = executer;
            executer = () => intercepteur.ExecuterAsync(appel.Nom, contexte, suivant, ct);
        }

        await executer();
    }

    public async Task<bool> EvaluerAsync(AppelRequete appel, InstanceProcessus instance, string codeNoeud, IVariablesLecture variables, CancellationToken ct)
    {
        var type = registre.TypeRequete(appel.Nom) ?? throw new RequeteIntrouvableException(appel.Nom);
        var requete = (IRequeteSentier)services.GetRequiredService(type);
        var libelle = $"Requête '{appel.Nom}' du nœud '{codeNoeud}'";
        var lecture = new VariablesLectureSeule(variables);
        var parametres = new ParametresResolus(libelle, ResoudreParametres(appel.Parametres, instance, lecture, libelle));
        var contexte = new ContexteRequeteSentier(instance, codeNoeud, parametres, lecture);

        Func<Task<bool>> evaluer = () => requete.EvaluerAsync(contexte, ct);
        for (var i = _intercepteursRequete.Length - 1; i >= 0; i--)
        {
            var intercepteur = _intercepteursRequete[i];
            var suivant = evaluer;
            evaluer = () => intercepteur.EvaluerAsync(appel.Nom, contexte, suivant, ct);
        }

        var resultat = await evaluer();
        return appel.Inverser ? !resultat : resultat;
    }

    /// <summary>Résout les paramètres : valeur statique, variable lue au moment de l'appel, ou clé d'affaire.</summary>
    public static Dictionary<string, object?> ResoudreParametres(IReadOnlyList<Parametre> parametres, InstanceProcessus instance, IVariablesLecture variables, string libelle)
    {
        var valeurs = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var parametre in parametres)
        {
            valeurs[parametre.Nom] = parametre.Source switch
            {
                SourceParametre.Statique => parametre.Valeur?.VersObjet(parametre.Nom),
                SourceParametre.CleAffaire => instance.CleAffaire,
                _ => LireVariable(parametre, variables, libelle),
            };
        }

        return valeurs;
    }

    private static object? LireVariable(Parametre parametre, IVariablesLecture variables, string libelle)
    {
        var valeur = parametre.Variable is null ? null : variables.Lire(parametre.Variable);
        if (valeur is not null)
        {
            return valeur;
        }

        if (!parametre.Optionnel)
        {
            throw new ParametreInvalideException($"{libelle} : la variable '{parametre.Variable}' du paramètre '{parametre.Nom}' n'a pas de valeur.");
        }

        return parametre.Defaut?.VersObjet(parametre.Nom);
    }
}
