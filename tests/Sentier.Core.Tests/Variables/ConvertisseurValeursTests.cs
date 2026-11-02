using System.Text.Json;
using Sentier.Variables;

namespace Sentier.Core.Tests.Variables;

public sealed class ConvertisseurValeursTests
{
    [Theory]
    [InlineData(TypeVariable.Texte, typeof(string))]
    [InlineData(TypeVariable.Entier, typeof(long))]
    [InlineData(TypeVariable.Decimal, typeof(decimal))]
    [InlineData(TypeVariable.Booleen, typeof(bool))]
    [InlineData(TypeVariable.Date, typeof(DateTime))]
    [InlineData(TypeVariable.Json, typeof(JsonElement))]
    public void Donne_le_type_clr_de_chaque_type_de_variable(TypeVariable type, Type attendu)
        => Assert.Equal(attendu, ConvertisseurValeurs.TypeClr(type));

    [Fact]
    public void Deduit_le_type_d_une_valeur()
    {
        Assert.Equal(TypeVariable.Texte, ConvertisseurValeurs.DeduireType("a"));
        Assert.Equal(TypeVariable.Texte, ConvertisseurValeurs.DeduireType('a'));
        Assert.Equal(TypeVariable.Entier, ConvertisseurValeurs.DeduireType(3));
        Assert.Equal(TypeVariable.Entier, ConvertisseurValeurs.DeduireType(3L));
        Assert.Equal(TypeVariable.Decimal, ConvertisseurValeurs.DeduireType(3.5));
        Assert.Equal(TypeVariable.Decimal, ConvertisseurValeurs.DeduireType(3.5m));
        Assert.Equal(TypeVariable.Booleen, ConvertisseurValeurs.DeduireType(true));
        Assert.Equal(TypeVariable.Date, ConvertisseurValeurs.DeduireType(DateTime.UtcNow));
        Assert.Equal(TypeVariable.Date, ConvertisseurValeurs.DeduireType(DateTimeOffset.UtcNow));
        Assert.Equal(TypeVariable.Json, ConvertisseurValeurs.DeduireType(new { a = 1 }));
    }

    [Fact]
    public void Normalise_les_entiers_en_long()
    {
        Assert.Equal(42L, ConvertisseurValeurs.Normaliser(TypeVariable.Entier, 42, "n"));
        Assert.Equal(42L, ConvertisseurValeurs.Normaliser(TypeVariable.Entier, (short)42, "n"));
        Assert.Equal(42L, ConvertisseurValeurs.Normaliser(TypeVariable.Entier, 42UL, "n"));
    }

    [Fact]
    public void Normalise_les_nombres_en_decimal()
    {
        Assert.Equal(1.5m, ConvertisseurValeurs.Normaliser(TypeVariable.Decimal, 1.5, "m"));
        Assert.Equal(7m, ConvertisseurValeurs.Normaliser(TypeVariable.Decimal, 7L, "m"));
        Assert.Equal(7m, ConvertisseurValeurs.Normaliser(TypeVariable.Decimal, 7, "m"));
    }

    [Fact]
    public void Normalise_les_dates_en_utc()
    {
        var non = new DateTime(2026, 5, 1, 8, 0, 0, DateTimeKind.Unspecified);
        var resultat = (DateTime)ConvertisseurValeurs.Normaliser(TypeVariable.Date, non, "d")!;
        Assert.Equal(DateTimeKind.Utc, resultat.Kind);
        Assert.Equal(non.Ticks, resultat.Ticks);

        var offset = new DateTimeOffset(2026, 5, 1, 8, 0, 0, TimeSpan.FromHours(-4));
        var depuisOffset = (DateTime)ConvertisseurValeurs.Normaliser(TypeVariable.Date, offset, "d")!;
        Assert.Equal(new DateTime(2026, 5, 1, 12, 0, 0, DateTimeKind.Utc), depuisOffset);
    }

    [Fact]
    public void Normalise_un_objet_en_json()
    {
        var resultat = ConvertisseurValeurs.Normaliser(TypeVariable.Json, new { ville = "Québec" }, "adresse");
        var element = Assert.IsType<JsonElement>(resultat);
        Assert.Equal("Québec", element.GetProperty("ville").GetString());
    }

    [Fact]
    public void Null_reste_null()
        => Assert.Null(ConvertisseurValeurs.Normaliser(TypeVariable.Entier, null, "n"));

