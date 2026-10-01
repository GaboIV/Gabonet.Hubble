namespace Gabonet.Hubble.Extensions;

using Gabonet.Hubble.BackgroundServices;
using Gabonet.Hubble.Interfaces;
using Gabonet.Hubble.Logging;
using Gabonet.Hubble.Middleware;
using Gabonet.Hubble.Models;
using Gabonet.Hubble.Services;
using Gabonet.Hubble.UI;
using Gabonet.Hubble.Utilities;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using System;
using System.Collections.Generic;

/// <summary>
/// Extensiones para configurar los servicios de Hubble en la aplicación.
/// </summary>
public static class ServiceCollectionExtensions
{
    private static readonly TimeSpan DefaultServerSelectionTimeout = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Agrega los servicios de Hubble configurándolos desde código.
    /// </summary>
    /// <param name="services">Colección de servicios</param>
    /// <param name="configureOptions">Acción para configurar las opciones (ConnectionString y DatabaseName son obligatorios)</param>
    /// <returns>Colección de servicios con Hubble configurado</returns>
    public static IServiceCollection AddHubble(
        this IServiceCollection services,
        Action<HubbleOptions> configureOptions)
    {
        if (configureOptions == null)
        {
            throw new ArgumentNullException(nameof(configureOptions));
        }

        var options = new HubbleOptions();
        configureOptions(options);
        return services.AddHubbleCore(options);
    }

    /// <summary>
    /// Agrega los servicios de Hubble leyendo las opciones de una sección de configuración (por ejemplo, "Hubble" en appsettings.json).
    /// Las listas configuradas reemplazan a los valores por defecto. <paramref name="configureOptions"/> se aplica después
    /// de leer la sección, para completar o sobrescribir valores (por ejemplo, la cadena de conexión).
    /// </summary>
    /// <param name="services">Colección de servicios</param>
    /// <param name="configurationSection">Sección de configuración, normalmente <c>configuration.GetSection("Hubble")</c></param>
    /// <param name="configureOptions">Acción opcional para completar o sobrescribir las opciones</param>
    /// <returns>Colección de servicios con Hubble configurado</returns>
    public static IServiceCollection AddHubble(
        this IServiceCollection services,
        IConfigurationSection configurationSection,
        Action<HubbleOptions>? configureOptions = null)
    {
        if (configurationSection == null)
        {
            throw new ArgumentNullException(nameof(configurationSection));
        }

        var options = HubbleOptionsBinder.Bind(configurationSection);
        configureOptions?.Invoke(options);
        return services.AddHubbleCore(options);
    }

