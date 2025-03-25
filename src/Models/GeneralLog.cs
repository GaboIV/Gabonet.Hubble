namespace Gabonet.Hubble.Models;

using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;
using System.Collections.Generic;

/// <summary>
/// Modelo que representa un registro de log general para solicitudes HTTP y operaciones del sistema.
/// </summary>
public class GeneralLog
{
    /// <summary>
    /// Identificador único del log
    /// </summary>
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    /// <summary>
    /// Marca de tiempo cuando se registró el log
    /// </summary>
    [BsonElement("timestamp")]
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// URL de la solicitud HTTP
    /// </summary>
    [BsonElement("httpUrl")]
    public string HttpUrl { get; set; } = string.Empty;

    /// <summary>
    /// Nombre del controlador que procesó la solicitud
    /// </summary>
    [BsonElement("controllerName")]
    public string ControllerName { get; set; } = string.Empty;

    /// <summary>
    /// Nombre de la acción que procesó la solicitud
    /// </summary>
    [BsonElement("actionName")]
    public string ActionName { get; set; } = string.Empty;

    /// <summary>
    /// Método HTTP utilizado (GET, POST, PUT, DELETE, etc.)
    /// </summary>
    [BsonElement("method")]
    public string Method { get; set; } = string.Empty;

    /// <summary>
    /// Cabeceras de la solicitud (serializadas como JSON)
    /// </summary>
    [BsonElement("requestHeaders")]
    public string? RequestHeaders { get; set; }

    /// <summary>
    /// Datos de la solicitud (cuerpo)
    /// </summary>
    [BsonElement("requestData")]
    public string? RequestData { get; set; }

    /// <summary>
    /// Datos de la respuesta
    /// </summary>
    [BsonElement("responseData")]
    public string? ResponseData { get; set; }

    /// <summary>
    /// Código de estado HTTP de la respuesta
    /// </summary>
    [BsonElement("statusCode")]
    public int StatusCode { get; set; }

    /// <summary>
    /// Indica si ocurrió un error durante el procesamiento
    /// </summary>
    [BsonElement("isError")]
    public bool IsError { get; set; }

    /// <summary>
    /// Mensaje de error si ocurrió alguno
    /// </summary>
    [BsonElement("errorMessage")]
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Traza de la pila si ocurrió un error
    /// </summary>
    [BsonElement("stackTrace")]
    public string? StackTrace { get; set; }

    /// <summary>
    /// Nombre del servicio que generó el log
    /// </summary>
    [BsonElement("serviceName")]
    public string ServiceName { get; set; } = string.Empty;

    /// <summary>
    /// Dirección IP del cliente
    /// </summary>
    [BsonElement("ipAddress")]
    public string IpAddress { get; set; } = string.Empty;

    /// <summary>
    /// Tiempo de ejecución en milisegundos
    /// </summary>
    [BsonElement("executionTime")]
    public long ExecutionTime { get; set; }

    /// <summary>
    /// Lista de consultas a bases de datos realizadas durante el procesamiento
    /// </summary>
    [BsonElement("databaseQueries")]
    public List<DatabaseQuery> DatabaseQueries { get; set; } = new List<DatabaseQuery>();

    /// <summary>
    /// ID de la solicitud HTTP relacionada (para logs de ILogger asociados a una solicitud).
    /// </summary>
    [BsonElement("relatedRequestId")]
    public string? RelatedRequestId { get; set; }
} 