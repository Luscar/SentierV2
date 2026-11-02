namespace Sentier.Persistance.Tests;

public sealed class DialectesTests
{
    private static readonly NomsObjets Noms = new("SENTIER");

    [Fact]
    public void Sqlite_utilise_LIMIT_OFFSET_et_des_parametres_arobase()
    {
        var dialecte = new DialecteSqlite();

        Assert.Equal(TypeBaseDonnees.Sqlite, dialecte.Type);
        Assert.Equal("@id", dialecte.Parametre("id"));
        Assert.Equal("SELECT 1 LIMIT 5", dialecte.Limiter("SELECT 1", 5));
        Assert.Equal("SELECT 1 LIMIT 10 OFFSET 20", dialecte.Paginer("SELECT 1", 20, 10));
        Assert.False(dialecte.SupporteTransactionAmbiante);
        Assert.Equal("", dialecte.VerrouillageSansAttente);
    }

    [Fact]
    public void Oracle_utilise_ROWNUM_SKIP_LOCKED_et_des_parametres_deux_points()
    {
        var dialecte = new DialecteOracle();

        Assert.Equal(TypeBaseDonnees.Oracle, dialecte.Type);
        Assert.Equal(":id", dialecte.Parametre("id"));
        Assert.Equal("SELECT * FROM (SELECT 1) WHERE ROWNUM <= 5", dialecte.Limiter("SELECT 1", 5));
        Assert.Contains("ROWNUM <= 30", dialecte.Paginer("SELECT 1", 20, 10));
        Assert.Contains("RN__ > 20", dialecte.Paginer("SELECT 1", 20, 10));
        Assert.True(dialecte.SupporteTransactionAmbiante);
        Assert.Contains("SKIP LOCKED", dialecte.VerrouillageSansAttente);
    }

    [Theory]
    [MemberData(nameof(Dialectes))]
    public void Le_script_du_schema_porte_le_prefixe_sans_jeton_restant(IDialecteSql dialecte)
    {
        var script = dialecte.ScriptSchema(Noms);

        Assert.DoesNotContain(NomsObjets.JetonPrefixe, script);
        Assert.Contains(Noms.Instance, script);
        Assert.Contains(Noms.Definition, script);
        Assert.Contains(Noms.Attente, script);
        Assert.Contains(Noms.HistNoeud, script);
    }

    [Theory]
    [MemberData(nameof(Dialectes))]
    public void Le_script_se_decoupe_en_instructions_non_vides_sans_commentaires(IDialecteSql dialecte)
    {
        var instructions = dialecte.DecouperScript(dialecte.ScriptSchema(Noms));

        Assert.NotEmpty(instructions);
        Assert.All(instructions, i =>
        {
            Assert.False(string.IsNullOrWhiteSpace(i));
            Assert.False(i.TrimEnd().EndsWith(';'), i);
            Assert.DoesNotContain("\n--", "\n" + i);
        });
    }

    [Fact]
    public void Le_decoupage_ignore_les_commentaires_et_les_lignes_vides()
    {
        var instructions = new DialecteSqlite().DecouperScript("-- commentaire\nCREATE TABLE A (x INT);\n\n  -- autre\nCREATE TABLE B (y INT);  \n");

        Assert.Equal(["CREATE TABLE A (x INT)", "CREATE TABLE B (y INT)"], instructions);
    }

    public static TheoryData<IDialecteSql> Dialectes() => new() { new DialecteSqlite(), new DialecteOracle() };
}
