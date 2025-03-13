namespace Gabonet.Hubble.Extensions;

using Gabonet.Hubble.Models;
using Microsoft.AspNetCore.Http;
using System.Collections.Generic;

/// <summary>
/// Extensiones para HttpContext para manejar consultas a bases de datos.
/// </summary>
public static class HttpContextExtensions
{
    private const string DatabaseQueriesKey = "Gabonet.Hubble.DatabaseQueries";

    /// <summary>
    /// Agrega una consulta a la base de datos al contexto HTTP actual.
    /// </summary>
    /// <param name="context">Contexto HTTP</param>
    /// <param name="query">Consulta a la base de datos</param>
    public static void AddDatabaseQuery(this HttpContext context, DatabaseQueryLog query)
    {
        var queries = GetDatabaseQueries(context);
        queries.Add(query);
    }

    /// <summary>
    /// Obtiene todas las consultas a bases de datos registradas en el contexto HTTP actual.
    /// </summary>
    /// <param name="context">Contexto HTTP</param>
    /// <returns>Lista de consultas a bases de datos</returns>
    public static List<DatabaseQueryLog> GetDatabaseQueries(this HttpContext context)
    {
        if (!context.Items.ContainsKey(DatabaseQueriesKey))
        {
            context.Items[DatabaseQueriesKey] = new List<DatabaseQueryLog>();
        }

        return (List<DatabaseQueryLog>)context.Items[DatabaseQueriesKey];
    }

    /// <summary>
    /// Limpia todas las consultas a bases de datos registradas en el contexto HTTP actual.
    /// </summary>
    /// <param name="context">Contexto HTTP</param>
    public static void ClearDatabaseQueries(this HttpContext context)
    {
        if (context.Items.ContainsKey(DatabaseQueriesKey))
        {
            context.Items.Remove(DatabaseQueriesKey);
        }
    }
} 