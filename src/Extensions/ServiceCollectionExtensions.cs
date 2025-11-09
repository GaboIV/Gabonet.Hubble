namespace Gabonet.Hubble.Extensions;

using Gabonet.Hubble.Interfaces;
using Gabonet.Hubble.Logging;
using Gabonet.Hubble.Middleware;
using Gabonet.Hubble.Models;
using Gabonet.Hubble.Services;
using Gabonet.Hubble.UI;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Extensiones para configurar los servicios de Hubble en la aplicación.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Agrega los servicios de Hubble a la colección de servicios.
    /// </summary>
    /// <param name="services">Colección de servicios</param>
    /// <param name="connectionString">String de conexión a MongoDB</param>
    /// <param name="databaseName">Nombre de la base de datos</param>
    /// <param name="serviceName">Nombre del servicio (opcional)</param>
    /// <param name="timeZoneId">ID de la zona horaria (opcional)</param>
    /// <returns>Colección de servicios con Hubble configurado</returns>
    public static IServiceCollection AddHubble(
        this IServiceCollection services,
        string connectionString,
        string databaseName,
        string serviceName = "HubbleService",
        string? timeZoneId = null)
    {
        // Registrar el cliente de MongoDB
        var mongoClient = new MongoClient(connectionString);
        services.AddSingleton<IMongoClient>(mongoClient);

        // Registrar el acceso al contexto HTTP
        services.AddHttpContextAccessor();

        // Registrar el servicio de Hubble
        services.AddScoped<IHubbleService>(provider =>
        {
            var httpContextAccessor = provider.GetRequiredService<IHttpContextAccessor>();
            return new HubbleService(mongoClient, databaseName, httpContextAccessor, serviceName, timeZoneId ?? "Universal");
        });

        // Registrar el controlador de Hubble
        services.AddScoped<HubbleController>();

        // Registrar las opciones
        services.AddSingleton(new HubbleOptions
        {
            ServiceName = serviceName,
            // Activar por defecto la captura de logs
            CaptureLoggerMessages = true,
            BasePath = "/hubble",
            // Por defecto, no destacar nuevos servicios
            HighlightNewServices = false,
            HighlightDurationSeconds = 5,
            IgnorePaths = new List<string>(),
            TimeZoneId = timeZoneId ?? string.Empty
        });

        return services;
    }

    /// <summary>
    /// Agrega los servicios de Hubble a la colección de servicios usando configuración desde appsettings.json
    /// </summary>
    /// <param name="services">Colección de servicios</param>
    /// <param name="configuration">Configuración de la aplicación</param>
    /// <param name="connectionString">Cadena de conexión a MongoDB</param>
    /// <param name="databaseName">Nombre de la base de datos</param>
    /// <param name="sectionName">Nombre de la sección en appsettings.json (por defecto: "Hubble")</param>
    /// <returns>Colección de servicios con Hubble configurado</returns>
    public static IServiceCollection AddHubble(
        this IServiceCollection services,
        IConfiguration configuration,
        string connectionString,
        string databaseName,
        string sectionName = HubbleAuthConfiguration.SectionName)
    {
        if (string.IsNullOrEmpty(connectionString))
        {
            throw new ArgumentException("La cadena de conexión es requerida", nameof(connectionString));
        }

        if (string.IsNullOrEmpty(databaseName))
        {
            throw new ArgumentException("El nombre de la base de datos es requerido", nameof(databaseName));
        }

        // Cargar configuración desde appsettings.json
        var hubbleConfig = new HubbleAuthConfiguration();
        var section = configuration.GetSection(sectionName);
        
        // Cargar valores manualmente desde la configuración usando indexadores
        if (bool.TryParse(section[nameof(HubbleAuthConfiguration.RequireAuthentication)], out bool requireAuth))
            hubbleConfig.RequireAuthentication = requireAuth;
        
        hubbleConfig.Username = section[nameof(HubbleAuthConfiguration.Username)] ?? string.Empty;
        hubbleConfig.Password = section[nameof(HubbleAuthConfiguration.Password)] ?? string.Empty;
        hubbleConfig.BasePath = section[nameof(HubbleAuthConfiguration.BasePath)] ?? "/hubble";
        hubbleConfig.PrefixPath = section[nameof(HubbleAuthConfiguration.PrefixPath)] ?? string.Empty;
        hubbleConfig.ServiceName = section[nameof(HubbleAuthConfiguration.ServiceName)] ?? "HubbleService";
        
        if (bool.TryParse(section[nameof(HubbleAuthConfiguration.EnableDiagnostics)], out bool enableDiag))
            hubbleConfig.EnableDiagnostics = enableDiag;
        
        if (bool.TryParse(section[nameof(HubbleAuthConfiguration.CaptureLoggerMessages)], out bool captureLogger))
            hubbleConfig.CaptureLoggerMessages = captureLogger;
        
        if (bool.TryParse(section[nameof(HubbleAuthConfiguration.CaptureHttpRequests)], out bool captureHttp))
            hubbleConfig.CaptureHttpRequests = captureHttp;
        else
            hubbleConfig.CaptureHttpRequests = true; // Valor por defecto
        
        if (bool.TryParse(section[nameof(HubbleAuthConfiguration.IgnoreStaticFiles)], out bool ignoreStatic))
            hubbleConfig.IgnoreStaticFiles = ignoreStatic;
        else
            hubbleConfig.IgnoreStaticFiles = true; // Valor por defecto
        
        if (bool.TryParse(section[nameof(HubbleAuthConfiguration.EnableDataPrune)], out bool enablePrune))
            hubbleConfig.EnableDataPrune = enablePrune;
        
        if (int.TryParse(section[nameof(HubbleAuthConfiguration.DataPruneIntervalHours)], out int pruneInterval))
            hubbleConfig.DataPruneIntervalHours = pruneInterval;
        else
            hubbleConfig.DataPruneIntervalHours = 1; // Valor por defecto
        
        if (int.TryParse(section[nameof(HubbleAuthConfiguration.MaxLogAgeHours)], out int maxAge))
            hubbleConfig.MaxLogAgeHours = maxAge;
        else
            hubbleConfig.MaxLogAgeHours = 24; // Valor por defecto
        
        hubbleConfig.TimeZoneId = section[nameof(HubbleAuthConfiguration.TimeZoneId)] ?? string.Empty;
        
        if (bool.TryParse(section[nameof(HubbleAuthConfiguration.HighlightNewServices)], out bool highlightNew))
            hubbleConfig.HighlightNewServices = highlightNew;
        
        if (int.TryParse(section[nameof(HubbleAuthConfiguration.HighlightDurationSeconds)], out int highlightDuration))
            hubbleConfig.HighlightDurationSeconds = highlightDuration;
        else
            hubbleConfig.HighlightDurationSeconds = 5; // Valor por defecto
        
        // Cargar IgnorePaths como array
        var ignorePathsSection = section.GetSection(nameof(HubbleAuthConfiguration.IgnorePaths));
        var ignorePaths = new List<string>();
        foreach (var child in ignorePathsSection.GetChildren())
        {
            if (!string.IsNullOrEmpty(child.Value))
                ignorePaths.Add(child.Value);
        }
        hubbleConfig.IgnorePaths = ignorePaths;

        // Registrar el cliente de MongoDB
        var mongoClient = new MongoClient(connectionString);
        services.AddSingleton<IMongoClient>(mongoClient);

        // Registrar el acceso al contexto HTTP
        services.AddHttpContextAccessor();

        // Crear las opciones de Hubble usando la configuración cargada
        var options = new HubbleOptions
        {
            ServiceName = hubbleConfig.ServiceName,
            EnableDiagnostics = hubbleConfig.EnableDiagnostics,
            CaptureLoggerMessages = hubbleConfig.CaptureLoggerMessages,
            CaptureHttpRequests = hubbleConfig.CaptureHttpRequests,
            RequireAuthentication = hubbleConfig.RequireAuthentication,
            Username = hubbleConfig.Username,
            Password = hubbleConfig.Password,
            BasePath = hubbleConfig.BasePath,
            PrefixPath = hubbleConfig.PrefixPath,
            HighlightNewServices = hubbleConfig.HighlightNewServices,
            HighlightDurationSeconds = hubbleConfig.HighlightDurationSeconds,
            IgnorePaths = hubbleConfig.IgnorePaths ?? new List<string>(),
            IgnoreStaticFiles = hubbleConfig.IgnoreStaticFiles,
            EnableDataPrune = hubbleConfig.EnableDataPrune,
            DataPruneIntervalHours = hubbleConfig.DataPruneIntervalHours,
            MaxLogAgeHours = hubbleConfig.MaxLogAgeHours,
            TimeZoneId = hubbleConfig.TimeZoneId
        };
        
        services.AddSingleton(options);

        // Registrar el servicio de Hubble
        services.AddScoped<IHubbleService>(provider =>
        {
            var httpContextAccessor = provider.GetRequiredService<IHttpContextAccessor>();
            return new HubbleService(mongoClient, databaseName, httpContextAccessor, hubbleConfig.ServiceName, hubbleConfig.TimeZoneId);
        });
        
        // Registrar el servicio de estadísticas de Hubble
        services.AddSingleton<IHubbleStatsService>(provider =>
        {
            var logger = provider.GetRequiredService<ILogger<HubbleStatsService>>();
            return new HubbleStatsService(mongoClient, databaseName, options, logger);
        });

        // Registrar el controlador de Hubble
        services.AddTransient<HubbleController>();
        
        // Registrar el servicio de limpieza de datos si está habilitado
        if (hubbleConfig.EnableDataPrune)
        {
            services.AddHostedService<BackgroundServices.DataPruneService>();
            Console.WriteLine($"[Hubble] Servicio de limpieza automática habilitado (Intervalo: {hubbleConfig.DataPruneIntervalHours}h, Max. edad: {hubbleConfig.MaxLogAgeHours}h)");
        }

        // Mostrar información de autenticación
        if (hubbleConfig.RequireAuthentication)
        {
            Console.WriteLine($"[Hubble] Autenticación habilitada para usuario: {hubbleConfig.Username}");
        }

        return services;
    }

    /// <summary>
    /// Agrega los servicios de Hubble a la colección de servicios con opciones personalizadas.
    /// </summary>
    /// <param name="services">Colección de servicios</param>
    /// <param name="configureOptions">Acción para configurar las opciones</param>
    /// <returns>Colección de servicios con Hubble configurado</returns>
    public static IServiceCollection AddHubble(
        this IServiceCollection services,
        Action<HubbleConfiguration> configureOptions)
    {
        var config = new HubbleConfiguration();
        configureOptions(config);

        if (string.IsNullOrEmpty(config.ConnectionString))
        {
            throw new ArgumentException("La cadena de conexión es requerida", nameof(configureOptions));
        }

        if (string.IsNullOrEmpty(config.DatabaseName))
        {
            throw new ArgumentException("El nombre de la base de datos es requerido", nameof(configureOptions));
        }

        // Registrar el cliente de MongoDB
        var mongoClient = new MongoClient(config.ConnectionString);
        services.AddSingleton<IMongoClient>(mongoClient);

        // Registrar el acceso al contexto HTTP
        services.AddHttpContextAccessor();

        // Registrar las opciones
        var options = new HubbleOptions
        {
            ServiceName = config.ServiceName,
            EnableDiagnostics = config.EnableDiagnostics,
            CaptureLoggerMessages = config.CaptureLoggerMessages,
            CaptureHttpRequests = config.CaptureHttpRequests,
            RequireAuthentication = config.RequireAuthentication,
            Username = config.Username,
            Password = config.Password,
            BasePath = config.BasePath,
            PrefixPath = config.PrefixPath,
            HighlightNewServices = config.HighlightNewServices,
            HighlightDurationSeconds = config.HighlightDurationSeconds,
            IgnorePaths = config.IgnorePaths ?? new List<string>(),
            EnableDataPrune = config.EnableDataPrune,
            DataPruneIntervalHours = config.DataPruneIntervalHours,
            MaxLogAgeHours = config.MaxLogAgeHours,
            TimeZoneId = config.TimeZoneId
        };
        
        services.AddSingleton(options);

        // Registrar el servicio de Hubble
        services.AddScoped<IHubbleService>(provider =>
        {
            var httpContextAccessor = provider.GetRequiredService<IHttpContextAccessor>();
            return new HubbleService(mongoClient, config.DatabaseName, httpContextAccessor, config.ServiceName, config.TimeZoneId);
        });
        
        // Registrar el servicio de estadísticas de Hubble
        services.AddSingleton<IHubbleStatsService>(provider =>
        {
            var logger = provider.GetRequiredService<ILogger<HubbleStatsService>>();
            return new HubbleStatsService(mongoClient, config.DatabaseName, options, logger);
        });

        // Registrar el controlador de Hubble
        services.AddTransient<HubbleController>();
        
        // Registrar el servicio de limpieza de datos si está habilitado
        if (config.EnableDataPrune)
        {
            services.AddHostedService<BackgroundServices.DataPruneService>();
            Console.WriteLine($"[Hubble] Servicio de limpieza automática habilitado (Intervalo: {config.DataPruneIntervalHours}h, Max. edad: {config.MaxLogAgeHours}h)");
        }

        return services;
    }

    /// <summary>
    /// Agrega el middleware de Hubble a la canalización de la aplicación.
    /// </summary>
    /// <param name="app">Constructor de aplicaciones</param>
    /// <returns>Constructor de aplicaciones con Hubble configurado</returns>
    public static IApplicationBuilder UseHubble(this IApplicationBuilder app)
    {
        Console.WriteLine($"[Hubble] Iniciando middleware...");
        
        // Obtener las opciones configuradas
        var options = app.ApplicationServices.GetService<HubbleOptions>();
        if (options != null)
        {
            Console.WriteLine($"[Hubble] Servicio: {options.ServiceName}");
            Console.WriteLine($"[Hubble] Interfaz web disponible en: {options.BasePath}");
            
            if (options.RequireAuthentication)
            {
                Console.WriteLine($"[Hubble] Autenticación habilitada para acceder a la interfaz");
            }
        }
        
        // Agregar el middleware de Hubble
        app.UseMiddleware<HubbleMiddleware>();

        // Agregar el middleware para la interfaz de usuario de Hubble
        app.UseMiddleware<HubbleUIMiddleware>();

        Console.WriteLine($"[Hubble] Middleware configurado correctamente");
        return app;
    }

    /// <summary>
    /// Agrega la captura de logs de ILogger a la aplicación.
    /// </summary>
    /// <param name="builder">Constructor de logging</param>
    /// <param name="minimumLevel">Nivel mínimo de log a capturar</param>
    /// <returns>Constructor de logging con Hubble configurado</returns>
    public static ILoggingBuilder AddHubbleLogging(this ILoggingBuilder builder, LogLevel minimumLevel = LogLevel.Information)
    {        
        // En lugar de intentar resolver IHubbleService directamente, que es un servicio scoped,
        // creamos una factory que resuelve el servicio cuando se necesita, evitando el error
        // "Cannot resolve scoped service from root provider"
        builder.Services.AddSingleton<ILoggerProvider>(sp => 
        {
            // Crear un provider que obtendrá IHubbleService desde el scope apropiado
            return new HubbleLoggerProvider(
                () => sp.CreateScope().ServiceProvider.GetRequiredService<IHubbleService>(),
                minimumLevel);
        });
        
        return builder;
    }
}

