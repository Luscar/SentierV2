using Sentier.Commandes;
using Sentier.Definitions;
using Sentier.Integration.Tests.Infrastructure;
using Sentier.Variables;

namespace Sentier.Integration.Tests.Processus;

/// <summary>Boucle sans fin : la protection MaxEtapesParExecution doit l'arrêter.</summary>
[ProcessusSentier(Cle)]
public sealed class BoucleSansFin : DefinitionSentier
{
    public const string Cle = "boucle-sans-fin";

    public static readonly Variable<bool> Continuer = Variable.Booleen("continuer").ParDefaut(true);

    protected override void Definir(ConstructeurProcessus processus) => processus
        .Systemique<NeRienFaire>("tourner")
        .Decision("encore", d => d
            .Si(Continuer).EstVrai().Vers("tourner")
            .Sinon())
        .Fin("fin");
}

public sealed class NeRienFaire : ICommandeSentier
{
    public Task ExecuterAsync(ContexteCommandeSentier contexte, CancellationToken ct) => Task.CompletedTask;
}

/// <summary>
/// Définition qui change selon la configuration injectée : sert à déployer une seconde version
/// et à vérifier que les instances en cours gardent la leur.
/// </summary>
[ProcessusSentier(Cle)]
public sealed class Evolutif(ConfigurationProcessus configuration) : DefinitionSentier
{
    public const string Cle = "evolutif";

    protected override void Definir(ConstructeurProcessus processus)
    {
        if (configuration.EtapeControleAjoutee)
        {
            processus.Systemique<NeRienFaire>("controler");
        }

        processus
            .Interactif("traiter", "Traiter")
            .Fin("fin");
    }
}
