using Sentier.Definitions;

namespace Exemples.DeclarationSousProcessus;

/// <summary>
/// Comptabilisation : délai avant le traitement comptable, comptabilisation, attente du retour comptable ;
/// un rejet part en correction, puis le sous-processus se termine.
/// </summary>
[ProcessusSentier("comptabilisation-declaration", Nom = "Comptabilisation d'une déclaration")]
public sealed class ComptabilisationDeclaration : DefinitionSentier
{
    /// <summary>Signal envoyé pour la clé d'affaire quand le système comptable a répondu.</summary>
    public const string SignalRetourComptable = "RETOUR_COMPTABLE";

    /// <summary>Délai entre l'entrée dans la comptabilisation et le traitement comptable.</summary>
    public static readonly TimeSpan DelaiComptabilisation = TimeSpan.FromDays(1);

    protected override void Definir(ConstructeurProcessus processus) => processus
        .Attente("attendre-comptabilisation", a => a.Echeance(DelaiComptabilisation))
        .Systemique<Comptabiliser>("comptabiliser")
        .Attente("attendre-retour-comptable", a => a.Signal(SignalRetourComptable, s => s.SansFusion()))
        .Decision("est-rejetee", d => d
            .Si<EstRejeteeComptabilite>().Nommee("rejetee").Alors(b => b
                .Interactif("corriger-rejet-comptable", "Corriger le rejet comptable", i => i
                    .Role("agent-comptable")
                    .Echeance(TimeSpan.FromDays(3))))
            .Sinon())
        .Fin("comptabilisee");
}
