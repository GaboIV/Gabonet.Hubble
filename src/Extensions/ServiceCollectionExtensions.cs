namespace Gabonet.Hubble.Extensions;

using Gabonet.Hubble.Interfaces;
using Gabonet.Hubble.Middleware;
using Gabonet.Hubble.Services;
using Gabonet.Hubble.UI;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
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
            ServiceName = serviceName
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
        services.AddScoped<HubbleController>();

        // Registrar las opciones
        services.AddSingleton(new HubbleOptions
        {
            ServiceName = config.ServiceName,
            EnableDiagnostics = config.EnableDiagnostics,
            BasePath = config.BasePath
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
    /// Ruta base para la interfaz de usuario de Hubble
    /// </summary>
    public string BasePath { get; set; } = "/hubble";
} 