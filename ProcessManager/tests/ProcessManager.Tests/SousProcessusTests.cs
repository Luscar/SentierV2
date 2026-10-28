using System.Text.Json;
using ProcessManager.Tests.Exemple;

namespace ProcessManager.Tests;

public sealed class SousProcessusTests
{
    private readonly Contexte _c = new();
    private readonly Guid _aggregat = Guid.NewGuid();

    public SousProcessusTests() => _c.Fabrique.Enregistrer(() => new Parent());

    [Theory]
    [InlineData("signal", "signée")]
    [InlineData("delai", "non-signée")]
    [InlineData("abandon", "abandonnée")]
    public async Task La_fin_du_sous_processus_reprend_le_parent_dans_la_meme_transaction(string declencheur, string resultat)
    {
        var id = await _c.Moteur.Demarrer<Parent>(p => p.Demarrer(_aggregat));
        var enfant = _c.Enfant(id);
        Assert.Equal(StatutProcessus.AttenteEnfant, _c.Instance(id).Statut);
        Assert.Equal(Parent.EtapeSignature, _c.Instance(id).Etape);
        Assert.Equal(StatutProcessus.EnAttente, enfant.Statut);
        Assert.Equal(_c.Horloge.Maintenant.AddDays(10), enfant.Echeance);
        Assert.Equal(1, _c.Store.Commits);

        switch (declencheur)
        {
            case "signal":
                await _c.Moteur.Signaler<Signature>(_aggregat.ToString(), new SignatureRecue());
                break;
            case "delai":
                _c.Horloge.Avancer(TimeSpan.FromDays(10));
                Assert.Equal(1, await _c.Moteur.Tick());
                break;
            default:
                await _c.Moteur.Abandonner(enfant.Id);
                break;
        }

        Assert.Equal(2, _c.Store.Commits);
        Assert.Equal(StatutProcessus.Termine, _c.Instance(enfant.Id).Statut);
        Assert.Equal(StatutProcessus.Termine, _c.Instance(id).Statut);
        Assert.Equal(resultat, JsonSerializer.Deserialize<EtatParent>(_c.Instance(id).Etat!)!.Resultat);
    }

    [Fact]
    public async Task Un_sous_processus_peut_etre_demarre_seul()
    {
        var id = await _c.Moteur.Demarrer<Signature>(s => s.Demarrer(_aggregat));

        var instance = _c.Instance(id);
        Assert.Equal(StatutProcessus.EnAttente, instance.Statut);
        Assert.Equal(Signature.EtapeSignature, instance.Etape);
        Assert.Null(instance.ParentId);

        Assert.True(await _c.Moteur.Signaler(id, new SignatureRecue()));
        Assert.Equal(StatutProcessus.Termine, _c.Instance(id).Statut);
    }

    private sealed class EtatParent
    {
        public string? Resultat { get; set; }
    }

    private sealed class Parent : Processus<EtatParent>
    {
        public const string EtapeSignature = "Signature";

        public Task Demarrer(Guid aggregateId)
        {
            Appeler<Signature>(EtapeSignature, s => s.Demarrer(aggregateId));
            return Task.CompletedTask;
        }

        private void Handle(SousProcessusTermine e)
        {
            Etat.Resultat = e.Resultat;
            Terminer(e.Resultat);
        }
    }
}
