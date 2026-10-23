using Sentier.Definitions;
using Sentier.Variables;

namespace Exemples.TraitementDeclaration;

/// <summary>
/// Traitement d'une déclaration, de sa réception à sa comptabilisation.
/// La clé d'affaire est le numéro de la déclaration.
/// </summary>
/// <remarks>
/// <list type="number">
/// <item>Réception, puis identification du déclarant ; un rejet part en correction, puis l'identification est reprise.</item>
/// <item>Validation de conformité ; un rejet part en correction, la correction doit être autorisée, puis la conformité est revalidée.
/// Une correction refusée retourne en correction.</item>
/// <item>Attente de la prochaine date du calendrier comptable, puis comptabilisation.</item>
/// <item>Attente du retour comptable (signal corrélé au numéro de lot) : sans rejet, fin ; sinon correction du rejet comptable,
/// puis retour en attente de comptabilisation.</item>
/// </list>
/// </remarks>
[ProcessusSentier("traitement-declaration", Nom = "Traitement d'une déclaration")]
public sealed class TraitementDeclaration : DefinitionSentier
{
    /// <summary>Nom du signal envoyé par le système comptable pour un lot.</summary>
    public const string SignalRetourComptable = "RETOUR_COMPTABLE";

    // Paramètres de démarrage.
    public static readonly Variable<string> NumeroPeriode = Variable.Texte("numeroPeriode").Obligatoire();
    public static readonly Variable<string> TypeDeclaration = Variable.Texte("typeDeclaration").Obligatoire();
    public static readonly Variable<decimal> MontantDeclare = Variable.Decimal("montantDeclare").Obligatoire();

    // Alimentées en cours de traitement.
    public static readonly Variable<long> NoIdentification = Variable.Entier("noIdentification");
    public static readonly Variable<string> CodeRejetIdentification = Variable.Texte("codeRejetIdentification");
    public static readonly Variable<string> CodeRejetConformite = Variable.Texte("codeRejetConformite");
    public static readonly Variable<bool> CorrectionAutorisee = Variable.Booleen("correctionAutorisee").ParDefaut(false);
    public static readonly Variable<DateTime> DateComptabilisation = Variable.Date("dateComptabilisation");
    public static readonly Variable<string> NoLotComptable = Variable.Texte("noLotComptable");
    public static readonly Variable<string> CodeRejetComptable = Variable.Texte("codeRejetComptable");

    // Codes ciblés par un lien.
    private const string Identification = "identification";
    private const string CorrectionRejetIdentification = "corriger-rejet-identification";
    private const string ValidationConformite = "valider-conformite";
    private const string CorrectionRejetConformite = "corriger-rejet-conformite";
    private const string AttenteComptabilisation = "attendre-comptabilisation";
    private const string CorrectionRejetComptable = "corriger-rejet-comptable";

    protected override void Definir(ConstructeurProcessus processus) => processus
        .Systemique<RecevoirDeclaration>("recevoir-declaration", p => p
            .CleAffaire("numeroDeclaration")
            .Variables(NumeroPeriode, TypeDeclaration, MontantDeclare))

        // Identification
        .Systemique<IdentifierDeclarant>(Identification, p => p
            .CleAffaire("numeroDeclaration")
            .Variable(NumeroPeriode))
        .Decision("identification-acceptee", d => d
            .Si<EstSansRejet>(p => p.VariableOptionnelle("codeRejet", CodeRejetIdentification)).Nommee("acceptee")
            .Sinon(CorrectionRejetIdentification))

        // Conformité
        .Systemique<ValiderConformite>(ValidationConformite, p => p
            .CleAffaire("numeroDeclaration")
            .Variables(NoIdentification, TypeDeclaration, MontantDeclare)
            .Valeur("toleranceEcart", 0.01m))
        .Decision("conformite-acceptee", d => d
            .Si<EstSansRejet>(p => p.VariableOptionnelle("codeRejet", CodeRejetConformite)).Nommee("acceptee").Vers(AttenteComptabilisation)
            .Sinon(CorrectionRejetConformite))
        .Interactif(CorrectionRejetConformite, "Corriger le rejet de conformité", i => i
            .Role("agent-conformite")
            .Echeance(TimeSpan.FromDays(5))
            .Donnees(CodeRejetConformite, TypeDeclaration, MontantDeclare))
        .Interactif("autoriser-correction-conformite", "Autoriser la correction du rejet de conformité", i => i
            .Role("chef-equipe-conformite")
            .Echeance(TimeSpan.FromDays(2))
            .Donnees(CodeRejetConformite, MontantDeclare))
        .Decision("correction-autorisee", d => d
            .Si<EstVrai>(p => p.Variable("valeur", CorrectionAutorisee)).Nommee("autorisee").Vers(ValidationConformite)
            .Sinon(CorrectionRejetConformite))

        // Comptabilisation : la date est tirée du calendrier comptable à chaque entrée dans l'attente.
        .Attente(AttenteComptabilisation, a => a
            .CommandeEntree<CalculerDateComptabilisation>(p => p.Variable(NumeroPeriode))
            .Echeance(DateComptabilisation))
        .Systemique<Comptabiliser>("comptabiliser", p => p
            .CleAffaire("numeroDeclaration")
            .Variables(NoIdentification, NumeroPeriode, MontantDeclare))
        .Attente("attendre-retour-comptable", a => a
            .Signal(SignalRetourComptable, s => s.Correlation(NoLotComptable)))
        .Decision("comptabilisation-acceptee", d => d
            .Si<EstSansRejet>(p => p.VariableOptionnelle("codeRejet", CodeRejetComptable)).Nommee("acceptee")
            .Sinon(CorrectionRejetComptable))
        .Fin("comptabilisee")

        // Corrections reprises en style plat.
        .Interactif(CorrectionRejetIdentification, "Corriger le rejet d'identification", i => i
            .Role("agent-identification")
            .Echeance(TimeSpan.FromDays(5))
            .Donnees(CodeRejetIdentification, NumeroPeriode))
        .Vers(Identification)
        .Interactif(CorrectionRejetComptable, "Corriger le rejet comptable", i => i
            .Role("agent-comptable")
            .Echeance(TimeSpan.FromDays(3))
            .Donnees(CodeRejetComptable, NoLotComptable, MontantDeclare))
        .Vers(AttenteComptabilisation);
}