/// <summary>
/// Configuración para Hubble.
/// </summary>
public class HubbleConfiguration
{
    /// <summary>
    /// Cadena de conexión a MongoDB
    /// </summary>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// Nombre de la base de datos
    /// </summary>
    public string DatabaseName { get; set; } = string.Empty;

    /// <summary>
    /// Nombre del servicio
    /// </summary>
    public string ServiceName { get; set; } = "HubbleService";

    /// <summary>
    /// ID de la zona horaria para mostrar las fechas
    /// </summary>
    public string TimeZoneId { get; set; } = string.Empty;

    /// <summary>
    /// Lista de rutas que deben ser ignoradas por el middleware
    /// </summary>
    public List<string> IgnorePaths { get; set; } = new List<string>();

    /// <summary>
    /// Habilitar diagnósticos
    /// </summary>
    public bool EnableDiagnostics { get; set; } = false;
    
    /// <summary>
    /// Habilitar la captura de mensajes de ILogger
    /// </summary>
    public bool CaptureLoggerMessages { get; set; } = false;
    
    /// <summary>
    /// Nivel mínimo de log a capturar
    /// </summary>
    public LogLevel MinimumLogLevel { get; set; } = LogLevel.Information;
    
    /// <summary>
    /// Habilitar la captura de solicitudes HTTP
    /// </summary>
    public bool CaptureHttpRequests { get; set; } = true;

