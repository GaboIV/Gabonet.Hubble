namespace Gabonet.Hubble.Extensions;

using Gabonet.Hubble.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Data;
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
        optionsBuilder.AddInterceptors(new HubbleDbCommandInterceptor(httpContextAccessor, databaseName));
        optionsBuilder.AddInterceptors(new HubbleDbConnectionInterceptor(httpContextAccessor, databaseName));
        return optionsBuilder;
    }

    /// <summary>
    /// Captura una consulta SQL utilizando ADO.NET directamente (sin pasar por EF Core).
    /// </summary>
    /// <param name="command">Comando SQL a ejecutar</param>
    /// <param name="httpContextAccessor">Acceso al contexto HTTP</param>
    /// <param name="databaseName">Nombre de la base de datos</param>
    public static void CaptureAdoNetCommand(
        this DbCommand command,
        IHttpContextAccessor httpContextAccessor,
        string databaseName)
    {
        if (httpContextAccessor?.HttpContext == null)
            return;

        var parameters = new Dictionary<string, object>();
        foreach (DbParameter parameter in command.Parameters)
        {
            parameters.Add(parameter.ParameterName, parameter.Value ?? DBNull.Value);
        }

        string operationType = "UNKNOWN";
        if (command.CommandType == CommandType.StoredProcedure)
        {
            operationType = "STORED_PROCEDURE";
        }
        else if (command.CommandType == CommandType.Text)
        {
            // Determinar el tipo de operación basado en el texto del comando
            operationType = DetermineOperationType(command.CommandText);
        }

        var query = new DatabaseQueryLog(
            databaseType: command.Connection?.GetType().Name ?? "Unknown",
            databaseName: databaseName,
            query: command.CommandText,
            parameters: parameters,
            callerMethod: GetCallerMethod(),
            tableName: ExtractTableName(command.CommandText, operationType),
            operationType: operationType
        );

        httpContextAccessor.HttpContext.AddDatabaseQuery(query);
    }

    private static string DetermineOperationType(string commandText)
    {
        if (string.IsNullOrWhiteSpace(commandText))
            return "UNKNOWN";

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
        if (commandText.StartsWith("SP_") || commandText.StartsWith("[SP_"))
            return "STORED_PROCEDURE";
            
        return "UNKNOWN";
    }

    private static string ExtractTableName(string commandText, string operationType)
    {
        try
        {
            // Si es un procedimiento almacenado, tomar el nombre del procedimiento
            if (operationType == "STORED_PROCEDURE" || operationType == "EXECUTE")
            {
                var procName = commandText.Split(new[] { ' ', '\t', '\r', '\n', '[', ']', '.', '(', ')' }, 
                    StringSplitOptions.RemoveEmptyEntries).LastOrDefault();
                return procName ?? "Unknown";
            }

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

    private static string GetCallerMethod()
    {
        try
        {
            var stackTrace = new System.Diagnostics.StackTrace();
            var frames = stackTrace.GetFrames();
            
            // Buscar el primer método que no sea parte de Entity Framework, este interceptor o System.Data
            foreach (var frame in frames)
            {
                var method = frame.GetMethod();
                var declaringType = method?.DeclaringType;
                var fullName = declaringType?.FullName;
                
                if (declaringType != null && fullName != null && 
                    !fullName.StartsWith("Microsoft.EntityFrameworkCore") &&
                    !fullName.StartsWith("Gabonet.Hubble") &&
                    !fullName.StartsWith("System.Data"))
                {
                    return $"{declaringType.Name ?? "Unknown"}.{method?.Name ?? "Unknown"}";
                }
            }
        }
        catch
        {
            // Si hay algún error al obtener el método llamador, simplemente devolvemos desconocido
        }
        
        return "Unknown";
    }

    /// <summary>
    /// Extiende el contexto para capturar todos los comandos ADO.NET ejecutados con la conexión obtenida del contexto.
    /// </summary>
    /// <param name="context">Contexto de Entity Framework</param>
    /// <param name="httpContextAccessor">Acceso al contexto HTTP</param>
    /// <param name="databaseName">Nombre de la base de datos</param>
    /// <returns>Una conexión de ADO.NET que captura automáticamente sus comandos</returns>
    public static DbConnection GetTrackedConnection(
        this DbContext context,
        IHttpContextAccessor httpContextAccessor,
        string databaseName)
    {
        var connection = context.Database.GetDbConnection();
        
        // Decoramos la conexión con un proxy que captura todos los comandos
        return new HubbleDbConnectionProxy(connection, httpContextAccessor, databaseName);
    }
}

/// <summary>
/// Interceptor para capturar conexiones y operaciones directas con ADO.NET.
/// </summary>
public class HubbleDbConnectionInterceptor : DbConnectionInterceptor
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly string _databaseName;

    /// <summary>
    /// Constructor del interceptor de conexiones.
    /// </summary>
    /// <param name="httpContextAccessor">Acceso al contexto HTTP</param>
    /// <param name="databaseName">Nombre de la base de datos</param>
    public HubbleDbConnectionInterceptor(IHttpContextAccessor httpContextAccessor, string databaseName)
    {
        _httpContextAccessor = httpContextAccessor;
        _databaseName = databaseName;
    }

    /// <inheritdoc />
    public override InterceptionResult ConnectionOpening(
        DbConnection connection,
        ConnectionEventData eventData,
        InterceptionResult result)
    {
        // Registramos la apertura de la conexión
        CaptureConnectionEvent(connection, "OPEN_CONNECTION");
        return base.ConnectionOpening(connection, eventData, result);
    }

    /// <inheritdoc />
    public override async ValueTask<InterceptionResult> ConnectionOpeningAsync(
        DbConnection connection,
        ConnectionEventData eventData,
        InterceptionResult result,
        CancellationToken cancellationToken = default)
    {
        // Registramos la apertura asíncrona de la conexión
        CaptureConnectionEvent(connection, "OPEN_CONNECTION_ASYNC");
        return await base.ConnectionOpeningAsync(connection, eventData, result, cancellationToken);
    }

    /// <inheritdoc />
    public override void ConnectionClosed(DbConnection connection, ConnectionEndEventData eventData)
    {
        // Registramos el cierre de la conexión
        CaptureConnectionEvent(connection, "CLOSE_CONNECTION");
        base.ConnectionClosed(connection, eventData);
    }

    /// <inheritdoc />
    public override Task ConnectionClosedAsync(
        DbConnection connection,
        ConnectionEndEventData eventData)
    {
        // Registramos el cierre asíncrono de la conexión
        CaptureConnectionEvent(connection, "CLOSE_CONNECTION_ASYNC");
        return base.ConnectionClosedAsync(connection, eventData);
    }

    private void CaptureConnectionEvent(DbConnection connection, string operationType)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext == null)
            return;

        // Creamos un log para la operación de conexión
        var query = new DatabaseQueryLog(
            databaseType: connection.GetType().Name,
            databaseName: _databaseName,
            query: $"Connection {operationType} - {connection.ConnectionString.Split(';').FirstOrDefault() ?? ""}",
            parameters: null,
            callerMethod: GetCallerMethod(),
            tableName: "N/A",
            operationType: operationType
        );

        httpContext.AddDatabaseQuery(query);
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
                var fullName = declaringType?.FullName;
                
                if (declaringType != null && fullName != null && 
                    !fullName.StartsWith("Microsoft.EntityFrameworkCore") &&
                    !fullName.StartsWith("Gabonet.Hubble") &&
                    !fullName.StartsWith("System.Data"))
                {
                    return $"{declaringType.Name ?? "Unknown"}.{method?.Name ?? "Unknown"}";
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
                var fullName = declaringType?.FullName;
                
                if (declaringType != null && fullName != null && 
                    !fullName.StartsWith("Microsoft.EntityFrameworkCore") &&
                    !fullName.StartsWith("Gabonet.Hubble"))
                {
                    return $"{declaringType.Name ?? "Unknown"}.{method?.Name ?? "Unknown"}";
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

/// <summary>
/// Clase proxy que envuelve una conexión de base de datos y captura todos los comandos creados.
/// </summary>
public class HubbleDbConnectionProxy : DbConnection
{
    private readonly DbConnection _innerConnection;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly string _databaseName;
    
    /// <summary>
    /// Constructor del proxy.
    /// </summary>
    /// <param name="innerConnection">Conexión real que se envuelve</param>
    /// <param name="httpContextAccessor">Acceso al contexto HTTP</param>
    /// <param name="databaseName">Nombre de la base de datos</param>
    public HubbleDbConnectionProxy(
        DbConnection innerConnection,
        IHttpContextAccessor httpContextAccessor,
        string databaseName)
    {
        _innerConnection = innerConnection;
        _httpContextAccessor = httpContextAccessor;
        _databaseName = databaseName;
    }

    /// <inheritdoc />
    public override string ConnectionString 
    { 
        get => _innerConnection.ConnectionString; 
        set => _innerConnection.ConnectionString = value; 
    }

    /// <inheritdoc />
    public override string Database => _innerConnection.Database;

    /// <inheritdoc />
    public override string DataSource => _innerConnection.DataSource;

    /// <inheritdoc />
    public override string ServerVersion => _innerConnection.ServerVersion;

    /// <inheritdoc />
    public override ConnectionState State => _innerConnection.State;

    /// <inheritdoc />
    public override void ChangeDatabase(string databaseName) => _innerConnection.ChangeDatabase(databaseName);

    /// <inheritdoc />
    public override void Close() => _innerConnection.Close();

    /// <inheritdoc />
    public override void Open() => _innerConnection.Open();

    /// <inheritdoc />
    public override Task OpenAsync(CancellationToken cancellationToken) => _innerConnection.OpenAsync(cancellationToken);

    /// <inheritdoc />
    protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel) => _innerConnection.BeginTransaction(isolationLevel);

    /// <inheritdoc />
    protected override DbCommand CreateDbCommand()
    {
        var command = _innerConnection.CreateCommand();
        return new HubbleDbCommandProxy(command, _httpContextAccessor, _databaseName);
    }
}

/// <summary>
/// Clase proxy que envuelve un comando de base de datos y captura su ejecución.
/// </summary>
public class HubbleDbCommandProxy : DbCommand
{
    private readonly DbCommand _innerCommand;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly string _databaseName;
    
    /// <summary>
    /// Constructor del proxy.
    /// </summary>
    /// <param name="innerCommand">Comando real que se envuelve</param>
    /// <param name="httpContextAccessor">Acceso al contexto HTTP</param>
    /// <param name="databaseName">Nombre de la base de datos</param>
    public HubbleDbCommandProxy(
        DbCommand innerCommand,
        IHttpContextAccessor httpContextAccessor,
        string databaseName)
    {
        _innerCommand = innerCommand;
        _httpContextAccessor = httpContextAccessor;
        _databaseName = databaseName;
    }

    /// <inheritdoc />
    public override string CommandText 
    { 
        get => _innerCommand.CommandText; 
        set => _innerCommand.CommandText = value; 
    }

    /// <inheritdoc />
    public override int CommandTimeout 
    { 
        get => _innerCommand.CommandTimeout; 
        set => _innerCommand.CommandTimeout = value; 
    }

    /// <inheritdoc />
    public override CommandType CommandType 
    { 
        get => _innerCommand.CommandType; 
        set => _innerCommand.CommandType = value; 
    }

    /// <inheritdoc />
    public override UpdateRowSource UpdatedRowSource 
    { 
        get => _innerCommand.UpdatedRowSource; 
        set => _innerCommand.UpdatedRowSource = value; 
    }

    /// <inheritdoc />
    public override bool DesignTimeVisible 
    { 
        get => _innerCommand.DesignTimeVisible; 
        set => _innerCommand.DesignTimeVisible = value; 
    }

    /// <inheritdoc />
    protected override DbConnection DbConnection 
    { 
        get => _innerCommand.Connection; 
        set => _innerCommand.Connection = value; 
    }

    /// <inheritdoc />
    protected override DbTransaction DbTransaction 
    { 
        get => _innerCommand.Transaction; 
        set => _innerCommand.Transaction = value; 
    }

    /// <inheritdoc />
    protected override DbParameterCollection DbParameterCollection => _innerCommand.Parameters;

    /// <inheritdoc />
    public override void Cancel() => _innerCommand.Cancel();

    /// <inheritdoc />
    public override void Prepare() => _innerCommand.Prepare();

    /// <inheritdoc />
    protected override DbParameter CreateDbParameter() => _innerCommand.CreateParameter();

    /// <inheritdoc />
    public override int ExecuteNonQuery() 
    {
        _innerCommand.CaptureAdoNetCommand(_httpContextAccessor, _databaseName);
        return _innerCommand.ExecuteNonQuery();
    }

    /// <inheritdoc />
    public override object ExecuteScalar() 
    {
        _innerCommand.CaptureAdoNetCommand(_httpContextAccessor, _databaseName);
        return _innerCommand.ExecuteScalar();
    }

    /// <inheritdoc />
    protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior) 
    {
        _innerCommand.CaptureAdoNetCommand(_httpContextAccessor, _databaseName);
        return _innerCommand.ExecuteReader(behavior);
    }

    /// <inheritdoc />
    protected override async Task<DbDataReader> ExecuteDbDataReaderAsync(CommandBehavior behavior, CancellationToken cancellationToken) 
    {
        _innerCommand.CaptureAdoNetCommand(_httpContextAccessor, _databaseName);
        return await _innerCommand.ExecuteReaderAsync(behavior, cancellationToken);
    }

    /// <inheritdoc />
    public override async Task<int> ExecuteNonQueryAsync(CancellationToken cancellationToken) 
    {
        _innerCommand.CaptureAdoNetCommand(_httpContextAccessor, _databaseName);
        return await _innerCommand.ExecuteNonQueryAsync(cancellationToken);
    }

    /// <inheritdoc />
    public override async Task<object> ExecuteScalarAsync(CancellationToken cancellationToken) 
    {
        _innerCommand.CaptureAdoNetCommand(_httpContextAccessor, _databaseName);
        return await _innerCommand.ExecuteScalarAsync(cancellationToken);
    }
} 