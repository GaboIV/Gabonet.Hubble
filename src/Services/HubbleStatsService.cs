namespace Gabonet.Hubble.Services;

using Gabonet.Hubble.Interfaces;
using Gabonet.Hubble.Models;
using Gabonet.Hubble.Middleware;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using System;
using System.Linq;
using System.Threading.Tasks;

/// <summary>
/// Implementación del servicio de estadísticas de Hubble
/// </summary>
public class HubbleStatsService : IHubbleStatsService
{
    private readonly IMongoCollection<HubbleStatistics> _statsCollection;
    private readonly IMongoCollection<HubbleSystemConfiguration> _configCollection;
    private readonly IMongoCollection<GeneralLog> _logsCollection;
    private readonly HubbleOptions _options;
    private readonly ILogger<HubbleStatsService> _logger;
    private readonly IMongoClient _mongoClient;
    private readonly string _databaseName;

    /// <summary>
    /// Constructor del servicio de estadísticas
    /// </summary>
    /// <param name="mongoClient">Cliente de MongoDB</param>
    /// <param name="databaseName">Nombre de la base de datos</param>
    /// <param name="options">Opciones de configuración de Hubble</param>
    /// <param name="logger">Logger</param>
    public HubbleStatsService(
        IMongoClient mongoClient,
        string databaseName,
        HubbleOptions options,
        ILogger<HubbleStatsService> logger)
    {
        _mongoClient = mongoClient;
        _databaseName = databaseName;
        var mongoDatabase = mongoClient.GetDatabase(databaseName);
        
        _statsCollection = mongoDatabase.GetCollection<HubbleStatistics>("HubbleStats");
        _configCollection = mongoDatabase.GetCollection<HubbleSystemConfiguration>("HubbleConfig");
        _logsCollection = mongoDatabase.GetCollection<GeneralLog>("HubbleLogs");
        
        _options = options;
        _logger = logger;
        
        // Asegurar que exista la configuración inicial
        EnsureInitialConfigurationAsync().GetAwaiter().GetResult();
    }

    /// <inheritdoc />
    public async Task<HubbleStatistics> GetStatisticsAsync()
    {
        var stats = await _statsCollection.Find(_ => true)
            .SortByDescending(s => s.Timestamp)
            .FirstOrDefaultAsync();

        if (stats == null)
        {
            // Si no hay estadísticas, las calculamos
            return await RecalculateStatisticsAsync();
        }

        return stats;
    }

    /// <inheritdoc />
    public async Task<HubbleStatistics> UpdatePruneStatisticsAsync(DateTime pruneDate, long logsDeleted)
    {
        var stats = await GetStatisticsAsync();
        
        // Actualizar información del último prune
        stats.LastPrune.LastPruneDate = pruneDate;
        stats.LastPrune.LogsDeleted = logsDeleted;
        
        // Recalcular el resto de estadísticas
        stats.TotalLogs = await _logsCollection.CountDocumentsAsync(FilterDefinition<GeneralLog>.Empty);
        stats.SuccessfulLogs = await _logsCollection.CountDocumentsAsync(
            Builders<GeneralLog>.Filter.And(
                Builders<GeneralLog>.Filter.Gte(l => l.StatusCode, 200),
                Builders<GeneralLog>.Filter.Lt(l => l.StatusCode, 300)
            ));
        stats.FailedLogs = await _logsCollection.CountDocumentsAsync(
            Builders<GeneralLog>.Filter.Gte(l => l.StatusCode, 400)
        );
        stats.LoggerLogs = await _logsCollection.CountDocumentsAsync(
            Builders<GeneralLog>.Filter.Eq(l => l.Method, "LOGGER")
        );
        
        stats.Timestamp = DateTime.UtcNow;
        
        // Guardar las estadísticas actualizadas
        await _statsCollection.ReplaceOneAsync(
            s => s.Id == stats.Id,
            stats,
            new ReplaceOptions { IsUpsert = true }
        );
        
        return stats;
    }