    [Theory]
    [InlineData(TypeVariable.Entier, "12")]
    [InlineData(TypeVariable.Texte, 12)]
    [InlineData(TypeVariable.Booleen, "true")]
    [InlineData(TypeVariable.Entier, 1.5)]
    public void Refuse_une_valeur_d_un_type_incompatible(TypeVariable type, object valeur)
    {
        var ex = Assert.Throws<VariableInvalideException>(() => ConvertisseurValeurs.Normaliser(type, valeur, "x"));
        Assert.Contains("'x'", ex.Message);
    }

    [Fact]
    public void Refuse_un_ulong_qui_deborde()
        => Assert.Throws<VariableInvalideException>(() => ConvertisseurValeurs.Normaliser(TypeVariable.Entier, ulong.MaxValue, "n"));

    [Fact]
    public void Lit_les_valeurs_json_selon_le_type_declare()
    {
        using var document = JsonDocument.Parse("""{"t":"a","e":5,"d":1.25,"b":true,"date":"2026-01-02T03:04:05Z","n":null}""");
        var racine = document.RootElement;

        Assert.Equal("a", ConvertisseurValeurs.DepuisJson(TypeVariable.Texte, racine.GetProperty("t"), "t"));
        Assert.Equal(5L, ConvertisseurValeurs.DepuisJson(TypeVariable.Entier, racine.GetProperty("e"), "e"));
        Assert.Equal(1.25m, ConvertisseurValeurs.DepuisJson(TypeVariable.Decimal, racine.GetProperty("d"), "d"));
        Assert.Equal(true, ConvertisseurValeurs.DepuisJson(TypeVariable.Booleen, racine.GetProperty("b"), "b"));
        Assert.Equal(new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc), ConvertisseurValeurs.DepuisJson(TypeVariable.Date, racine.GetProperty("date"), "date"));
        Assert.Null(ConvertisseurValeurs.DepuisJson(TypeVariable.Entier, racine.GetProperty("n"), "n"));
        Assert.Throws<VariableInvalideException>(() => ConvertisseurValeurs.DepuisJson(TypeVariable.Entier, racine.GetProperty("t"), "t"));
        Assert.Throws<VariableInvalideException>(() => ConvertisseurValeurs.DepuisJson(TypeVariable.Entier, racine.GetProperty("d"), "d"));
    }

    [Theory]
    [InlineData(TypeVariable.Entier, TypeVariable.Entier, true)]
    [InlineData(TypeVariable.Entier, TypeVariable.Decimal, true)]
    [InlineData(TypeVariable.Decimal, TypeVariable.Entier, false)]
    [InlineData(TypeVariable.Texte, TypeVariable.Json, true)]
    [InlineData(TypeVariable.Texte, TypeVariable.Entier, false)]
    public void Indique_les_types_compatibles(TypeVariable source, TypeVariable cible, bool attendu)
        => Assert.Equal(attendu, ConvertisseurValeurs.SontCompatibles(source, cible));

    private enum Priorite { Basse, Haute }

    private sealed record Adresse(string Rue, int Numero);

    [Fact]
    public void Convertit_vers_le_type_demande()
    {
        Assert.Equal(5, ConvertisseurValeurs.VersType<int>(5L, "n"));
        Assert.Equal(Priorite.Haute, ConvertisseurValeurs.VersType<Priorite>("haute", "p"));
        Assert.Equal(0L, ConvertisseurValeurs.VersType<long>(null, "n"));
        Assert.Null(ConvertisseurValeurs.VersType<long?>(null, "n"));

        var json = JsonSerializer.SerializeToElement(new Adresse("Principale", 12));
        Assert.Equal(new Adresse("Principale", 12), ConvertisseurValeurs.VersType<Adresse>(json, "adresse"));
    }

    [Fact]
    public void Signale_une_conversion_impossible()
    {
        Assert.Throws<VariableInvalideException>(() => ConvertisseurValeurs.VersType<int>("abc", "n"));
        Assert.Throws<VariableInvalideException>(() => ConvertisseurValeurs.VersType<int>(long.MaxValue, "n"));
        Assert.Throws<VariableInvalideException>(() => ConvertisseurValeurs.VersType<Adresse>(12L, "adresse"));
    }
}
