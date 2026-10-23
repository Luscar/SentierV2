using Sentier.Commandes;
using Sentier.Definitions;
using Sentier.Migration;
using Sentier.Variables;

namespace Sentier.Tests.Integration.Processus;

/// <summary>Version du processus « abonnement » à déployer : simule les livraisons successives de l'application.</summary>
public sealed class VersionsProcessus
{
    public int Abonnement { get; set; } = 1;
}

/// <summary>
/// Processus dont la définition évolue d'une livraison à l'autre (même classe, injectée de sa version) :
/// v1 → v2 est compatible (migration automatique), v2 → v3 renomme un nœud et exige le plan <see cref="PlanAbonnementV3"/>.
/// </summary>
[ProcessusSentier("abonnement", Nom = "Abonnement")]
public sealed class Abonnement(VersionsProcessus versions) : DefinitionSentier
{
    public const string SignalPaiement = "PAIEMENT";

    public static readonly Variable<string> Formule = Variable.Texte("formule").Obligatoire();
    public static readonly Variable<decimal> Rabais = Variable.Decimal("rabais").ParDefaut(0m);

    protected override void Definir(ConstructeurProcessus processus)
    {
        processus.MigrationAutomatique();
        switch (versions.Abonnement)
        {
            case 1:
                processus
                    .AttendreSignal("attendre-paiement", SignalPaiement)
                    .Fin("actif");
                break;

            case 2:
                processus
                    .Attente("attendre-paiement", a => a
                        .Signal(SignalPaiement)
                        .Echeance(TimeSpan.FromDays(30)).Alors(b => b.Fin("expire")))
                    .Systemique<ConfirmerAbonnement>("confirmer")
                    .Fin("actif");
                break;

            default:
                processus
                    .Attente("attendre-reglement", a => a
                        .Signal(SignalPaiement)
                        .Echeance(TimeSpan.FromDays(30)).Alors(b => b.Fin("expire")))
                    .Systemique<ConfirmerAbonnement>("confirmer")
                    .Fin("actif");
                break;
        }
    }
}

/// <summary>Plan de la v3 : le nœud d'attente est renommé, ses attentes sont conservées.</summary>
public sealed class PlanAbonnementV3 : IPlanMigration
{
    public string CleProcessus => "abonnement";

    public int VersionSourceMin => 1;

    public int VersionSourceMax => 2;

    public void Definir(ConstructeurPlanMigration plan) => plan
        .Noeud("attendre-paiement", "attendre-reglement", r => r.ConserverAttentes())
        .ValeurVariable(Abonnement.Rabais, 5m);
}

public sealed class ConfirmerAbonnement(ISessionSentier session) : ICommandeSentier
{
    public Task ExecuterAsync(ContexteCommande contexte, CancellationToken ct)
        => JournalClient.EcrireAsync(session, contexte.CleAffaire, "abonnement-confirme", contexte.Variables.Lire(Abonnement.Formule), contexte.Utilisateur, ct);
}
