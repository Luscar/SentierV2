using Sentier.Definitions;
using Sentier.Variables;

namespace Sentier.Core.Tests.Construction;

/// <summary>La construction lève <see cref="DefinitionInvalideException"/> avec la liste complète des erreurs.</summary>
public sealed class ValidationDefinitionTests
{
    private static readonly Variable<decimal> Montant = Variable.Decimal("montant");
    private static readonly Variable<bool> Urgent = Variable.Booleen("urgent");
    private static readonly Variable<string> Texte = Variable.Texte("texte");

    private static DefinitionInvalideException Invalide(Func<ConstructeurProcessus, ConstructeurProcessus> definir)
        => Assert.Throws<DefinitionInvalideException>(() => definir(ConstructeurProcessus.Nouveau("invalide")).Construire());

    private static void ContientErreur(DefinitionInvalideException ex, string fragment)
        => Assert.True(ex.Erreurs.Any(e => e.Contains(fragment, StringComparison.Ordinal)),
            $"Erreur « {fragment} » attendue parmi :\n- {string.Join("\n- ", ex.Erreurs)}");

    [Fact]
    public void Un_processus_vide_est_refuse()
    {
        var ex = Assert.Throws<DefinitionInvalideException>(() => ConstructeurProcessus.Nouveau("vide").Construire());
        Assert.Equal("vide", ex.CleProcessus);
        Assert.NotEmpty(ex.Erreurs);
    }

    [Fact]
    public void Il_faut_au_moins_une_fin()
        => ContientErreur(Invalide(p => p.Systemique<CommandeFictive>("a")), "au moins un nœud de fin");

    [Fact]
    public void Les_codes_de_noeuds_sont_uniques()
        => ContientErreur(Invalide(p => p.Systemique<CommandeFictive>("a").Systemique<CommandeFictive>("a").Fin("fin")), "'a' est utilisé plusieurs fois");

    [Fact]
    public void Un_lien_vers_un_code_inexistant_est_refuse()
        => ContientErreur(Invalide(p => p.Systemique<CommandeFictive>("a").Vers("absent").Fin("fin")), "inexistant 'absent'");

    [Fact]
    public void Un_noeud_jamais_atteint_est_refuse()
        => ContientErreur(Invalide(p => p.Fin("fin").Systemique<CommandeFictive>("orphelin").Fin("autre")), "'orphelin' n'est jamais atteint");

    [Fact]
    public void Une_decision_exige_une_branche_conditionnelle()
        => ContientErreur(Invalide(p => p.Decision("d", d => d.Sinon()).Fin("fin")), "au moins une branche conditionnelle");

    [Fact]
    public void Une_decision_exige_exactement_un_sinon()
        => ContientErreur(Invalide(p => p.Decision("d", d => d.Si<RequeteFictive>()).Fin("fin")), "exactement une branche « sinon »");

    [Fact]
    public void Une_attente_exige_un_declencheur()
        => ContientErreur(Invalide(p => p.Attente("a", _ => { }).Fin("fin")), "au moins un déclencheur");

    [Fact]
    public void Une_attente_a_au_plus_une_echeance()
        => ContientErreur(
            Invalide(p => p.Attente("a", a => a.Echeance(TimeSpan.FromHours(1)).Echeance(TimeSpan.FromHours(2)).Vers("fin")).Fin("fin")),
            "plus d'une échéance");

    [Fact]
    public void Une_condition_sur_une_variable_non_declaree_est_refusee()
        => ContientErreur(
            Invalide(p => p.Decision("d", d => d.Si(Montant).SuperieurA(1m).Vers("fin").Sinon()).Fin("fin")),
            "'montant'");

    [Fact]
    public void Un_operateur_inapplicable_au_type_est_refuse()
    {
        var ex = Invalide(p => p
            .Variables(Urgent)
            .Decision("d", d => d.Si(Urgent).SuperieurA(true).Vers("fin").Sinon())
            .Fin("fin"));

        ContientErreur(ex, "ne s'applique pas");
    }

    [Fact]
    public void Contient_est_reserve_aux_textes()
    {
        var ex = Invalide(p => p
            .Variables(Montant)
            .Variable("autre", TypeVariable.Entier)
            .Decision("d", d => d.Si(Variable.Entier("autre")).Contient("x").Vers("fin").Sinon())
            .Fin("fin"));

        ContientErreur(ex, "réservé aux variables texte");
    }

    [Fact]
    public void Les_noms_reserves_sont_refuses()
        => ContientErreur(Invalide(p => p.Variable("_interne", TypeVariable.Texte).Fin("fin")), "réservé");

    [Fact]
    public void Une_variable_declaree_deux_fois_differemment_est_refusee()
        => ContientErreur(
            Invalide(p => p.Variables(Texte).Variable("texte", TypeVariable.Entier).Fin("fin")),
            "déclarée plusieurs fois");

    [Fact]
    public void Un_parametre_vers_une_variable_non_declaree_est_refuse()
        => ContientErreur(
            Invalide(p => p.Systemique<CommandeFictive>("a", c => c.Variable(Montant)).Fin("fin")),
            "'montant'");

    [Fact]
    public void Nommee_sans_branche_est_refuse()
        => ContientErreur(Invalide(p => p.Decision("d", d => d.Nommee("x").Si<RequeteFictive>().Sinon()).Fin("fin")), "doit suivre la déclaration d'une branche");

    [Fact]
    public void Une_branche_avec_deux_destinations_est_refusee()
        => ContientErreur(
            Invalide(p => p.Decision("d", d => d.Si<RequeteFictive>().Vers("fin").Vers("fin").Sinon()).Fin("fin")),
            "a déjà une destination");

    [Fact]
    public void Toutes_les_erreurs_sont_rapportees_ensemble()
    {
        var ex = Invalide(p => p
            .Systemique<CommandeFictive>("a")
            .Systemique<CommandeFictive>("a")
            .Decision("d", d => d.Sinon()));

        Assert.True(ex.Erreurs.Count >= 3, string.Join("\n", ex.Erreurs));
        Assert.Contains("invalide", ex.Message);
    }

    [Fact]
    public void La_cle_du_processus_est_obligatoire()
        => Assert.ThrowsAny<Exception>(() => ConstructeurProcessus.Nouveau(" ").Fin("fin").Construire());
}
