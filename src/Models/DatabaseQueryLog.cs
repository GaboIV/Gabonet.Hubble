namespace Gabonet.Hubble.Models;

using System;
using System.Diagnostics;
using Newtonsoft.Json;

/// <summary>
/// Clase para capturar y registrar consultas a bases de datos en tiempo real.
/// </summary>
public class DatabaseQueryLog
{
    /// <summary>
    /// Cronómetro para medir el tiempo de ejecución de la consulta
    /// </summary>
    private readonly Stopwatch _stopwatch;

    /// <summary>
    /// Tipo de base de datos
    /// </summary>
    public string DatabaseType { get; set; }

    /// <summary>
    /// Nombre de la base de datos
    /// </summary>
    public string DatabaseName { get; set; }

    /// <summary>
    /// Consulta SQL o comando
    /// </summary>
    public string Query { get; set; }

    /// <summary>
    /// Parámetros de la consulta
    /// </summary>
    public object? Parameters { get; set; }

    /// <summary>
    /// Marca de tiempo de inicio
    /// </summary>
    public DateTime StartTime { get; private set; }

    /// <summary>
    /// Nombre del método o servicio que ejecutó la consulta
    /// </summary>
    public string? CallerMethod { get; set; }

    /// <summary>
    /// Nombre de la tabla o colección principal afectada
    /// </summary>
    public string? TableName { get; set; }

    /// <summary>
    /// Tipo de operación (SELECT, INSERT, UPDATE, DELETE, etc.)
    /// </summary>
    public string? OperationType { get; set; }

    /// <summary>
    /// Constructor para iniciar el registro de una consulta
    /// </summary>
    /// <param name="databaseType">Tipo de base de datos</param>
    /// <param name="databaseName">Nombre de la base de datos</param>
    /// <param name="query">Consulta SQL o comando</param>
    /// <param name="parameters">Parámetros de la consulta</param>
    /// <param name="callerMethod">Método que ejecuta la consulta</param>
    /// <param name="tableName">Nombre de la tabla</param>
    /// <param name="operationType">Tipo de operación</param>
    public DatabaseQueryLog(
        string databaseType,
        string databaseName,
        string query,
        object? parameters = null,
        string? callerMethod = null,
        string? tableName = null,
        string? operationType = null)
    {
        DatabaseType = databaseType;
        DatabaseName = databaseName;
        Query = query;
        Parameters = parameters;
        CallerMethod = callerMethod;
        TableName = tableName;
        OperationType = operationType;
        StartTime = DateTime.UtcNow;
        _stopwatch = Stopwatch.StartNew();
    }

    /// <summary>
    /// Convierte el log de consulta a un objeto DatabaseQuery para almacenamiento
    /// </summary>
    /// <param name="rowCount">Número de filas afectadas</param>
    /// <param name="isSuccess">Indica si la consulta fue exitosa</param>
    /// <param name="errorMessage">Mensaje de error si la consulta falló</param>
    /// <param name="additionalInfo">Información adicional</param>
    /// <returns>Objeto DatabaseQuery para almacenamiento</returns>
    public DatabaseQuery ToDatabaseQuery(
        int? rowCount = null,
        bool isSuccess = true,
        string? errorMessage = null,
        string? additionalInfo = null)
    {
        _stopwatch.Stop();
        
        return new DatabaseQuery
        {
            DatabaseType = this.DatabaseType,
            DatabaseName = this.DatabaseName,
            Query = this.Query,
            Parameters = this.Parameters != null ? JsonConvert.SerializeObject(this.Parameters) : null,
            ExecutionTime = _stopwatch.ElapsedMilliseconds,
            Timestamp = this.StartTime,
            RowCount = rowCount,
            IsSuccess = isSuccess,
            ErrorMessage = errorMessage,
            CallerMethod = this.CallerMethod,
            TableName = this.TableName,
            OperationType = this.OperationType,
            AdditionalInfo = additionalInfo
        };
    }
} 