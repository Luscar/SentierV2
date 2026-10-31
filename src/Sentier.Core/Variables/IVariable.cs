namespace Sentier.Variables;

/// <summary>Variable typée non générique, telle que manipulée par le constructeur.</summary>
public interface IVariable
{
    /// <summary>Nom de la variable.</summary>
    string Nom { get; }

    /// <summary>Type déclaré.</summary>
    TypeVariable Type { get; }

    /// <summary>Retourne la déclaration sérialisable de la variable.</summary>
    DeclarationVariable VersDeclaration();
}
