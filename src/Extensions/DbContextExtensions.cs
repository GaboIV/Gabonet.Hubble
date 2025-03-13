namespace Gabonet.Hubble.Extensions;

using Gabonet.Hubble.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Extensiones para DbContext para capturar consultas SQL.
/// </summary>
public static class DbContextExtensions
{
    /// <summary>
    /// Configura un DbContext para capturar consultas SQL.
    /// </summary>
    /// <param name="optionsBuilder">Constructor de opciones de DbContext</param>
    /// <param name="httpContextAccessor">Acceso al contexto HTTP</param>
    /// <param name="databaseName">Nombre de la base de datos</param>
    /// <returns>Constructor de opciones configurado</returns>
    public static DbContextOptionsBuilder AddHubbleInterceptor(
        this DbContextOptionsBuilder optionsBuilder,
        IHttpContextAccessor httpContextAccessor,
        string databaseName)
    {
        return optionsBuilder.AddInterceptors(new HubbleDbCommandInterceptor(httpContextAccessor, databaseName));
    }
}

/// <summary>
/// Interceptor para capturar comandos SQL.
/// </summary>
public class HubbleDbCommandInterceptor : DbCommandInterceptor
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly string _databaseName;

    /// <summary>
    /// Constructor del interceptor.
    /// </summary>
    /// <param name="httpContextAccessor">Acceso al contexto HTTP</param>
    /// <param name="databaseName">Nombre de la base de datos</param>
    public HubbleDbCommandInterceptor(IHttpContextAccessor httpContextAccessor, string databaseName)
    {
        _httpContextAccessor = httpContextAccessor;
        _databaseName = databaseName;
    }

    /// <inheritdoc />
    public override InterceptionResult<DbDataReader> ReaderExecuting(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result)
    {
        CaptureCommand(command, "SELECT");
        return base.ReaderExecuting(command, eventData, result);
    }

    /// <inheritdoc />
    public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        CaptureCommand(command, "SELECT");
        return await base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
    }

    /// <inheritdoc />
    public override InterceptionResult<int> NonQueryExecuting(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<int> result)
    {
        string operationType = DetermineOperationType(command.CommandText);
        CaptureCommand(command, operationType);
        return base.NonQueryExecuting(command, eventData, result);
    }

    /// <inheritdoc />
    public override async ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        string operationType = DetermineOperationType(command.CommandText);
        CaptureCommand(command, operationType);
        return await base.NonQueryExecutingAsync(command, eventData, result, cancellationToken);
    }

    /// <inheritdoc />
    public override InterceptionResult<object> ScalarExecuting(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<object> result)
    {
        CaptureCommand(command, "SCALAR");
        return base.ScalarExecuting(command, eventData, result);
    }

    /// <inheritdoc />
    public override async ValueTask<InterceptionResult<object>> ScalarExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<object> result,
        CancellationToken cancellationToken = default)
    {
        CaptureCommand(command, "SCALAR");
        return await base.ScalarExecutingAsync(command, eventData, result, cancellationToken);
    }

    private void CaptureCommand(DbCommand command, string operationType)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext == null)
            return;

        var parameters = new Dictionary<string, object>();
        foreach (DbParameter parameter in command.Parameters)
        {
            parameters.Add(parameter.ParameterName, parameter.Value ?? DBNull.Value);
        }

        var tableName = ExtractTableName(command.CommandText, operationType);

        var query = new DatabaseQueryLog(
            databaseType: command.Connection?.GetType().Name ?? "Unknown",
            databaseName: _databaseName,
            query: command.CommandText,
            parameters: parameters,
            callerMethod: GetCallerMethod(),
            tableName: tableName,
            operationType: operationType
        );

        httpContext.AddDatabaseQuery(query);
    }

    private string DetermineOperationType(string commandText)
    {
        commandText = commandText.TrimStart().ToUpper();
        
        if (commandText.StartsWith("INSERT"))
            return "INSERT";
        if (commandText.StartsWith("UPDATE"))
            return "UPDATE";
        if (commandText.StartsWith("DELETE"))
            return "DELETE";
        if (commandText.StartsWith("CREATE"))
            return "CREATE";
        if (commandText.StartsWith("ALTER"))
            return "ALTER";
        if (commandText.StartsWith("DROP"))
            return "DROP";
        if (commandText.StartsWith("TRUNCATE"))
            return "TRUNCATE";
        if (commandText.StartsWith("EXEC") || commandText.StartsWith("EXECUTE"))
            return "EXECUTE";
            
        return "UNKNOWN";
    }

    private string ExtractTableName(string commandText, string operationType)
    {
        try
        {
            var words = commandText.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            
            switch (operationType)
            {
                case "SELECT":
                    // Buscar la palabra FROM y tomar la siguiente
                    for (int i = 0; i < words.Length - 1; i++)
                    {
                        if (words[i].Equals("FROM", StringComparison.OrdinalIgnoreCase))
                            return words[i + 1].Trim('[', ']', '`', '"', '\'');
                    }
                    break;
                    
                case "INSERT":
                    // Buscar la palabra INTO y tomar la siguiente
                    for (int i = 0; i < words.Length - 1; i++)
                    {
                        if (words[i].Equals("INTO", StringComparison.OrdinalIgnoreCase))
                            return words[i + 1].Trim('[', ']', '`', '"', '\'');
                    }
                    break;
                    
                case "UPDATE":
                    // La palabra después de UPDATE suele ser la tabla
                    if (words.Length > 1)
                        return words[1].Trim('[', ']', '`', '"', '\'');
                    break;
                    
                case "DELETE":
                    // Buscar la palabra FROM y tomar la siguiente
                    for (int i = 0; i < words.Length - 1; i++)
                    {
                        if (words[i].Equals("FROM", StringComparison.OrdinalIgnoreCase))
                            return words[i + 1].Trim('[', ']', '`', '"', '\'');
                    }
                    break;
            }
        }
        catch
        {
            // Si hay algún error al extraer el nombre de la tabla, simplemente devolvemos desconocido
        }
        
        return "Unknown";
    }

    private string GetCallerMethod()
    {
        try
        {
            var stackTrace = new System.Diagnostics.StackTrace();
            var frames = stackTrace.GetFrames();
            
            // Buscar el primer método que no sea parte de Entity Framework o este interceptor
            foreach (var frame in frames)
            {
                var method = frame.GetMethod();
                var declaringType = method?.DeclaringType;
                
                if (declaringType != null && 
                    !declaringType.FullName.StartsWith("Microsoft.EntityFrameworkCore") &&
                    !declaringType.FullName.StartsWith("Gabonet.Hubble"))
                {
                    return $"{declaringType.Name}.{method.Name}";
                }
            }
        }
        catch
        {
            // Si hay algún error al obtener el método llamador, simplemente devolvemos desconocido
        }
        
        return "Unknown";
    }
} 