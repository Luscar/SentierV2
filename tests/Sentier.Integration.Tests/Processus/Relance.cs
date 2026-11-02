using Sentier.Commandes;
using Sentier.Definitions;
using Sentier.Integration.Tests.Infrastructure;
using Sentier.Variables;

namespace Sentier.Integration.Tests.Processus;

/// <summary>
/// Attente d'un signal avec échéance : à l'échéance, une relance est faite puis l'attente reprend (boucle).
/// </summary>
[ProcessusSentier(Cle, Nom = "Relance d'un fournisseur")]
public sealed class Relance : DefinitionSentier
{
    public const string Cle = "relance-fournisseur";
    public const string NoeudAttente = "attendre-reponse";
    public const string SignalReponse = "REPONSE_FOURNISSEUR";
    public const string SignalAbandon = "ABANDON";
    public static readonly TimeSpan Delai = TimeSpan.FromDays(2);

    public static readonly Variable<string> Reponse = Variable.Texte("reponse");
    public static readonly Variable<long> NbRelances = Variable.Entier("nbRelances").ParDefaut(0);

    protected override void Definir(ConstructeurProcessus processus) => processus
        .Attente(NoeudAttente, a => a
            .Signal(SignalReponse)
            .Signal(SignalAbandon).Vers("abandonnee")
            .Echeance(Delai).Alors(b => b.Systemique<Relancer>("relancer").Vers(NoeudAttente)))
        .Fin("repondue")
        .Fin("abandonnee");
}

public sealed class Relancer(ServicesMetier metier) : ICommandeSentier
{
    public Task ExecuterAsync(ContexteCommandeSentier contexte, CancellationToken ct)
    {
        if (metier.RelanceEnPanne)
        {
            throw new InvalidOperationException("Service de courriel indisponible.");
        }

        var nombre = contexte.Variables.LireOuDefaut(Relance.NbRelances) + 1;
        contexte.Variables.Ecrire(Relance.NbRelances, nombre);
        metier.Noter($"relance:{contexte.CleAffaire}:{nombre}");
        return Task.CompletedTask;
    }
}

/// <summary>Attente corrélée : le signal cible une instance par une clé métier, sans connaître la clé d'affaire.</summary>
[ProcessusSentier(Cle, Nom = "Paiement d'une facture")]
public sealed class PaiementFacture : DefinitionSentier
{
    public const string Cle = "paiement-facture";
    public const string SignalPaiement = "PAIEMENT_RECU";

    public static readonly Variable<string> NumeroFacture = Variable.Texte("numeroFacture").Obligatoire();
    public static readonly Variable<decimal> MontantPaye = Variable.Decimal("montantPaye");

    protected override void Definir(ConstructeurProcessus processus) => processus
        .InstancesMultiples()
        .AttendreSignal("attendre-paiement", SignalPaiement, s => s.Correlation(NumeroFacture))
        .Fin("payee");
}
