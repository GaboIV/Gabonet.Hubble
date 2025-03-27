namespace Gabonet.Hubble.Extensions;

using Gabonet.Hubble.Interfaces;
using Gabonet.Hubble.Logging;
using Gabonet.Hubble.Middleware;
using Gabonet.Hubble.Services;
using Gabonet.Hubble.UI;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using System;

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
            CaptureLoggerMessages = true
        });

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

        // Registrar el servicio de Hubble
        services.AddScoped<IHubbleService>(provider =>
        {
            var httpContextAccessor = provider.GetRequiredService<IHttpContextAccessor>();
            return new HubbleService(mongoClient, config.DatabaseName, httpContextAccessor, config.ServiceName, config.TimeZoneId);
        });

        // Registrar el controlador de Hubble
        services.AddTransient<HubbleController>();

        // Registrar las opciones
        services.AddSingleton(new HubbleOptions
        {
            ServiceName = config.ServiceName,
            EnableDiagnostics = config.EnableDiagnostics,
            CaptureLoggerMessages = config.CaptureLoggerMessages,
            CaptureHttpRequests = config.CaptureHttpRequests,
            RequireAuthentication = config.RequireAuthentication,
            Username = config.Username,
            Password = config.Password
        });

        return services;
    }

    /// <summary>
    /// Agrega el middleware de Hubble a la canalización de la aplicación.
    /// </summary>
    /// <param name="app">Constructor de aplicaciones</param>
    /// <returns>Constructor de aplicaciones con Hubble configurado</returns>
    public static IApplicationBuilder UseHubble(this IApplicationBuilder app)
    {
        // Agregar el middleware de Hubble
        app.UseMiddleware<HubbleMiddleware>();

        // Agregar el middleware para la interfaz de usuario de Hubble
        app.UseMiddleware<HubbleUIMiddleware>();

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
} 