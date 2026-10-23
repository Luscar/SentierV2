using System.Globalization;
using Sentier.Definitions;
using Sentier.Persistance;
using Sentier.Variables;

namespace Sentier.Execution;

/// <summary>Création des lignes d'attente (une par déclencheur) d'un nœud d'attente.</summary>
internal sealed class PlanificateurAttentes(IDepotAttentes depot, IHorloge horloge)
{
    public async Task CreerAsync(InstanceProcessus instance, NoeudAttente noeud, IVariablesLecture variables, IEnumerable<Declencheur> declencheurs, CancellationToken ct)
    {
        var maintenant = horloge.MaintenantUtc;
        foreach (var declencheur in declencheurs)
        {
            var attente = new Attente
            {
                IdInstance = instance.Id,
                CodeNoeud = noeud.Code,
                TypeDeclencheur = declencheur.Type,
                NomSortie = declencheur.NomSortie,
                DateCreation = maintenant,
            };

            switch (declencheur)
            {
                case DeclencheurEcheance echeance:
                    attente.DateEcheance = CalculerEcheance(echeance, maintenant, variables, noeud.Code);
                    break;
                case DeclencheurSignal signal:
                    attente.NomSignal = signal.NomSignal;
                    attente.CleCorrelation = CalculerCorrelation(signal, variables);
                    break;
            }

            await depot.InsererAsync(attente, ct);
        }
    }

    private static DateTime CalculerEcheance(DeclencheurEcheance echeance, DateTime maintenant, IVariablesLecture variables, string codeNoeud)
    {
        if (echeance.DateAbsolue is { } date)
        {
            return DateTime.SpecifyKind(date, DateTimeKind.Utc);
        }

        if (echeance.Duree is { } duree)
        {
            return maintenant + duree;
        }

        return variables.Lire(echeance.Variable!) is DateTime valeur
            ? valeur
            : throw new VariableInvalideException($"L'échéance de l'attente '{codeNoeud}' lit la variable '{echeance.Variable}', qui n'a pas de valeur.");
    }

    private static string? CalculerCorrelation(DeclencheurSignal signal, IVariablesLecture variables)
    {
        if (signal.VariableCorrelation is null)
        {
            return signal.CleCorrelation;
        }

        return variables.Lire(signal.VariableCorrelation) switch
        {
            null => throw new VariableInvalideException($"La clé de corrélation du signal '{signal.NomSignal}' lit la variable '{signal.VariableCorrelation}', qui n'a pas de valeur."),
            DateTime date => date.ToString("O", CultureInfo.InvariantCulture),
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            var valeur => valeur.ToString(),
        };
    }
}
