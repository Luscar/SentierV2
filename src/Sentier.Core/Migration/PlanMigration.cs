using Sentier.Definitions;
using Sentier.Variables;

namespace Sentier.Migration;

/// <summary>Plan de migration compilé.</summary>
public sealed class PlanMigration
{
    internal PlanMigration()
    {
    }

    /// <summary>Règles par code de nœud source.</summary>
    public IReadOnlyDictionary<string, RegleNoeudMigration> Noeuds { get; internal set; } = new Dictionary<string, RegleNoeudMigration>();

    /// <summary>Renommages de variables (ancien nom → nouveau nom).</summary>
    public IReadOnlyDictionary<string, string> RenommagesVariables { get; internal set; } = new Dictionary<string, string>();

    /// <summary>Valeurs statiques pour les nouvelles variables.</summary>
    public IReadOnlyDictionary<string, ValeurTypee> ValeursVariables { get; internal set; } = new Dictionary<string, ValeurTypee>();

    /// <summary>Commandes dédiées qui calculent les nouvelles variables (moment <see cref="MomentCommande.Migration"/>).</summary>
    public IReadOnlyList<AppelCommande> CommandesVariables { get; internal set; } = [];
}
