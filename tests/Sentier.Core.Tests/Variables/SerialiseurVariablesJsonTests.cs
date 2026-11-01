using System.Text.Json;
using Sentier.Variables;

namespace Sentier.Core.Tests.Variables;

public sealed class SerialiseurVariablesJsonTests
{
    private readonly SerialiseurVariablesJson _serialiseur = new();

    [Fact]
    public void Aller_retour_conserve_les_valeurs_et_leurs_types()
    {
        var date = new DateTime(2026, 3, 2, 9, 30, 0, DateTimeKind.Utc);
        var valeurs = new Dictionary<string, object?>
        {
            ["texte"] = "été",
            ["entier"] = 9_007_199_254_740_993L,
            ["decimal"] = 1250.10m,
            ["booleen"] = true,
            ["date"] = date,
            ["json"] = JsonSerializer.SerializeToElement(new { a = 1 }),
            ["vide"] = null,
        };
        var types = new Dictionary<string, TypeVariable>
        {
            ["texte"] = TypeVariable.Texte,
            ["entier"] = TypeVariable.Entier,
            ["decimal"] = TypeVariable.Decimal,
            ["booleen"] = TypeVariable.Booleen,
            ["date"] = TypeVariable.Date,
            ["json"] = TypeVariable.Json,
            ["vide"] = TypeVariable.Texte,
        };

        var relu = _serialiseur.Deserialiser(_serialiseur.Serialiser(valeurs), types);

        Assert.Equal("été", relu["texte"]);
        Assert.Equal(9_007_199_254_740_993L, relu["entier"]);
        Assert.Equal(1250.10m, relu["decimal"]);
        Assert.Equal(true, relu["booleen"]);
        Assert.Equal(date, relu["date"]);
        Assert.Equal(1, ((JsonElement)relu["json"]!).GetProperty("a").GetInt32());
        Assert.Null(relu["vide"]);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Un_json_vide_donne_un_dictionnaire_vide(string? json)
        => Assert.Empty(_serialiseur.Deserialiser(json, new Dictionary<string, TypeVariable>()));

    [Fact]
    public void Les_variables_systeme_sont_relues_comme_texte_et_les_inconnues_comme_json()
    {
        var relu = _serialiseur.Deserialiser("""{"_declencheur":"echeance","inconnue":{"x":1}}""", new Dictionary<string, TypeVariable>());

        Assert.Equal("echeance", relu["_declencheur"]);
        Assert.IsType<JsonElement>(relu["inconnue"]);
    }
}
