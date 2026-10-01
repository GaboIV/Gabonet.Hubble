namespace Gabonet.Hubble.Middleware;

using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;

/// <summary>
/// Opciones de configuración de Hubble. Es la única clase de configuración: se rellena desde código
/// (<c>AddHubble(options => ...)</c>) o desde una sección de configuración (<c>AddHubble(configuration.GetSection("Hubble"))</c>).
/// </summary>
public class HubbleOptions
{
    /// <summary>
    /// Nombre por defecto de la sección de configuración (appsettings.json).
    /// </summary>
    public const string SectionName = "Hubble";

    /// <summary>
    /// Edad máxima permitida para <see cref="MaxLogAgeHours"/> (MongoDB admite expireAfterSeconds hasta int.MaxValue segundos).
    /// </summary>
    public const int MaxRetentionHours = int.MaxValue / 3600;

    /// <summary>
    /// Cadena de conexión a MongoDB. Obligatoria.
    /// </summary>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// Base de datos de MongoDB donde Hubble guarda sus colecciones. Obligatoria.
    /// </summary>
    public string DatabaseName { get; set; } = string.Empty;

    /// <summary>
    /// Nombre del servicio que se mostrará en los logs.
    /// </summary>
    public string ServiceName { get; set; } = "HubbleService";

    /// <summary>
    /// Lista de rutas que deben ser ignoradas por el middleware.
    /// </summary>
    public List<string> IgnorePaths { get; set; } = new List<string>();

    /// <summary>
    /// Indica si se deben ignorar las solicitudes a archivos estáticos.
    /// </summary>
    public bool IgnoreStaticFiles { get; set; } = true;

    /// <summary>
    /// Indica si se deben mostrar mensajes de diagnóstico en la consola.
    /// </summary>
    public bool EnableDiagnostics { get; set; } = false;

    /// <summary>
    /// Indica si se capturan los mensajes de ILogger. Requiere registrar el proveedor con <c>AddHubbleLogging()</c>.
    /// </summary>
    public bool CaptureLoggerMessages { get; set; } = true;

    /// <summary>
    /// Nivel mínimo de los mensajes de ILogger que se capturan, salvo que se indique otro en <c>AddHubbleLogging(level)</c>.
    /// </summary>
    public LogLevel MinimumLogLevel { get; set; } = LogLevel.Information;

    /// <summary>
    /// Indica si se deben capturar las solicitudes HTTP.
    /// </summary>
    public bool CaptureHttpRequests { get; set; } = true;

    /// <summary>
    /// Indica si se debe requerir autenticación para acceder a la interfaz de Hubble.
    /// </summary>
    public bool RequireAuthentication { get; set; } = false;

    /// <summary>
    /// Nombre de usuario para la autenticación (si RequireAuthentication es true).
    /// </summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// Contraseña para la autenticación (si RequireAuthentication es true).
    /// </summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// Ruta base para acceder a la interfaz de Hubble. Por defecto es "/hubble".
    /// </summary>
    public string BasePath { get; set; } = "/hubble";

    /// <summary>
    /// Prefijo de ruta para las rutas de Hubble. Por defecto es string.Empty.
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
    /// Activa la retención automática: MongoDB elimina los logs con más de <see cref="MaxLogAgeHours"/> horas
    /// mediante un índice TTL que Hubble crea y mantiene.
    /// </summary>
    public bool EnableDataPrune { get; set; } = false;

    /// <summary>
    /// Sin uso desde que la retención se aplica con un índice TTL (MongoDB revisa los documentos expirados cada minuto).
    /// Se conserva por compatibilidad.
    /// </summary>
    public int DataPruneIntervalHours { get; set; } = 1;

    /// <summary>
    /// Edad máxima en horas de los logs cuando <see cref="EnableDataPrune"/> está activado.
    /// </summary>
    public int MaxLogAgeHours { get; set; } = 24;

    /// <summary>
    /// ID de la zona horaria para mostrar las fechas. Si está vacío, se usará UTC.
    /// </summary>
    public string TimeZoneId { get; set; } = string.Empty;

