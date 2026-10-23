using Sentier.Persistance.Dapper;
using Sentier.Persistance.Oracle;
using Sentier.Persistance.Sqlite;
using Sentier.Tests.Unitaires.Support;

namespace Sentier.Tests.Unitaires.Persistance;

public class NomsObjetsTests
{
    [Fact]
    public void Noms_prefixes_en_majuscules()
    {
        var noms = new NomsObjets("sentier");
        Assert.Equal("SENTIER", noms.Prefixe);
        Assert.Equal("SENTIER_DEFIN_PROCS", noms.Definition);
        Assert.Equal("SENTIER_INSTC_PROCS", noms.Instance);
        Assert.Equal("SENTIER_HIST_NOEUD_PROCS", noms.HistNoeud);
        Assert.Equal("SENTIER_ATTEN_PROCS", noms.Attente);
        Assert.Equal("SENTIER_MIGRA_PROCS", noms.Migration);
        Assert.Equal("SENTIER_V_INSTC_ACTIV", noms.VueInstanceActive);
        Assert.Equal("SENTIER_SEQ", noms.TableSequences);
        Assert.Equal("SENTIER_SEQ_DEFIN", noms.SeqDefinition);
        Assert.Equal("SENTIER_SEQ_INSTC", noms.SeqInstance);
        Assert.Equal("SENTIER_SEQ_HIST_NOEUD", noms.SeqHistNoeud);
        Assert.Equal("SENTIER_SEQ_ATTEN", noms.SeqAttente);
        Assert.Equal("SENTIER_SEQ_MIGRA", noms.SeqMigration);
    }

    [Fact]
    public void Appliquer_remplace_le_jeton()
        => Assert.Equal("CREATE TABLE ABC_X; DROP ABC_Y", new NomsObjets("abc").Appliquer("CREATE TABLE {{P}}_X; DROP {{P}}_Y"));

    [Theory]
    [InlineData("")]
    [InlineData("_A")]
    [InlineData("A B")]
    [InlineData("A-B")]
    public void Prefixe_invalide(string prefixe)
        => Assert.Throws<SentierException>(() => new NomsObjets(prefixe));

    [Fact]
    public void Prefixe_trop_long_pour_oracle()
    {
        Assert.Throws<SentierException>(() => new NomsObjets("ABCDEFGHIJKLMN"));
        Assert.Equal(30, new NomsObjets("ABCDEFGHIJKLM").HistNoeud.Length);
    }
}

public class DialectesTests
{
    [Fact]
    public void Sqlite()
    {
        var dialecte = new DialecteSqlite();
        Assert.Equal(DialecteBase.Sqlite, dialecte.Type);
        Assert.Equal("@id", dialecte.Parametre("id"));
        Assert.Equal("", dialecte.VerrouillageSansAttente);
        Assert.Equal("SELECT 1 LIMIT 5", dialecte.Limiter("SELECT 1", 5));
        Assert.Equal("SELECT 1 LIMIT 5 OFFSET 10", dialecte.Paginer("SELECT 1", 10, 5));
    }

    [Fact]
    public void Oracle()
    {
        var dialecte = new DialecteOracle();
        Assert.Equal(DialecteBase.Oracle, dialecte.Type);
        Assert.Equal(":id", dialecte.Parametre("id"));
        Assert.Equal(" FOR UPDATE SKIP LOCKED", dialecte.VerrouillageSansAttente);
        Assert.Equal("SELECT * FROM (SELECT 1) WHERE ROWNUM <= 5", dialecte.Limiter("SELECT 1", 5));
        Assert.Equal("SELECT * FROM (SELECT t__.*, ROWNUM AS RN__ FROM (SELECT 1) t__ WHERE ROWNUM <= 15) WHERE RN__ > 10", dialecte.Paginer("SELECT 1", 10, 5));
    }

    [Theory]
    [InlineData(typeof(DialecteSqlite))]
    [InlineData(typeof(DialecteOracle))]
    public void Le_script_embarque_est_prefixe_et_decoupable(Type typeDialecte)
    {
        var dialecte = (IDialecteSql)Activator.CreateInstance(typeDialecte)!;
        var script = dialecte.ScriptSchema(new NomsObjets("TST"));

        Assert.DoesNotContain(NomsObjets.JetonPrefixe, script);
        Assert.Contains("TST_INSTC_PROCS", script);

        var instructions = dialecte.DecouperScript(script);
        Assert.True(instructions.Count > 5);
        Assert.All(instructions, i => Assert.False(i.TrimStart().StartsWith("--", StringComparison.Ordinal)));
        Assert.All(instructions, i => Assert.False(i.EndsWith(';')));
        Assert.Contains(instructions, i => i.Contains("CREATE TABLE") && i.Contains("TST_DEFIN_PROCS"));
    }

