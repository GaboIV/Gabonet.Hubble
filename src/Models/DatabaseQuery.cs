namespace Gabonet.Hubble.Models;

using MongoDB.Bson.Serialization.Attributes;
using System;

/// <summary>
/// Entidad que captura información relevante sobre consultas a bases de datos.
/// </summary>
public class DatabaseQuery
{
    /// <summary>
    /// Tipo de base de datos (SQL Server, Oracle, MongoDB, etc.)
    /// </summary>
    [BsonElement("databaseType")]
    public string DatabaseType { get; set; } = string.Empty;

    /// <summary>
    /// Nombre de la base de datos
    /// </summary>
    [BsonElement("databaseName")]
    public string DatabaseName { get; set; } = string.Empty;

    /// <summary>
    /// Consulta SQL o comando ejecutado
    /// </summary>
    [BsonElement("query")]
    public string Query { get; set; } = string.Empty;

    /// <summary>
    /// Parámetros utilizados en la consulta (serializados como JSON)
    /// </summary>
    [BsonElement("parameters")]
    public string? Parameters { get; set; }

    /// <summary>
    /// Tiempo de ejecución de la consulta en milisegundos
    /// </summary>
    [BsonElement("executionTime")]
    public long ExecutionTime { get; set; }

    /// <summary>
    /// Marca de tiempo cuando se inició la consulta
    /// </summary>
    [BsonElement("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Número de filas afectadas o devueltas por la consulta
    /// </summary>
    [BsonElement("rowCount")]
    public int? RowCount { get; set; }

    /// <summary>
    /// Indica si la consulta fue exitosa
    /// </summary>
    [BsonElement("isSuccess")]
    public bool IsSuccess { get; set; } = true;

    /// <summary>
    /// Mensaje de error si la consulta falló
    /// </summary>
    [BsonElement("errorMessage")]
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Nombre del método o servicio que ejecutó la consulta
    /// </summary>
    [BsonElement("callerMethod")]
    public string? CallerMethod { get; set; }

    /// <summary>
    /// Nombre de la tabla o colección principal afectada
    /// </summary>
    [BsonElement("tableName")]
    public string? TableName { get; set; }

    /// <summary>
    /// Tipo de operación (SELECT, INSERT, UPDATE, DELETE, etc.)
    /// </summary>
    [BsonElement("operationType")]
    public string? OperationType { get; set; }

    /// <summary>
    /// Información adicional sobre la consulta
    /// </summary>
    [BsonElement("additionalInfo")]
    public string? AdditionalInfo { get; set; }
} 