namespace Gabonet.Hubble.Models;

/// <summary>
/// Configuración de autenticación para Hubble que se puede cargar desde appsettings.json
/// </summary>
public class HubbleAuthConfiguration
{
    /// <summary>
    /// Sección en appsettings.json donde se encuentra la configuración de Hubble
    /// </summary>
    public const string SectionName = "Hubble";

    /// <summary>
    /// Indica si se debe requerir autenticación para acceder a la interfaz de Hubble
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
    /// Ruta base para acceder a la interfaz de Hubble. Por defecto es "/hubble"
    /// </summary>
    public string BasePath { get; set; } = "/hubble";

    /// <summary>
    /// Prefijo de ruta para las rutas de Hubble. Por defecto es string.Empty
    /// </summary>
    public string PrefixPath { get; set; } = string.Empty;

    /// <summary>
    /// Nombre del servicio que se mostrará en los logs
    /// </summary>
    public string ServiceName { get; set; } = "HubbleService";

    /// <summary>
    /// Indica si se deben mostrar mensajes de diagnóstico en la consola
    /// </summary>
    public bool EnableDiagnostics { get; set; } = false;

    /// <summary>
    /// Indica si se deben capturar los mensajes de ILogger
    /// </summary>
    public bool CaptureLoggerMessages { get; set; } = false;

    /// <summary>
    /// Indica si se deben capturar las solicitudes HTTP
    /// </summary>
    public bool CaptureHttpRequests { get; set; } = true;

    /// <summary>
    /// Lista de rutas que deben ser ignoradas por el middleware
    /// </summary>
    public List<string> IgnorePaths { get; set; } = new List<string>();

    /// <summary>
    /// Indica si se deben ignorar las solicitudes a archivos estáticos
    /// </summary>
    public bool IgnoreStaticFiles { get; set; } = true;

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

    /// <summary>
    /// ID de la zona horaria para mostrar las fechas. Si está vacío, se usará UTC
    /// </summary>
    public string TimeZoneId { get; set; } = string.Empty;

    /// <summary>
    /// Indica si se deben destacar los nuevos servicios que se van agregando en tiempo real
    /// </summary>
    public bool HighlightNewServices { get; set; } = false;

    /// <summary>
    /// Duración en segundos que los nuevos servicios permanecerán destacados
    /// </summary>
    public int HighlightDurationSeconds { get; set; } = 5;

    /// <summary>
    /// Configuración de seguridad para enmascaramiento de datos sensibles
    /// </summary>
    public SecurityConfiguration Security { get; set; } = new SecurityConfiguration();
}

/// <summary>
/// Configuración de seguridad para Hubble
/// </summary>
public class SecurityConfiguration
{
    /// <summary>
    /// Claves que activan el enmascaramiento en el JSON body
    /// </summary>
    public List<string> MaskBodyProperties { get; set; } = new List<string> { "password", "token", "cuentaOrigen", "tarjeta", "cvv" };

    /// <summary>
    /// Headers que nunca se mostrarán completos
    /// </summary>
    public List<string> MaskHeaders { get; set; } = new List<string> { "Authorization", "X-Api-Key", "Cookie" };

    /// <summary>
    /// Solo permitir acceso desde estas IPs (VPN/Oficina)
    /// </summary>
    public List<string> AllowedIps { get; set; } = new List<string> { "127.0.0.1" };
}