    [Fact]
    public void Decouper_ignore_les_commentaires_et_les_lignes_vides()
    {
        var instructions = new DialecteSqlite().DecouperScript("-- commentaire\nCREATE TABLE A (X INT);\n\n  -- autre\nINSERT INTO A VALUES (1);   \nSELECT ';' AS x;");
        Assert.Equal(["CREATE TABLE A (X INT)", "INSERT INTO A VALUES (1)", "SELECT ';' AS x"], instructions);
    }
}

public class ContexteSqlTests
{
    private sealed class SessionFactice : ISessionSentier
    {
        public System.Data.IDbConnection Connexion { get; } = new ConnexionFactice();

        public System.Data.IDbTransaction? Transaction { get; set; }
    }

    [Fact]
    public void Traduire_les_parametres_selon_le_dialecte()
    {
        var oracle = new ContexteSql(new SessionFactice(), new DialecteOracle(), new NomsObjets("S"));
        Assert.Equal("SELECT * FROM T WHERE A = :a AND B = :b_2 AND C = 'x@y' AND D = @@e", oracle.Traduire("SELECT * FROM T WHERE A = @a AND B = @b_2 AND C = 'x@y' AND D = @@e"));

        var sqlite = new ContexteSql(new SessionFactice(), new DialecteSqlite(), new NomsObjets("S"));
        Assert.Equal("SELECT @a", sqlite.Traduire("SELECT @a"));
    }

    [Fact]
    public void Lecture_sans_transaction_ecriture_avec()
    {
        var session = new SessionFactice();
        var contexte = new ContexteSql(session, new DialecteOracle(), new NomsObjets("S"));

        var lecture = contexte.Lecture("SELECT @a", new { a = 1 }, default);
        Assert.Equal("SELECT :a", lecture.CommandText);
        Assert.Null(lecture.Transaction);
        Assert.Throws<TransactionRequiseException>(() => contexte.Ecriture("DELETE", null, default));

        session.Transaction = new TransactionFactice(session.Connexion);
        Assert.Same(session.Transaction, contexte.Ecriture("DELETE", null, default).Transaction);
        Assert.Same(session.Transaction, contexte.Lecture("SELECT 1", null, default).Transaction);
        Assert.Same(session.Connexion, contexte.Connexion);
    }

    [Fact]
    public void Conversions_utilitaires()
    {
        Assert.Equal(1, ContexteSql.Booleen(true));
        Assert.Equal(0, ContexteSql.Booleen(false));
        Assert.Equal(DateTimeKind.Utc, ContexteSql.Utc(new DateTime(2026, 1, 1)).Kind);
        Assert.Null(ContexteSql.Utc((DateTime?)null));
        Assert.Equal(DateTimeKind.Utc, ContexteSql.Utc((DateTime?)new DateTime(2026, 1, 1))!.Value.Kind);
    }
}

public class CodesBaseTests
{
    [Theory]
    [InlineData(StatutInstance.EnCours, "EN_COURS")]
    [InlineData(StatutInstance.Suspendue, "SUSPENDUE")]
    [InlineData(StatutInstance.Terminee, "TERMINEE")]
    [InlineData(StatutInstance.Annulee, "ANNULEE")]
    [InlineData(StatutInstance.EnErreur, "EN_ERREUR")]
    public void Statuts(StatutInstance statut, string code)
    {
        Assert.Equal(code, CodesBase.Code(statut));
        Assert.Equal(statut, CodesBase.LireStatutInstance(code));
    }

    [Fact]
    public void Types_de_noeud_aller_retour()
    {
        foreach (var type in Enum.GetValues<TypeNoeud>())
        {
            Assert.Equal(type, CodesBase.LireTypeNoeud(CodesBase.Code(type)));
        }

        Assert.Equal("SOUS_PROCESSUS", CodesBase.Code(TypeNoeud.SousProcessus));
    }

    [Fact]
    public void Declencheurs_et_modes()
    {
        Assert.Equal(TypeDeclencheur.Echeance, CodesBase.LireTypeDeclencheur(CodesBase.Code(TypeDeclencheur.Echeance)));
        Assert.Equal(TypeDeclencheur.Signal, CodesBase.LireTypeDeclencheur(CodesBase.Code(TypeDeclencheur.Signal)));
        Assert.Equal("AUTO", CodesBase.Code(ModeMigration.Auto));
        Assert.Equal("PLAN", CodesBase.Code(ModeMigration.Plan));
    }

    [Fact]
    public void Codes_inconnus()
    {
        Assert.Throws<SentierException>(() => CodesBase.LireStatutInstance("X"));
        Assert.Throws<SentierException>(() => CodesBase.LireTypeNoeud("X"));
        Assert.Throws<SentierException>(() => CodesBase.LireTypeDeclencheur("X"));
        Assert.Throws<ArgumentOutOfRangeException>(() => CodesBase.Code((StatutInstance)99));
        Assert.Throws<ArgumentOutOfRangeException>(() => CodesBase.Code((TypeNoeud)99));
    }
}
