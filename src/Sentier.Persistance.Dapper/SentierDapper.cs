using System.Data;
using Dapper;

namespace Sentier.Persistance.Dapper;

/// <summary>Configuration de Dapper pour Sentier.</summary>
public static class SentierDapper
{
    private static int _enregistre;

    /// <summary>
    /// Enregistre les gestionnaires de types des identifiants typés (<see cref="IdInstance"/>…),
    /// pour que le client puisse les utiliser dans ses propres requêtes Dapper. Idempotent.
    /// </summary>
    public static void EnregistrerGestionnairesTypes()
    {
        if (Interlocked.Exchange(ref _enregistre, 1) == 1)
        {
            return;
        }

        SqlMapper.AddTypeHandler(new GestionnaireIdentifiant<IdDefinition>());
        SqlMapper.AddTypeHandler(new GestionnaireIdentifiant<IdInstance>());
        SqlMapper.AddTypeHandler(new GestionnaireIdentifiant<IdPassageNoeud>());
        SqlMapper.AddTypeHandler(new GestionnaireIdentifiant<IdAttente>());
        SqlMapper.AddTypeHandler(new GestionnaireIdentifiant<IdTache>());
        SqlMapper.AddTypeHandler(new GestionnaireIdentifiant<IdMigration>());
    }

    private sealed class GestionnaireIdentifiant<T> : SqlMapper.TypeHandler<T> where T : struct, IIdentifiant<T>
    {
        public override void SetValue(IDbDataParameter parameter, T value)
        {
            parameter.DbType = DbType.Int64;
            parameter.Value = value.Valeur;
        }

        public override T Parse(object value) => T.Depuis(Convert.ToInt64(value));
    }
}
