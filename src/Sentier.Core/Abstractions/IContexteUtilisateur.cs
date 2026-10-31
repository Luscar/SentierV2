namespace Sentier;

/// <summary>Optionnel : fournit l'utilisateur courant, pour l'audit.</summary>
public interface IContexteUtilisateur
{
    /// <summary>Identifiant de l'utilisateur courant, ou <c>null</c>.</summary>
    string? Utilisateur { get; }
}
