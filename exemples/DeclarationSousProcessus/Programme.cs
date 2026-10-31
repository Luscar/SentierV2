using System.Data;
using System.Globalization;
using Exemples.DeclarationSousProcessus;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Sentier;
using Sentier.DependencyInjection;
using Sentier.Persistance.Dapper;

// Démonstration : une déclaration traverse chaque sous-processus et chaque correction, sur SQLite en mémoire.

CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-CA");

using var connexion = new SqliteConnection("Data Source=:memory:");
connexion.Open();

var simulateur = new Simulateur { RejetsIdentification = 1, MontantAttendu = 1250.00m };
var horloge = new HorlogeSimulee();

var services = new ServiceCollection();
services.AddSingleton<IDbConnection>(connexion);
services.AddSentier(o =>
{
    o.PrefixeTables = "SENTIER";
    o.UtiliserSqlite();
    o.BalayerAssemblyDe<TraitementDeclaration>();     // définitions, commandes, requêtes, gestionnaire de tâches
});
services.AddSingleton<IHorlogeSentier>(horloge);
services.AddSingleton<IDossiersDeclarations>(simulateur);
services.AddSingleton<IServiceIdentification>(simulateur);
services.AddSingleton<IServiceConformite>(simulateur);
services.AddSingleton<IServiceVerification>(simulateur);
services.AddSingleton<IServiceComptable>(simulateur);
using var fournisseur = services.BuildServiceProvider();

string[] processus = ["traitement-declaration", "identification-declaration", "conformite-declaration", "verification-declaration", "comptabilisation-declaration"];

await EnTransaction(s => s.GetRequiredService<InstallateurSchemaSentier>().InstallerAsync());
await EnTransaction(s => s.GetRequiredService<IServiceSentier>().DeployerDefinitionsAsync());

const long numeroDeclaration = 1001;

Etape("Réception : la déclaration est enregistrée, puis le traitement démarre dans la même transaction");
await Executer(async s =>
{
    await s.GetRequiredService<IDossiersDeclarations>().EnregistrerAsync(new Declaration(numeroDeclaration, "2026-02", "ORIGINALE", 1205.00m), default);
    await s.GetRequiredService<IServiceSentier>().DemarrerAsync("traitement-declaration", numeroDeclaration);
});

Etape("Correction du rejet d'identification");
await CompleterTache();

Etape("Correction du rejet de conformité : nouveau montant");
simulateur.CorrigerMontant(numeroDeclaration, 1250.00m);
await CompleterTache();

Etape("Autorisation refusée : retour en correction");
simulateur.DeciderAutorisation(numeroDeclaration, false);
await CompleterTache();

Etape("Correction reprise, puis autorisée : la conformité est revalidée");
await CompleterTache();
simulateur.DeciderAutorisation(numeroDeclaration, true);
await CompleterTache();

Etape("Retour du système de vérification");
await Executer(s => s.GetRequiredService<IServiceSentier>().EnvoyerSignalAsync(
    VerificationDeclaration.SignalRetourVerification, "verification-declaration", numeroDeclaration));

Etape("Délai de comptabilisation écoulé");
horloge.MaintenantUtc += ComptabilisationDeclaration.DelaiComptabilisation + TimeSpan.FromMinutes(1);
Journal.Ecrire($"Horloge : {horloge.MaintenantUtc:yyyy-MM-dd HH:mm}");
foreach (var echeance in await Transaction(s => s.GetRequiredService<IServiceSentier>().ObtenirEcheancesDuesAsync()))
{
    await Executer(s => s.GetRequiredService<IServiceSentier>().TraiterEcheanceAsync(echeance.IdAttente));
}

Etape("Retour comptable avec rejet");
await Executer(s =>
{
    simulateur.RecevoirRetourComptable(numeroDeclaration, "CP-03");
    return s.GetRequiredService<IServiceSentier>().EnvoyerSignalAsync(
        ComptabilisationDeclaration.SignalRetourComptable, "comptabilisation-declaration", numeroDeclaration);
});

Etape("Correction du rejet comptable : fin de la comptabilisation, puis du traitement");
await CompleterTache();

Etape("Historique");
await EnTransaction(async s =>
{
    var suivi = s.GetRequiredService<ISuiviSentier>();
    foreach (var instance in (await suivi.ObtenirInstancesParCleAffaireAsync(numeroDeclaration, processus)).OrderBy(i => i.Id.Valeur))
    {
        Console.WriteLine($"  {instance.CleProcessus} : {instance.Statut}, fin « {instance.CodeFin} »");
        foreach (var passage in await suivi.ObtenirHistoriqueAsync(instance.Id))
        {
            Console.WriteLine($"    {passage.DateEntree:yyyy-MM-dd}  {passage.CodeNoeud,-32} {passage.Resultat}");
        }
    }
});

Etape("Diagrammes (Mermaid)");
await EnTransaction(async s =>
{
    foreach (var cle in processus)
    {
        Console.WriteLine(await s.GetRequiredService<ISuiviSentier>().ExporterDiagrammeAsync(cle));
    }
});

// Une portée et une transaction par appel, comme une requête web ou un traitement en lot.
async Task<T> Transaction<T>(Func<IServiceProvider, Task<T>> action)
{
    using var portee = fournisseur.CreateScope();
    using var transaction = connexion.BeginTransaction();
    portee.ServiceProvider.GetRequiredService<IAccesseurTransactionSentier>().Transaction = transaction;
    var retour = await action(portee.ServiceProvider);
    transaction.Commit();
    return retour;
}

Task EnTransaction(Func<IServiceProvider, Task> action) => Transaction(async s => { await action(s); return 0; });

// Exécute l'action, puis affiche où en sont le parent et le sous-processus actif.
async Task Executer(Func<IServiceProvider, Task> action)
{
    await EnTransaction(action);
    foreach (var instance in await Instances())
    {
        var parent = instance.IdInstanceParent is null ? "" : "  ";
        Console.WriteLine($"  {parent}→ {instance.CleProcessus} : {instance.Statut} sur « {instance.CodeNoeudCourant} »{(instance.CodeFin is null ? "" : $", fin « {instance.CodeFin} »")}");
    }
}

// L'agent ouvre la tâche du sous-processus actif (seul le sous-processus porte une tâche) et la complète.
async Task CompleterTache()
{
    var idTache = (await Instances()).Single(i => i.IdTacheCourante is not null).IdTacheCourante!.Value;
    await Executer(s => s.GetRequiredService<IServiceSentier>().CompleterTacheAsync(idTache, utilisateur: "agent"));
}

// Le parent, puis le sous-processus actif ; le parent seul une fois terminé.
async Task<IReadOnlyList<EtatInstance>> Instances()
{
    var instances = await Transaction(s => s.GetRequiredService<ISuiviSentier>().ObtenirInstancesParCleAffaireAsync(numeroDeclaration, processus));
    return instances
        .Where(i => i.CleProcessus == "traitement-declaration" || i.Statut != StatutInstance.Terminee)
        .OrderBy(i => i.IdInstanceParent is null ? 0 : 1)
        .ToList();
}

static void Etape(string titre) => Console.WriteLine($"\n■ {titre}");