    /// <summary>
    /// Configuración de seguridad para enmascaramiento de datos sensibles
    /// </summary>
    public SecurityConfiguration Security { get; set; } = new SecurityConfiguration();

    /// <summary>
    /// Indica si se permite eliminar todos los logs desde la interfaz de usuario.
    /// </summary>
    public bool AllowDeleteAll { get; set; } = true;

    /// <summary>
    /// Valida la configuración. Lanza <see cref="InvalidOperationException"/> con todos los errores encontrados,
    /// para que la aplicación falle al arrancar en lugar de funcionar mal o de forma insegura.
    /// </summary>
    internal void Validate()
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(ConnectionString))
        {
            errors.Add("ConnectionString es obligatorio.");
        }

        if (string.IsNullOrWhiteSpace(DatabaseName))
        {
            errors.Add("DatabaseName es obligatorio.");
        }

        if (string.IsNullOrEmpty((BasePath ?? string.Empty).Trim().Trim('/')))
        {
            errors.Add("BasePath no puede estar vacío ni ser \"/\".");
        }

        // Fallar al arrancar en lugar de exponer un dashboard "protegido" sin credenciales
        if (RequireAuthentication && (string.IsNullOrWhiteSpace(Username) || string.IsNullOrEmpty(Password)))
        {
            errors.Add("RequireAuthentication está activado pero Username o Password están vacíos. Configura ambas credenciales o desactiva la autenticación.");
        }

        if (EnableDataPrune && (MaxLogAgeHours < 1 || MaxLogAgeHours > MaxRetentionHours))
        {
            errors.Add($"MaxLogAgeHours debe estar entre 1 y {MaxRetentionHours} cuando EnableDataPrune está activado.");
        }

        if (HighlightDurationSeconds < 0)
        {
            errors.Add("HighlightDurationSeconds no puede ser negativo.");
        }

        if (!string.IsNullOrWhiteSpace(TimeZoneId) && !TimeZoneExists(TimeZoneId))
        {
            errors.Add($"TimeZoneId \"{TimeZoneId}\" no es una zona horaria válida en este sistema.");
        }

        if (errors.Count > 0)
        {
            throw new InvalidOperationException("Configuración de Hubble inválida:" + Environment.NewLine + "- " +
                                                string.Join(Environment.NewLine + "- ", errors));
        }
    }

    private static bool TimeZoneExists(string timeZoneId)
    {
        try
        {
            TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            return true;
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            return false;
        }
    }
}

/// <summary>
/// Configuración de seguridad para Hubble
/// </summary>
public class SecurityConfiguration
{
    /// <summary>
    /// Claves que activan el enmascaramiento en el JSON body de las solicitudes (request) y respuestas (response).
    /// </summary>
    public List<string> MaskBodyProperties { get; set; } = new List<string> { "password", "token", "cuentaOrigen", "tarjeta", "cvv" };

    /// <summary>
    /// Claves adicionales que activan el enmascaramiento específicamente en el JSON body de las solicitudes (request).
    /// Estas se combinan con MaskBodyProperties para el enmascaramiento de solicitudes.
    /// </summary>
    public List<string> MaskRequestBodyProperties { get; set; } = new List<string>();

    /// <summary>
    /// Claves adicionales que activan el enmascaramiento específicamente en el JSON body de las respuestas (response).
    /// Estas se combinan con MaskBodyProperties para el enmascaramiento de respuestas.
    /// </summary>
    public List<string> MaskResponseBodyProperties { get; set; } = new List<string>();

    /// <summary>
    /// Headers que nunca se mostrarán completos
    /// </summary>
    public List<string> MaskHeaders { get; set; } = new List<string> { "Authorization", "X-Api-Key", "Cookie" };

    /// <summary>
    /// IPs o rangos CIDR (IPv4/IPv6) que pueden acceder al dashboard y a la API de Hubble.
    /// No afecta al resto de la aplicación. Vacío o "*" permite cualquier IP.
    /// </summary>
    public List<string> AllowedIps { get; set; } = new List<string>();
}
