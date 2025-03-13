namespace Gabonet.Hubble.Services;

using Gabonet.Hubble.Interfaces;
using Gabonet.Hubble.Models;
using Microsoft.AspNetCore.Http;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

/// <summary>
/// Implementación del servicio de Hubble para gestionar logs generales.
/// </summary>
public class HubbleService : IHubbleService
{
    private readonly IMongoCollection<GeneralLog> _logsCollection;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly string _serviceName;
    private readonly TimeZoneInfo _timeZone;

    /// <summary>
    /// Constructor del servicio de Hubble.
    /// </summary>
    /// <param name="mongoClient">Cliente de MongoDB</param>
    /// <param name="databaseName">Nombre de la base de datos</param>
    /// <param name="httpContextAccessor">Acceso al contexto HTTP</param>
    /// <param name="serviceName">Nombre del servicio</param>
    /// <param name="timeZoneId">ID de la zona horaria (opcional)</param>
    public HubbleService(
        IMongoClient mongoClient,
        string databaseName,
        IHttpContextAccessor httpContextAccessor,
        string serviceName = "HubbleService",
        string timeZoneId = null)
    {
        var mongoDatabase = mongoClient.GetDatabase(databaseName);
        _logsCollection = mongoDatabase.GetCollection<GeneralLog>("HubbleLogs");
        _httpContextAccessor = httpContextAccessor;
        _serviceName = serviceName;
        
        // Usar la zona horaria especificada o UTC por defecto
        _timeZone = string.IsNullOrEmpty(timeZoneId) 
            ? TimeZoneInfo.Utc 
            : TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
    }

    /// <inheritdoc />
    public async Task<List<GeneralLog>> GetAllLogsAsync()
    {
        return await _logsCollection.Find(_ => true).ToListAsync();
    }

    /// <inheritdoc />
    public async Task<GeneralLog> GetLogByIdAsync(string id)
    {
        return await _logsCollection.Find(log => log.Id == id).FirstOrDefaultAsync();
    }

    /// <inheritdoc />
    public async Task<List<GeneralLog>> GetFilteredLogsAsync(
        string? method = null, 
        string? url = null,
        int page = 1, 
        int pageSize = 50)
    {
        var filterBuilder = Builders<GeneralLog>.Filter;
        var filter = filterBuilder.Empty;

        // Filtrar por método si se proporciona
        if (!string.IsNullOrEmpty(method))
        {
            filter &= filterBuilder.Eq(log => log.Method, method);
        }

        // Filtrar por URL si se proporciona
        if (!string.IsNullOrEmpty(url))
        {
            var words = url.Split(' ');
            foreach (var word in words)
            {
                if (!string.IsNullOrWhiteSpace(word))
                {
                    filter &= filterBuilder.Regex(log => log.HttpUrl, new MongoDB.Bson.BsonRegularExpression(word, "i"));
                }
            }
        }

        // Ordenar los registros del más reciente al más antiguo
        var sort = Builders<GeneralLog>.Sort.Descending(log => log.Timestamp);

        // Paginación con Skip y Limit
        var pagedLogs = await _logsCollection
            .Find(filter)
            .Sort(sort)
            .Skip((page - 1) * pageSize)
            .Limit(pageSize)
            .ToListAsync();

        // Convertir las fechas a la zona horaria configurada
        foreach (var log in pagedLogs)
        {
            log.Timestamp = TimeZoneInfo.ConvertTimeFromUtc(log.Timestamp, _timeZone);
        }

        return pagedLogs;
    }

    /// <inheritdoc />
    public async Task CreateLogAsync(GeneralLog newLog)
    {
        await _logsCollection.InsertOneAsync(newLog);
    }

    /// <inheritdoc />
    public async Task UpdateLogAsync(string id, GeneralLog updatedLog)
    {
        await _logsCollection.ReplaceOneAsync(log => log.Id == id, updatedLog);
    }

    /// <inheritdoc />
    public async Task DeleteLogAsync(string id)
    {
        await _logsCollection.DeleteOneAsync(log => log.Id == id);
    }

    /// <inheritdoc />
    public async Task LogAsync(
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
    )
    {
        var logEntry = new GeneralLog
        {
            ServiceName = string.IsNullOrEmpty(serviceName) ? _serviceName : serviceName,
            ControllerName = title,
            ActionName = logType,
            HttpUrl = route,
            Method = method,
            RequestData = request,
            ResponseData = responseOrError,
            StatusCode = statusCode,
            IsError = isError,
            ErrorMessage = errorMessage,
            StackTrace = stackTrace,
            IpAddress = GetClientIpAddress(),
            Timestamp = DateTime.UtcNow,
            ExecutionTime = executionTime ?? 0
        };

        await _logsCollection.InsertOneAsync(logEntry);
    }

    /// <summary>
    /// Obtiene la dirección IP del cliente.
    /// </summary>
    /// <returns>Dirección IP del cliente</returns>
    private string GetClientIpAddress()
    {
        var ipAddress = _httpContextAccessor.HttpContext?.Connection?.RemoteIpAddress?.ToString();
        
        if (ipAddress == "::1" || ipAddress == "127.0.0.1")
        {
            ipAddress = "Localhost";
        }
        else if (string.IsNullOrEmpty(ipAddress))
        {
            ipAddress = "IP not available";
        }

        return ipAddress;
    }
} 