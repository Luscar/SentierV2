using System.Data;
using System.Text.RegularExpressions;
using Exemples.DeclarationSousProcessus;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Sentier.DependencyInjection;
using Xunit;

namespace Sentier.Persistance.Tests;

/// <summary>
/// Garde de compatibilité avec un exécuteur client qui traduit le SQL Oracle en SQLite par remplacements textuels
/// (ex. TraducteurSQLite d'AccesBDPlus) : le SQL que Sentier envoie sous SQLite ne doit contenir aucun motif que ce
/// traducteur réécrirait ou supprimerait, sinon la requête serait altérée sans erreur.
/// </summary>
public sealed class CompatibiliteTraducteurSqliteTests
{
    // Mêmes motifs (et même sensibilité à la casse) que les remplacements du traducteur.
    private static readonly (string Nom, Regex Motif)[] MotifsTraduits =
    [
        ("FROM DUAL", new Regex("FROM DUAL", RegexOptions.IgnoreCase)),
        ("ROWNUM", new Regex("ROWNUM", RegexOptions.IgnoreCase)),
        ("SYSDATE", new Regex("SYSDATE")),
        ("CHR(", new Regex(@"CHR\(")),
        ("NVL2", new Regex("NVL2")),
        ("FOR UPDATE", new Regex("FOR UPDATE")),
        ("TO_DATE(", new Regex(@"TO_DATE\(")),
        ("TO_CHAR(", new Regex(@"TO_CHAR\(")),
        ("FETCH FIRST", new Regex("FETCH FIRST")),
        ("OFFSET … ROWS", new Regex("OFFSET .* ROWS")),
        ("EXTRACT(YEAR FROM", new Regex(@"EXTRACT\(YEAR FROM")),
        ("EXTRACT(MONTH FROM", new Regex(@"EXTRACT\(MONTH FROM")),
        ("MERGE INTO", new Regex(@"MERGE\s+INTO")),
        ("OPEN :", new Regex("OPEN :")),
        ("BEGIN", new Regex("BEGIN")),
        ("END;", new Regex("END;")),
    ];

    [Fact]
    public async Task Le_sql_envoye_sous_sqlite_ne_contient_aucun_motif_traduit()
    {
        var journal = await ExecuterScenarioAsync();

        Assert.NotEmpty(journal.Requetes);
        var violations = Violations(journal.Requetes);
        Assert.True(violations.Count == 0, "SQL altéré par un traducteur Oracle → SQLite :\n" + string.Join("\n", violations));
    }

    [Fact]
    public void Les_fragments_du_dialecte_sqlite_ne_contiennent_aucun_motif_traduit()
    {
        var dialecte = new DialecteSqlite();
        string[] fragments =
        [
            dialecte.VerrouillageSansAttente,
            dialecte.Limiter("SELECT 1", 10),
            dialecte.Paginer("SELECT 1", 20, 10),
            dialecte.Parametre("nom"),
        ];

        var violations = Violations(fragments);
        Assert.True(violations.Count == 0, string.Join("\n", violations));
    }

    private static List<string> Violations(IEnumerable<string> requetes)
        => requetes
            .Distinct()
            .SelectMany(sql => MotifsTraduits.Where(m => m.Motif.IsMatch(sql)).Select(m => $"[{m.Nom}] {sql}"))
            .ToList();