    /// <inheritdoc />
    public async Task<HubbleSystemConfiguration> GetSystemConfigurationAsync()
    {
        var config = await _configCollection.Find(_ => true).FirstOrDefaultAsync();
        
        if (config == null)
        {
            // Si no hay configuración, creamos una nueva con valores por defecto
            return await EnsureInitialConfigurationAsync();
        }
        
        return config;
    }

    /// <inheritdoc />
    public async Task<HubbleSystemConfiguration> SaveSystemConfigurationAsync(HubbleSystemConfiguration config)
    {
        config.Timestamp = DateTime.UtcNow;
        
        await _configCollection.ReplaceOneAsync(
            c => c.Id == config.Id,
            config,
            new ReplaceOptions { IsUpsert = true }
        );
        
        return config;
    }

    /// <inheritdoc />
    public async Task<HubbleStatistics> RecalculateStatisticsAsync()
    {
        var stats = new HubbleStatistics
        {
            Timestamp = DateTime.UtcNow,
            TotalLogs = await _logsCollection.CountDocumentsAsync(FilterDefinition<GeneralLog>.Empty),
            SuccessfulLogs = await _logsCollection.CountDocumentsAsync(
                Builders<GeneralLog>.Filter.And(
                    Builders<GeneralLog>.Filter.Gte(l => l.StatusCode, 200),
                    Builders<GeneralLog>.Filter.Lt(l => l.StatusCode, 300)
                )),
            FailedLogs = await _logsCollection.CountDocumentsAsync(
                Builders<GeneralLog>.Filter.Gte(l => l.StatusCode, 400)
            ),
            LoggerLogs = await _logsCollection.CountDocumentsAsync(
                Builders<GeneralLog>.Filter.Eq(l => l.Method, "LOGGER")
            )
        };
        
        // Obtener la información de la última limpieza
        var config = await GetSystemConfigurationAsync();
        if (config.EnableDataPrune)
        {
            var oldStats = await _statsCollection.Find(_ => true)
                .SortByDescending(s => s.Timestamp)
                .FirstOrDefaultAsync();
                
            if (oldStats != null && oldStats.LastPrune != null && oldStats.LastPrune.LastPruneDate.HasValue)
            {
                stats.LastPrune = oldStats.LastPrune;
            }
        }
        
        // Guardar las estadísticas actualizadas
        await _statsCollection.ReplaceOneAsync(
            s => s.Id == stats.Id,
            stats,
            new ReplaceOptions { IsUpsert = true }
        );
        
        return stats;
    }
    
