using Sentier.Commandes;
using Sentier.Variables;

namespace Sentier.Definitions;

/// <summary>Déclencheurs d'un nœud d'attente : au plus une échéance, des signaux ; chacun a sa branche.</summary>
/// <example>
/// <code>
/// .Attente("attendre-rh", a =&gt; a
///     .Signal("VALIDATION_RH")
///     .Echeance(TimeSpan.FromDays(2)).Alors(b =&gt; b.Systemique&lt;Relancer&gt;("relancer").Vers("attendre-rh")))
/// </code>
/// </example>
public sealed class ConstructeurAttente : ConstructeurBranches<ConstructeurAttente>
{
    private readonly List<Func<string, Declencheur>> _declencheurs = [];
    private readonly List<AppelCommande> _commandesEntree = [];

    internal ConstructeurAttente()
    {
    }

    /// <summary>Échéance relative à l'entrée dans le nœud. Sa sortie se nomme « echeance ».</summary>
    public ConstructeurAttente Echeance(TimeSpan duree)
        => AjouterEcheance(nom => new DeclencheurEcheance { NomSortie = nom, Duree = duree });

    /// <summary>Échéance à une date absolue (UTC).</summary>
    public ConstructeurAttente Echeance(DateTime date)
    {
        var utc = date.Kind == DateTimeKind.Local ? date.ToUniversalTime() : DateTime.SpecifyKind(date, DateTimeKind.Utc);
        return AjouterEcheance(nom => new DeclencheurEcheance { NomSortie = nom, DateAbsolue = utc });
    }

    /// <summary>Échéance lue dans une variable date (calculée, par exemple, par une commande d'entrée).</summary>
    public ConstructeurAttente Echeance(Variable<DateTime> variable)
        => AjouterEcheance(nom => new DeclencheurEcheance { NomSortie = nom, Variable = variable.Nom });

    /// <summary>Signal nommé ; sa sortie porte le nom du signal et sa charge utile est fusionnée dans les variables.</summary>
    /// <param name="nomSignal">Nom du signal.</param>
    /// <param name="options">Corrélation, fusion de la charge utile.</param>
    public ConstructeurAttente Signal(string nomSignal, Action<OptionsSignal>? options = null)
    {
        var configuration = new OptionsSignal();
        options?.Invoke(configuration);
        _declencheurs.Add(nom => new DeclencheurSignal
        {
            NomSortie = nom,
            NomSignal = nomSignal,
            CleCorrelation = configuration.CleCorrelation,
            VariableCorrelation = configuration.VariableCorrelation,
            FusionnerChargeUtile = configuration.Fusionner,
        });
        return AjouterBranche(new BrancheDeclaree(nomSignal));
    }

    /// <summary>Nom de la sortie du dernier déclencheur, si le nom par défaut ne convient pas.</summary>
    public ConstructeurAttente Nommee(string nomSortie) => Renommer(nomSortie);

    /// <summary>Commande exécutée à l'entrée, avant l'enregistrement des déclencheurs.</summary>
    public ConstructeurAttente CommandeEntree<TCommande>(Action<ConfigurationParametres>? parametres = null) where TCommande : ICommandeSentier
        => CommandeEntree(NomsSentier.NomCommande(typeof(TCommande)), parametres);

    /// <summary>Commande désignée par son nom, exécutée à l'entrée.</summary>
    public ConstructeurAttente CommandeEntree(string nomCommande, Action<ConfigurationParametres>? parametres = null)
    {
        _commandesEntree.Add(ConfigurationParametres.Appel(nomCommande, parametres));
        return this;
    }

    internal NoeudAttente Creer(string code) => new()
    {
        Code = code,
        Declencheurs = _declencheurs.Select((creer, i) => creer(Branches[i].NomSortie)).ToList(),
        CommandesEntree = _commandesEntree.ToList(),
    };

    private ConstructeurAttente AjouterEcheance(Func<string, Declencheur> declencheur)
    {
        _declencheurs.Add(declencheur);
        return AjouterBranche(new BrancheDeclaree(DeclencheurEcheance.SortieParDefaut));
    }
}
