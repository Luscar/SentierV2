namespace Sentier;

/// <summary>Type d'une variable de processus.</summary>
public enum TypeVariable
{
    /// <summary>Chaîne de caractères.</summary>
    Texte,

    /// <summary>Entier 64 bits (<see cref="long"/>).</summary>
    Entier,

    /// <summary>Nombre décimal (<see cref="decimal"/>).</summary>
    Decimal,

    /// <summary>Booléen.</summary>
    Booleen,

    /// <summary>Date et heure UTC (<see cref="DateTime"/>).</summary>
    Date,

    /// <summary>Objet JSON libre, sans garantie de type.</summary>
    Json,
}