    /// <summary>
    /// Asegura que exista una configuración inicial en la base de datos
    /// </summary>
    private async Task<HubbleSystemConfiguration> EnsureInitialConfigurationAsync()
    {
        var config = await _configCollection.Find(_ => true).FirstOrDefaultAsync();
        
        if (config == null)
        {
            try
            {
                // Obtener información sobre la versión de MongoDB de manera más segura
                string mongoVersion = "Desconocida";
                try
                {
                    // Intentar obtener la versión usando un enfoque más simple
                    var serverStatus = await _mongoClient.GetDatabase("admin")
                        .RunCommandAsync<MongoDB.Bson.BsonDocument>(new MongoDB.Bson.BsonDocument("serverStatus", 1));
                    
                    if (serverStatus.Contains("version"))
                    {
                        mongoVersion = serverStatus["version"].AsString;
                    }
                }
                catch
                {
                    // Si falla, intentar con otra aproximación
                    try
                    {
                        // Intentar determinar la versión de MongoDB usando el ping
                        await _mongoClient.GetDatabase("admin").RunCommandAsync<MongoDB.Bson.BsonDocument>(
                            new MongoDB.Bson.BsonDocument("ping", 1));
                        mongoVersion = "3.x o superior"; // Si no hay error, asumimos una versión reciente
                    }
                    catch
                    {
                        // No podemos determinar la versión
                        mongoVersion = "Desconocida";
                    }
                }
                
                // Crear configuración inicial con los valores de las opciones
                config = new HubbleSystemConfiguration
                {
                    ServiceName = _options.ServiceName,
                    TimeZoneId = _options.TimeZoneId,
                    EnableDataPrune = _options.EnableDataPrune,
                    DataPruneIntervalHours = _options.DataPruneIntervalHours,
                    MaxLogAgeHours = _options.MaxLogAgeHours,
                    CaptureLoggerMessages = _options.CaptureLoggerMessages,
                    CaptureHttpRequests = true,
                    IgnorePaths = _options.IgnorePaths,
                    MinimumLogLevel = "Information",
                    SystemInfo = new SystemInfo
                    {
                        Version = GetType().Assembly.GetName().Version?.ToString() ?? "0.2.9.0",
                        MongoDBVersion = mongoVersion,
                        DatabaseName = _databaseName,
                        ConnectionString = ObfuscateConnectionString(_mongoClient.Settings.ToString() ?? string.Empty),
                        StartTime = DateTime.UtcNow
                    }
                };
                
                await _configCollection.InsertOneAsync(config);
                
                _logger.LogInformation("Configuración inicial de Hubble creada correctamente");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al crear la configuración inicial de Hubble");
                
                // Crear una configuración mínima en caso de error
                config = new HubbleSystemConfiguration
                {
                    ServiceName = _options.ServiceName,
                    TimeZoneId = _options.TimeZoneId,
                    EnableDataPrune = _options.EnableDataPrune,
                    DataPruneIntervalHours = _options.DataPruneIntervalHours,
                    MaxLogAgeHours = _options.MaxLogAgeHours,
                    SystemInfo = new SystemInfo
                    {
                        Version = "0.2.9.0",
                        DatabaseName = _databaseName,
                        MongoDBVersion = "Desconocida",
                        ConnectionString = ObfuscateConnectionString(_mongoClient.Settings.ToString() ?? string.Empty),
                        StartTime = DateTime.UtcNow
                    }
                };
                
                await _configCollection.InsertOneAsync(config);
            }
        }
        
        return config;
    }
    
    /// <summary>
    /// Ofusca la cadena de conexión para evitar exponer información sensible
    /// </summary>
    /// <param name="connectionString">Cadena de conexión original</param>
    /// <returns>Cadena de conexión ofuscada</returns>
    private string ObfuscateConnectionString(string connectionString)
    {
        // Proteger datos sensibles como contraseñas
        if (string.IsNullOrEmpty(connectionString))
            return string.Empty;
            
        try
        {
            // Ofuscar usuario y contraseña en la cadena de conexión
            var parts = connectionString.Split('@');
            if (parts.Length > 1)
            {
                var credentials = parts[0].Split(':');
                if (credentials.Length > 1)
                {
                    // Mostrar solo el inicio del usuario y ofuscar la contraseña
                    var user = credentials[0];
                    if (user.Contains("//"))
                    {
                        var userParts = user.Split("//");
                        user = userParts[0] + "//" + ObfuscateText(userParts[1]);
                    }
                    else
                    {
                        user = ObfuscateText(user);
                    }
                    
                    return $"{user}:****@{parts[1]}";
                }
            }
            
            return connectionString;
        }
        catch
        {
            // En caso de error, devolver una versión muy ofuscada
            return "mongodb://****:****@****";
        }
    }
    
    /// <summary>
    /// Ofusca texto mostrando solo los primeros caracteres
    /// </summary>
    /// <param name="text">Texto a ofuscar</param>
    /// <returns>Texto ofuscado</returns>
    private string ObfuscateText(string text)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= 3)
            return "***";
            
        return text.Substring(0, Math.Min(3, text.Length)) + "***";
    }
} 