    /// <summary>
    /// Agrega los servicios de Hubble a la colección de servicios.
    /// </summary>
    /// <param name="services">Colección de servicios</param>
    /// <param name="connectionString">String de conexión a MongoDB</param>
    /// <param name="databaseName">Nombre de la base de datos</param>
    /// <param name="serviceName">Nombre del servicio (opcional)</param>
    /// <param name="timeZoneId">ID de la zona horaria (opcional)</param>
    /// <returns>Colección de servicios con Hubble configurado</returns>
    [Obsolete("Usa AddHubble(options => { options.ConnectionString = ...; options.DatabaseName = ...; }). Esta sobrecarga se eliminará en una versión futura.")]
    public static IServiceCollection AddHubble(
        this IServiceCollection services,
        string connectionString,
        string databaseName,
        string serviceName = "HubbleService",
        string? timeZoneId = null)
    {
        return services.AddHubble(options =>
        {
            options.ConnectionString = connectionString;
            options.DatabaseName = databaseName;
            options.ServiceName = serviceName;
            options.TimeZoneId = timeZoneId ?? string.Empty;
        });
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
    [Obsolete("Usa AddHubble(configuration.GetSection(\"Hubble\"), options => { options.ConnectionString = ...; options.DatabaseName = ...; }). Esta sobrecarga se eliminará en una versión futura.")]
    public static IServiceCollection AddHubble(
        this IServiceCollection services,
        IConfiguration configuration,
        string connectionString,
        string databaseName,
        string sectionName = HubbleOptions.SectionName)
    {
        return services.AddHubble(configuration.GetSection(sectionName), options =>
        {
            options.ConnectionString = connectionString;
            options.DatabaseName = databaseName;
        });
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
    /// <param name="minimumLevel">Nivel mínimo de log a capturar. Si no se indica, se usa <see cref="HubbleOptions.MinimumLogLevel"/>.</param>
    /// <returns>Constructor de logging con Hubble configurado</returns>
    public static ILoggingBuilder AddHubbleLogging(this ILoggingBuilder builder, LogLevel? minimumLevel = null)
    {
        // El proveedor solo encola los logs (singleton, sin acceso a MongoDB); requiere que AddHubble() se haya llamado
        builder.Services.AddSingleton<ILoggerProvider>(sp =>
        {
            var options = sp.GetRequiredService<HubbleOptions>();
            return new HubbleLoggerProvider(
                sp.GetRequiredService<HubbleLogQueue>(),
                sp.GetRequiredService<IHttpContextAccessor>(),
                options,
                minimumLevel ?? options.MinimumLogLevel);
        });

        return builder;
    }

    /// <summary>
    /// Único camino de registro: valida las opciones y registra todos los servicios de Hubble.
    /// </summary>
    private static IServiceCollection AddHubbleCore(this IServiceCollection services, HubbleOptions options)
    {
        options.Validate();

        // Cliente de MongoDB propio de Hubble. No se registra en el contenedor: así nunca reemplaza
        // ni se confunde con el IMongoClient que la aplicación pueda registrar para sus propios datos.
        var mongoClient = CreateMongoClient(options.ConnectionString);

        services.AddHttpContextAccessor();
        services.AddSingleton(options);
        services.AddSingleton<IOptions<HubbleOptions>>(Options.Create(options));

        // Data Protection firma la cookie de sesión; Antiforgery protege los formularios del dashboard contra CSRF
        services.AddDataProtection();
        services.AddAntiforgery();

        AddHubbleStorageServices(services, mongoClient, options);

        services.AddScoped<IHubbleService>(provider => new HubbleService(
            mongoClient,
            options.DatabaseName,
            provider.GetRequiredService<IHttpContextAccessor>(),
            options.ServiceName,
            options.TimeZoneId));

        services.AddSingleton<IHubbleStatsService>(provider => new HubbleStatsService(
            mongoClient,
            options.DatabaseName,
            options,
            provider.GetRequiredService<ILogger<HubbleStatsService>>()));

        services.AddTransient<HubbleController>();

        // Initialize the masking helper with the options
        HubbleMaskingHelper.Initialize(options);

        return services;
    }

    /// <summary>
    /// Crea el cliente de MongoDB de Hubble. Si la cadena de conexión no indica serverSelectionTimeoutMS,
    /// se usa un timeout corto: con MongoDB caído, el dashboard muestra el error en segundos y no en 30.
    /// </summary>
    private static MongoClient CreateMongoClient(string connectionString)
    {
        var settings = MongoClientSettings.FromConnectionString(connectionString);

        if (connectionString.IndexOf("serverSelectionTimeoutMS", StringComparison.OrdinalIgnoreCase) < 0)
        {
            settings.ServerSelectionTimeout = DefaultServerSelectionTimeout;
        }

        return new MongoClient(settings);
    }

    /// <summary>
    /// Registra la cola de logs, el servicio en segundo plano que la guarda en MongoDB por lotes
    /// y el que prepara los índices y la retención (índice TTL).
    /// </summary>
    private static void AddHubbleStorageServices(IServiceCollection services, IMongoClient mongoClient, HubbleOptions options)
    {
        var logsCollection = mongoClient.GetDatabase(options.DatabaseName).GetCollection<GeneralLog>("HubbleLogs");

        services.AddSingleton(new HubbleLogQueue());
        services.AddSingleton(new HubbleStorageStatus());
        services.AddHostedService(sp => new HubbleLogWriterService(
            sp.GetRequiredService<HubbleLogQueue>(),
            logsCollection,
            options));
        services.AddHostedService(sp => new HubbleStorageInitializer(
            logsCollection,
            options,
            sp.GetRequiredService<HubbleStorageStatus>()));
    }
}

/// <summary>
/// Lee <see cref="HubbleOptions"/> desde una sección de configuración.
/// </summary>
internal static class HubbleOptionsBinder
{
    /// <summary>
    /// Enlaza la sección sobre unas opciones con valores por defecto. El binder de .NET agrega los elementos de una lista
    /// a los que ya tiene; aquí, una lista presente en la configuración reemplaza a la lista por defecto
    /// (por ejemplo, MaskHeaders configurado sustituye a los headers enmascarados por defecto).
    /// </summary>
    public static HubbleOptions Bind(IConfiguration section)
    {
        var options = new HubbleOptions();

        ClearIfConfigured(section, nameof(HubbleOptions.IgnorePaths), options.IgnorePaths);

        var security = section.GetSection(nameof(HubbleOptions.Security));
        ClearIfConfigured(security, nameof(SecurityConfiguration.MaskBodyProperties), options.Security.MaskBodyProperties);
        ClearIfConfigured(security, nameof(SecurityConfiguration.MaskRequestBodyProperties), options.Security.MaskRequestBodyProperties);
        ClearIfConfigured(security, nameof(SecurityConfiguration.MaskResponseBodyProperties), options.Security.MaskResponseBodyProperties);
        ClearIfConfigured(security, nameof(SecurityConfiguration.MaskHeaders), options.Security.MaskHeaders);
        ClearIfConfigured(security, nameof(SecurityConfiguration.AllowedIps), options.Security.AllowedIps);

        section.Bind(options);
        return options;
    }

    private static void ClearIfConfigured(IConfiguration section, string key, List<string> list)
    {
        if (section.GetSection(key).Exists())
        {
            list.Clear();
        }
    }
}
