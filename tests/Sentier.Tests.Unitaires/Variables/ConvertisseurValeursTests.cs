using System.Text.Json;
using Sentier.Variables;

namespace Sentier.Tests.Unitaires.Variables;

public class ConvertisseurValeursTests
{
    [Theory]
    [InlineData(TypeVariable.Texte, typeof(string))]
    [InlineData(TypeVariable.Entier, typeof(long))]
    [InlineData(TypeVariable.Decimal, typeof(decimal))]
    [InlineData(TypeVariable.Booleen, typeof(bool))]
    [InlineData(TypeVariable.Date, typeof(DateTime))]
    [InlineData(TypeVariable.Json, typeof(JsonElement))]
    public void TypeClr_donne_le_type_canonique(TypeVariable type, Type attendu)
        => Assert.Equal(attendu, ConvertisseurValeurs.TypeClr(type));

    public static TheoryData<object, TypeVariable> ValeursEtTypes => new()
    {
        { "texte", TypeVariable.Texte },
        { 'c', TypeVariable.Texte },
        { 5L, TypeVariable.Entier },
        { 5, TypeVariable.Entier },
        { (short)5, TypeVariable.Entier },
        { (byte)5, TypeVariable.Entier },
        { 5u, TypeVariable.Entier },
        { 1.5m, TypeVariable.Decimal },
        { 1.5d, TypeVariable.Decimal },
        { 1.5f, TypeVariable.Decimal },
        { true, TypeVariable.Booleen },
        { new DateTime(2026, 1, 1), TypeVariable.Date },
        { new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), TypeVariable.Date },
        { new[] { 1, 2 }, TypeVariable.Json },
    };

    [Theory]
    [MemberData(nameof(ValeursEtTypes))]
    public void DeduireType_reconnait_les_types_clr(object valeur, TypeVariable attendu)
        => Assert.Equal(attendu, ConvertisseurValeurs.DeduireType(valeur));

    [Fact]
    public void Normaliser_laisse_passer_null()
        => Assert.Null(ConvertisseurValeurs.Normaliser(TypeVariable.Entier, null, "x"));

    [Fact]
    public void Normaliser_convertit_les_entiers_en_long()
    {
        Assert.Equal(7L, ConvertisseurValeurs.Normaliser(TypeVariable.Entier, 7, "x"));
        Assert.Equal(7L, ConvertisseurValeurs.Normaliser(TypeVariable.Entier, (short)7, "x"));
        Assert.Equal(7L, ConvertisseurValeurs.Normaliser(TypeVariable.Entier, 7UL, "x"));
    }

    [Fact]
    public void Normaliser_refuse_un_ulong_trop_grand()
        => Assert.Throws<VariableInvalideException>(() => ConvertisseurValeurs.Normaliser(TypeVariable.Entier, ulong.MaxValue, "x"));

    [Fact]
    public void Normaliser_convertit_entiers_et_flottants_en_decimal()
    {
        Assert.Equal(3m, ConvertisseurValeurs.Normaliser(TypeVariable.Decimal, 3L, "x"));
        Assert.Equal(3m, ConvertisseurValeurs.Normaliser(TypeVariable.Decimal, 3, "x"));
        Assert.Equal(2.5m, ConvertisseurValeurs.Normaliser(TypeVariable.Decimal, 2.5d, "x"));
    }

    [Fact]
    public void Normaliser_convertit_les_caracteres_en_texte()
        => Assert.Equal("a", ConvertisseurValeurs.Normaliser(TypeVariable.Texte, 'a', "x"));

    [Fact]
    public void Normaliser_ramene_les_dates_en_utc()
    {
        var non = new DateTime(2026, 5, 1, 10, 0, 0, DateTimeKind.Unspecified);
        var resultat = (DateTime)ConvertisseurValeurs.Normaliser(TypeVariable.Date, non, "x")!;
        Assert.Equal(DateTimeKind.Utc, resultat.Kind);
        Assert.Equal(non.Ticks, resultat.Ticks);

        var offset = new DateTimeOffset(2026, 5, 1, 10, 0, 0, TimeSpan.FromHours(-4));
        Assert.Equal(new DateTime(2026, 5, 1, 14, 0, 0, DateTimeKind.Utc), ConvertisseurValeurs.Normaliser(TypeVariable.Date, offset, "x"));
    }

    [Fact]
    public void Normaliser_serialise_tout_objet_en_json()
    {
        var resultat = Assert.IsType<JsonElement>(ConvertisseurValeurs.Normaliser(TypeVariable.Json, new { a = 1 }, "x"));
        Assert.Equal(1, resultat.GetProperty("a").GetInt32());
    }

    [Theory]
    [InlineData(TypeVariable.Entier, "texte")]
    [InlineData(TypeVariable.Texte, 12)]
    [InlineData(TypeVariable.Booleen, 1)]
    [InlineData(TypeVariable.Date, "2026-01-01")]
    [InlineData(TypeVariable.Decimal, "1.5")]
    public void Normaliser_refuse_un_type_incompatible(TypeVariable type, object valeur)
    {
        var ex = Assert.Throws<VariableInvalideException>(() => ConvertisseurValeurs.Normaliser(type, valeur, "maVariable"));
        Assert.Contains("maVariable", ex.Message);
    }

    [Fact]
    public void DepuisJson_utilise_le_type_declare()
    {
        using var document = JsonDocument.Parse("""{"t":"abc","e":42,"d":1.25,"b":true,"date":"2026-02-03T04:05:06Z","n":null,"o":{"x":1}}""");
        var racine = document.RootElement;
        Assert.Equal("abc", ConvertisseurValeurs.DepuisJson(TypeVariable.Texte, racine.GetProperty("t"), "t"));
        Assert.Equal(42L, ConvertisseurValeurs.DepuisJson(TypeVariable.Entier, racine.GetProperty("e"), "e"));
        Assert.Equal(42m, ConvertisseurValeurs.DepuisJson(TypeVariable.Decimal, racine.GetProperty("e"), "e"));
        Assert.Equal(1.25m, ConvertisseurValeurs.DepuisJson(TypeVariable.Decimal, racine.GetProperty("d"), "d"));
        Assert.Equal(true, ConvertisseurValeurs.DepuisJson(TypeVariable.Booleen, racine.GetProperty("b"), "b"));
        Assert.Equal(new DateTime(2026, 2, 3, 4, 5, 6, DateTimeKind.Utc), ConvertisseurValeurs.DepuisJson(TypeVariable.Date, racine.GetProperty("date"), "date"));
        Assert.Null(ConvertisseurValeurs.DepuisJson(TypeVariable.Entier, racine.GetProperty("n"), "n"));
        Assert.Equal(1, ((JsonElement)ConvertisseurValeurs.DepuisJson(TypeVariable.Json, racine.GetProperty("o"), "o")!).GetProperty("x").GetInt32());
    }

    [Fact]
    public void DepuisJson_refuse_un_nombre_decimal_pour_un_entier()
    {
        using var document = JsonDocument.Parse("1.5");
        Assert.Throws<VariableInvalideException>(() => ConvertisseurValeurs.DepuisJson(TypeVariable.Entier, document.RootElement, "x"));
    }

    [Fact]
    public void Normaliser_accepte_un_element_json()
    {
        using var document = JsonDocument.Parse("12");
        Assert.Equal(12L, ConvertisseurValeurs.Normaliser(TypeVariable.Entier, document.RootElement, "x"));
    }

    [Theory]
    [InlineData(TypeVariable.Entier, TypeVariable.Entier, true)]
    [InlineData(TypeVariable.Entier, TypeVariable.Decimal, true)]
    [InlineData(TypeVariable.Decimal, TypeVariable.Entier, false)]
    [InlineData(TypeVariable.Texte, TypeVariable.Json, true)]
    [InlineData(TypeVariable.Json, TypeVariable.Texte, false)]
    [InlineData(TypeVariable.Booleen, TypeVariable.Texte, false)]
    public void SontCompatibles(TypeVariable source, TypeVariable cible, bool attendu)
        => Assert.Equal(attendu, ConvertisseurValeurs.SontCompatibles(source, cible));

    private enum Couleur
    {
        Rouge,
        Vert,
    }

    private sealed record Adresse(string Ville, int Code);

    [Fact]
    public void VersType_convertit_vers_le_type_demande()
    {
        Assert.Equal(5, ConvertisseurValeurs.VersType<int>(5L, "x"));
        Assert.Equal(5L, ConvertisseurValeurs.VersType<long?>(5L, "x"));
        Assert.Equal("5", ConvertisseurValeurs.VersType<string>("5", "x"));
        Assert.Equal(2.5d, ConvertisseurValeurs.VersType<double>(2.5m, "x"));
        Assert.Equal(Couleur.Vert, ConvertisseurValeurs.VersType<Couleur>("vert", "x"));
    }

    [Fact]
    public void VersType_donne_la_valeur_par_defaut_pour_null()
    {
        Assert.Equal(0L, ConvertisseurValeurs.VersType<long>(null, "x"));
        Assert.Null(ConvertisseurValeurs.VersType<long?>(null, "x"));
        Assert.Null(ConvertisseurValeurs.VersType<string>(null, "x"));
    }

    [Fact]
    public void VersType_deserialise_un_element_json()
    {
        var element = JsonSerializer.SerializeToElement(new Adresse("Québec", 418));
        Assert.Equal(new Adresse("Québec", 418), ConvertisseurValeurs.VersType<Adresse>(element, "adresse"));
    }

    [Fact]
    public void VersType_signale_une_conversion_impossible()
    {
        Assert.Throws<VariableInvalideException>(() => ConvertisseurValeurs.VersType<int>("abc", "x"));
        Assert.Throws<VariableInvalideException>(() => ConvertisseurValeurs.VersType<Adresse>(12L, "x"));
    }
}