    // Parcours complet de l'exemple (installation, déploiement, sous-processus, tâches, signal, échéance, suivi),
    // avec un exécuteur qui enregistre chaque requête avant de la confier à Dapper.
    private static async Task<JournalSql> ExecuterScenarioAsync()
    {
        using var connexion = new SqliteConnection("Data Source=:memory:");
        connexion.Open();

        var simulateur = new Simulateur { RejetsIdentification = 1, MontantAttendu = 1250.00m };
        var horloge = new HorlogeSimulee();
        var journal = new JournalSql();

        var services = new ServiceCollection();
        services.AddSingleton<IDbConnection>(connexion);
        services.AddSingleton(journal);
        services.AddSentier(o =>
        {
            o.PrefixeTables = "SENTIER";
            o.UtiliserSqlite();
            o.UtiliserExecuteurSql<ExecuteurEnregistreur>();
            o.BalayerAssemblyDe<TraitementDeclaration>();
        });
        services.AddSingleton<IHorlogeSentier>(horloge);
        services.AddSingleton<IDossiersDeclarations>(simulateur);
        services.AddSingleton<IServiceIdentification>(simulateur);
        services.AddSingleton<IServiceConformite>(simulateur);
        services.AddSingleton<IServiceVerification>(simulateur);
        services.AddSingleton<IServiceComptable>(simulateur);
        using var fournisseur = services.BuildServiceProvider();

        string[] processus = ["traitement-declaration", "identification-declaration", "conformite-declaration", "verification-declaration", "comptabilisation-declaration"];
        const long numero = 1001;

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

        async Task CompleterTache()
        {
            var instances = await Transaction(s => s.GetRequiredService<ISuiviSentier>().ObtenirInstancesParCleAffaireAsync(numero, processus));
            var idTache = instances.Single(i => i.IdTacheCourante is not null).IdTacheCourante!.Value;
            await EnTransaction(s => s.GetRequiredService<IServiceSentier>().CompleterTacheAsync(idTache, utilisateur: "agent"));
        }

        await EnTransaction(s => s.GetRequiredService<InstallateurSchemaSentier>().InstallerAsync());
        await EnTransaction(s => s.GetRequiredService<IServiceSentier>().DeployerDefinitionsAsync());
        await EnTransaction(async s =>
        {
            await s.GetRequiredService<IDossiersDeclarations>().EnregistrerAsync(new Declaration(numero, "2026-02", "ORIGINALE", 1205.00m), default);
            await s.GetRequiredService<IServiceSentier>().DemarrerAsync("traitement-declaration", numero);
        });

        await CompleterTache();
        simulateur.CorrigerMontant(numero, 1250.00m);
        await CompleterTache();
        simulateur.DeciderAutorisation(numero, false);
        await CompleterTache();
        await CompleterTache();
        simulateur.DeciderAutorisation(numero, true);
        await CompleterTache();

        await EnTransaction(s => s.GetRequiredService<IServiceSentier>().EnvoyerSignalAsync(
            VerificationDeclaration.SignalRetourVerification, "verification-declaration", numero));

        horloge.MaintenantUtc += ComptabilisationDeclaration.DelaiComptabilisation + TimeSpan.FromMinutes(1);
        var echeances = await Transaction(s => s.GetRequiredService<IServiceSentier>().ObtenirEcheancesDuesAsync());
        Assert.NotEmpty(echeances);
        foreach (var echeance in echeances)
        {
            await EnTransaction(s => s.GetRequiredService<IServiceSentier>().TraiterEcheanceAsync(echeance.IdAttente));
        }

        await EnTransaction(s =>
        {
            simulateur.RecevoirRetourComptable(numero, "CP-03");
            return s.GetRequiredService<IServiceSentier>().EnvoyerSignalAsync(
                ComptabilisationDeclaration.SignalRetourComptable, "comptabilisation-declaration", numero);
        });
        await CompleterTache();

        await EnTransaction(async s =>
        {
            var suivi = s.GetRequiredService<ISuiviSentier>();
            foreach (var instance in await suivi.ObtenirInstancesParCleAffaireAsync(numero, processus))
            {
                await suivi.ObtenirHistoriqueAsync(instance.Id);
            }

            foreach (var cle in processus)
            {
                await suivi.ExporterDiagrammeAsync(cle);
            }
        });

        var final = await Transaction(s => s.GetRequiredService<ISuiviSentier>().ObtenirInstancesParCleAffaireAsync(numero, processus));
        Assert.Equal(StatutInstance.Terminee, final.Single(i => i.CleProcessus == "traitement-declaration").Statut);

        return journal;
    }

    /// <summary>Requêtes reçues par l'exécuteur, dans l'ordre.</summary>
    public sealed class JournalSql
    {
        public List<string> Requetes { get; } = [];
    }

    /// <summary>Enregistre chaque requête puis la délègue à l'exécuteur Dapper par défaut.</summary>
    public sealed class ExecuteurEnregistreur(ISessionSentier session, JournalSql journal) : IExecuteurSqlSentier
    {
        private readonly ExecuteurSqlDapper _dapper = new(session);

        public Task<int> ExecuterAsync(string sql, object? parametres, CancellationToken ct)
            => _dapper.ExecuterAsync(Noter(sql), parametres, ct);

        public Task<T?> ExecuterScalaireAsync<T>(string sql, object? parametres, CancellationToken ct)
            => _dapper.ExecuterScalaireAsync<T>(Noter(sql), parametres, ct);

        public Task<IEnumerable<T>> LireAsync<T>(string sql, object? parametres, CancellationToken ct)
            => _dapper.LireAsync<T>(Noter(sql), parametres, ct);

        public Task<T?> LirePremierOuDefautAsync<T>(string sql, object? parametres, CancellationToken ct)
            => _dapper.LirePremierOuDefautAsync<T>(Noter(sql), parametres, ct);

        private string Noter(string sql)
        {
            journal.Requetes.Add(sql);
            return sql;
        }
    }
}