    /// <summary>
    /// Indica si se debe habilitar la protección con autenticación
    /// </summary>
    public bool RequireAuthentication { get; set; } = false;
    
    /// <summary>
    /// Nombre de usuario para la autenticación (si RequireAuthentication es true)
    /// </summary>
    public string Username { get; set; } = string.Empty;
    
    /// <summary>
    /// Contraseña para la autenticación (si RequireAuthentication es true)
    /// </summary>
    public string Password { get; set; } = string.Empty;
    
    /// <summary>
    /// Ruta base para acceder a la interfaz de Hubble
    /// </summary>
    public string BasePath { get; set; } = "/hubble";
    
    /// <summary>
    /// Prefijo de ruta para las rutas de Hubble. Por defecto es string.Empty
    /// </summary>
    public string PrefixPath { get; set; } = string.Empty;
    
    /// <summary>
    /// Indica si se deben destacar los nuevos servicios que se van agregando en tiempo real.
    /// </summary>
    public bool HighlightNewServices { get; set; } = false;
    
    /// <summary>
    /// Duración en segundos que los nuevos servicios permanecerán destacados. Por defecto es 5 segundos.
    /// </summary>
    public int HighlightDurationSeconds { get; set; } = 5;
    
    /// <summary>
    /// Activa o desactiva el sistema de limpieza automática de logs antiguos
    /// </summary>
    public bool EnableDataPrune { get; set; } = false;
    
    /// <summary>
    /// Intervalo en horas entre cada ejecución del proceso de limpieza de logs
    /// </summary>
    public int DataPruneIntervalHours { get; set; } = 1;
    
    /// <summary>
    /// Edad máxima en horas que se conservarán los logs antes de ser eliminados
    /// </summary>
    public int MaxLogAgeHours { get; set; } = 24;
} 