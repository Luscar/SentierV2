namespace ProcessManager.Tests.Exemple;

// Bus applicatifs simulés : le moteur ne les connaît pas, seuls les processus les utilisent.

public sealed class BusCommandesTest : IBusCommandes
{
    public List<object> Envoyees { get; } = [];

    public Task Envoyer(object commande)
    {
        Envoyees.Add(commande);
        return Task.CompletedTask;
    }
}

public sealed class BusQueriesTest : IBusQueries
{
    public Func<object, object> Repondre { get; set; } = _ => throw new InvalidOperationException("Aucune réponse configurée.");

    public Task<TResultat> Demander<TResultat>(object query) => Task.FromResult((TResultat)Repondre(query));
}

// Commandes et queries de l'application.
public sealed record IdentifierDeclaration(Guid AggregateId);
public sealed record ValiderDeclaration(Guid AggregateId);
public sealed record ComptabiliserDeclaration(Guid AggregateId);
public sealed record DemanderAutorisationRejet(Guid AggregateId);
public sealed record Relancer(Guid AggregateId, string Etape, int Relance);
public sealed record DemanderSignature(Guid AggregateId);

public sealed record VerifierIdentification(Guid AggregateId);
public sealed record VerifierValidation(Guid AggregateId);
public sealed record VerifierComptabilisation(Guid AggregateId);
public sealed record ObtenirDateComptabilisation(Guid AggregateId);
public sealed record Verdict(bool Accepte);
