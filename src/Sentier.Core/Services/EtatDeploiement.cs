namespace Sentier;

/// <summary>Issue du déploiement d'une définition.</summary>
public enum EtatDeploiement
{
    /// <summary>Empreinte identique : rien n'a été créé.</summary>
    Inchangee,

    /// <summary>Nouvelle version enregistrée ; instances migrées automatiquement le cas échéant.</summary>
    NouvelleVersion,

    /// <summary>Nouvelle version enregistrée, mais des instances restent sur leur version (changement incompatible ou migration automatique désactivée).</summary>
    NouvelleVersionIncompatible,
}
