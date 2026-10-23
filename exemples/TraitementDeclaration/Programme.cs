using System.Data;
using System.Globalization;
using Exemples.TraitementDeclaration;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Sentier;
using Sentier.DependencyInjection;
using Sentier.Persistance.Dapper;
using Sentier.Variables;
using static Exemples.TraitementDeclaration.TraitementDeclaration;

// Démonstration : une déclaration traverse chaque boucle de correction une fois, sur SQLite en mémoire.

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
    o.BalayerAssemblyDe<TraitementDeclaration>();     // définition, commandes, requêtes, gestionnaire de tâches
});
services.AddSingleton<IHorloge>(horloge);
services.AddSingleton<IServiceDeclarations>(simulateur);
services.AddSingleton<IServiceIdentification>(simulateur);
services.AddSingleton<IServiceConformite>(simulateur);
services.AddSingleton<ICalendrierComptable>(simulateur);
services.AddSingleton<IServiceComptable>(simulateur);
using var fournisseur = services.BuildServiceProvider();

await EnTransaction(s => s.GetRequiredService<InstallateurSchemaSentier>().InstallerAsync());
await Transaction(s => s.GetRequiredService<IServiceSentier>().DeployerDefinitionsAsync());

const long numeroDeclaration = 1001;

Etape("Démarrage avec les paramètres de la déclaration");
var resultat = await Executer(s => s.DemarrerAsync("traitement-declaration", numeroDeclaration, ValeursVariables
    .Avec(NumeroPeriode, "2026-02")
    .Et(TypeDeclaration, "ORIGINALE")
    .Et(MontantDeclare, 1205.00m)));
var idInstance = resultat.IdInstance!.Value;

Etape("Correction du rejet d'identification");
resultat = await Completer(resultat, null);

Etape("Correction du rejet de conformité : nouveau montant");
resultat = await Completer(resultat, new() { [MontantDeclare.Nom] = 1250.00m });

Etape("Autorisation refusée : retour en correction");
resultat = await Completer(resultat, new() { [CorrectionAutorisee.Nom] = false });

Etape("Correction reprise, puis autorisée : la conformité est revalidée");
resultat = await Completer(resultat, null);
resultat = await Completer(resultat, new() { [CorrectionAutorisee.Nom] = true });

Etape("Date du calendrier comptable atteinte");
var noLot = await AtteindreDateComptabilisation();

Etape("Retour comptable avec rejet");
resultat = await Signaler(noLot, "CP-03");

Etape("Correction du rejet comptable : retour en attente de comptabilisation");
resultat = await Completer(resultat, null);

Etape("Nouvelle date du calendrier comptable atteinte");
noLot = await AtteindreDateComptabilisation();

Etape("Retour comptable sans rejet");
resultat = await Signaler(noLot, null);

Etape("Historique");
await EnTransaction(async s =>
{
    foreach (var passage in await s.GetRequiredService<ISuiviSentier>().ObtenirHistoriqueAsync(idInstance))
    {
        Console.WriteLine($"    {passage.DateEntree:yyyy-MM-dd}  {passage.CodeNoeud,-34} {passage.Resultat}");
    }
});

Etape("Diagramme (Mermaid)");
await EnTransaction(async s => Console.WriteLine(await s.GetRequiredService<ISuiviSentier>().ExporterDiagrammeAsync("traitement-declaration")));

// Une portée et une transaction par appel, comme une requête web ou un traitement en lot.
async Task<T> Transaction<T>(Func<IServiceProvider, Task<T>> action)
{
    using var portee = fournisseur.CreateScope();
    using var transaction = connexion.BeginTransaction();
    portee.ServiceProvider.GetRequiredService<IAccesseurTransaction>().Transaction = transaction;
    var retour = await action(portee.ServiceProvider);
    transaction.Commit();
    return retour;
}

Task EnTransaction(Func<IServiceProvider, Task> action) => Transaction(async s => { await action(s); return 0; });

async Task<ResultatExecution> Executer(Func<IServiceSentier, Task<ResultatExecution>> action)
{
    var r = await Transaction(s => action(s.GetRequiredService<IServiceSentier>()));
    Console.WriteLine($"  → {r.Statut} sur « {r.CodeNoeudCourant} »{(r.CodeFin is null ? "" : $", fin « {r.CodeFin} »")}");
    return r;
}

Task<ResultatExecution> Completer(ResultatExecution courant, Dictionary<string, object?>? donnees)
    => Executer(s => s.CompleterTacheAsync(courant.IdTacheCourante!.Value, donnees, "agent"));

async Task<ResultatExecution> Signaler(string noLot, string? codeRejet)
{
    var r = await Transaction(s => s.GetRequiredService<IServiceSentier>().EnvoyerSignalCorrelationAsync(
        SignalRetourComptable, noLot, new Dictionary<string, object?> { [CodeRejetComptable.Nom] = codeRejet }));
    var reprise = r.Resultats.Single();
    Console.WriteLine($"  → {reprise.Statut} sur « {reprise.CodeNoeudCourant} »{(reprise.CodeFin is null ? "" : $", fin « {reprise.CodeFin} »")}");
    return reprise;
}

// Ce que fait le traitement planifié du client : avancer le temps, puis traiter les échéances dues.
async Task<string> AtteindreDateComptabilisation()
{
    var etat = await Transaction(s => s.GetRequiredService<ISuiviSentier>().ObtenirInstanceAsync(idInstance));
    horloge.MaintenantUtc = (DateTime)etat!.Variables[DateComptabilisation.Nom]! + TimeSpan.FromMinutes(1);
    Console.WriteLine($"    Horloge : {horloge.MaintenantUtc:yyyy-MM-dd HH:mm}");

    foreach (var echeance in await Transaction(s => s.GetRequiredService<IServiceSentier>().ObtenirEcheancesDuesAsync()))
    {
        await Executer(s => s.TraiterEcheanceAsync(echeance.IdAttente));
    }

    etat = await Transaction(s => s.GetRequiredService<ISuiviSentier>().ObtenirInstanceAsync(idInstance));
    return (string)etat!.Variables[NoLotComptable.Nom]!;
}

static void Etape(string titre) => Console.WriteLine($"\n■ {titre}");
