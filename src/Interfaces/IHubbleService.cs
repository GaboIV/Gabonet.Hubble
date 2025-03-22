namespace Gabonet.Hubble.Interfaces;

using Gabonet.Hubble.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

/// <summary>
/// Interfaz para el servicio de Hubble que maneja los logs generales y consultas a bases de datos.
/// </summary>
public interface IHubbleService
{
    /// <summary>
    /// Obtiene todos los logs generales.
    /// </summary>
    /// <returns>Lista de logs generales</returns>
    Task<List<GeneralLog>> GetAllLogsAsync();

    /// <summary>
    /// Obtiene un log general por su ID.
    /// </summary>
    /// <param name="id">ID del log</param>
    /// <returns>Log general encontrado o null</returns>
    Task<GeneralLog> GetLogByIdAsync(string id);

    /// <summary>
    /// Obtiene logs filtrados por método HTTP y/o URL.
    /// </summary>
    /// <param name="method">Método HTTP (opcional)</param>
    /// <param name="url">URL (opcional)</param>
    /// <param name="page">Número de página</param>
    /// <param name="pageSize">Tamaño de página</param>
    /// <returns>Lista de logs filtrados</returns>
    Task<List<GeneralLog>> GetFilteredLogsAsync(
        string? method = null, 
        string? url = null,
        int page = 1, 
        int pageSize = 50);

    /// <summary>
    /// Crea un nuevo log general.
    /// </summary>
    /// <param name="newLog">Nuevo log general</param>
    Task CreateLogAsync(GeneralLog newLog);

    /// <summary>
    /// Actualiza un log general existente.
    /// </summary>
    /// <param name="id">ID del log</param>
    /// <param name="updatedLog">Log actualizado</param>
    Task UpdateLogAsync(string id, GeneralLog updatedLog);

    /// <summary>
    /// Elimina un log general.
    /// </summary>
    /// <param name="id">ID del log</param>
    Task DeleteLogAsync(string id);

    /// <summary>
    /// Elimina todos los logs generales.
    /// </summary>
    Task DeleteAllLogsAsync();

    /// <summary>
    /// Registra un log general con información detallada.
    /// </summary>
    /// <param name="title">Título del log</param>
    /// <param name="logType">Tipo de log</param>
    /// <param name="serviceName">Nombre del servicio</param>
    /// <param name="responseOrError">Respuesta o error</param>
    /// <param name="statusCode">Código de estado HTTP</param>
    /// <param name="route">Ruta de la solicitud</param>
    /// <param name="method">Método HTTP</param>
    /// <param name="request">Datos de la solicitud</param>
    /// <param name="isError">Indica si es un error</param>
    /// <param name="errorMessage">Mensaje de error</param>
    /// <param name="stackTrace">Traza de la pila</param>
    /// <param name="errorDetails">Detalles adicionales del error</param>
    /// <param name="executionTime">Tiempo de ejecución en milisegundos</param>
    Task LogAsync(
        string title,
        string logType,
        string serviceName,
        string responseOrError,
        int statusCode,
        string route,
        string method,
        string? request,
        bool isError,
        string? errorMessage = null,
        string? stackTrace = null,
        string? errorDetails = null,
        long? executionTime = null
    );
} 