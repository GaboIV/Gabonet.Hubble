namespace Gabonet.Hubble.Middleware;

using Gabonet.Hubble.Extensions;
using Gabonet.Hubble.Interfaces;
using Gabonet.Hubble.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

/// <summary>
/// Middleware para capturar y registrar solicitudes HTTP, respuestas y consultas a bases de datos.
/// </summary>
public class HubbleMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IServiceProvider _serviceProvider;
    private readonly HubbleOptions _options;
    private readonly HubbleDataPruneManager _pruneManager;

    /// <summary>
    /// Constructor del middleware de Hubble.
    /// </summary>
    /// <param name="next">Siguiente middleware en la cadena</param>
    /// <param name="httpContextAccessor">Acceso al contexto HTTP</param>
    /// <param name="serviceProvider">Proveedor de servicios</param>
    /// <param name="options">Opciones de configuración</param>
    public HubbleMiddleware(
        RequestDelegate next, 
        IHttpContextAccessor httpContextAccessor, 
        IServiceProvider serviceProvider,
        HubbleOptions options)
    {
        _next = next;
        _httpContextAccessor = httpContextAccessor;
        _serviceProvider = serviceProvider;
        _options = options;
        _pruneManager = new HubbleDataPruneManager(options);
        
        // Mostrar información de inicialización
        Console.WriteLine($"[Hubble] Servicio inicializado para: {options.ServiceName}");
        Console.WriteLine($"[Hubble] Limpieza automática de datos: {(options.EnableDataPrune ? "Activada" : "Desactivada")}");
        
        if (options.EnableDataPrune)
        {
            Console.WriteLine($"[Hubble] Intervalo de limpieza: {options.DataPruneIntervalHours} hora(s)");
            Console.WriteLine($"[Hubble] Se conservarán logs de las últimas {options.MaxLogAgeHours} hora(s)");
        }
    }

    /// <summary>
    /// Método principal del middleware que procesa la solicitud HTTP.
    /// </summary>
    /// <param name="context">Contexto HTTP</param>
    /// <returns>Tarea asíncrona</returns>
    public async Task InvokeAsync(HttpContext context)
    {
        // Verificar si la ruta actual debe ser ignorada
        var path = context.Request.Path.Value?.ToLower();
        if (ShouldIgnoreRequest(context, path))
        {
            await _next(context);
            return;
        }

        var stopwatch = new Stopwatch();
        stopwatch.Start();

        // Capturar la solicitud
        var request = await FormatRequest(context.Request);
        var originalBodyStream = context.Response.Body;
        GeneralLog? requestLog = null;

        using (var responseBody = new MemoryStream())
        {
            context.Response.Body = responseBody;

            try
            {
                // Registrar el log al principio y guardarlo en el contexto para que
                // los loggers puedan asociarse a él
                try 
                {
                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var hubbleService = scope.ServiceProvider.GetRequiredService<IHubbleService>();
                        
                        // Crear un log inicial que se actualizará más tarde
                        var initialLog = new GeneralLog
                        {
                            ServiceName = _options.ServiceName,
                            HttpUrl = context.Request.Path,
                            QueryParams = context.Request.QueryString.Value ?? string.Empty,
                            Method = context.Request.Method,
                            RequestData = request,
                            RequestHeaders = FormatHeaders(context.Request.Headers),
                            Timestamp = DateTime.UtcNow
                        };
                        
                        // Guardar el log en la base de datos para obtener su ID
                        await hubbleService.CreateLogAsync(initialLog);
                        
                        // Guardar el log en el contexto HTTP
                        context.Items["Hubble_RequestLog"] = initialLog;
                        requestLog = initialLog;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error al crear log inicial: {ex.Message}");
                }

                // Ejecutar el siguiente middleware en la cadena
                await _next(context);

                // Capturar la respuesta
                var response = await FormatResponse(context.Response);
                
                // Obtener las consultas a bases de datos capturadas
                var databaseQueries = context.GetDatabaseQueries();

                try
                {
                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var hubbleService = scope.ServiceProvider.GetRequiredService<IHubbleService>();

                        stopwatch.Stop();
                        var executionTime = stopwatch.ElapsedMilliseconds;

                        // Actualizar el log con la información completa
                        if (requestLog != null && !string.IsNullOrEmpty(requestLog.Id))
                        {
                            // Actualizar el log existente
                            requestLog.ResponseData = response;
                            requestLog.StatusCode = context.Response.StatusCode;
                            requestLog.ExecutionTime = executionTime;
                            requestLog.DatabaseQueries = databaseQueries.Select(q => q.ToDatabaseQuery()).ToList();
                            requestLog.QueryParams = context.Request.QueryString.Value ?? string.Empty;
                            
                            string controllerName = "Unknown";
                            string actionName = "Unknown";
                            
                            // Obtener información de controlador y acción si está disponible
                            var routeData = context.GetRouteData();
                            if (routeData != null)
                            {
                                var controllerValue = routeData.Values["controller"];
                                var actionValue = routeData.Values["action"];

                                if (controllerValue != null)
                                {
                                    controllerName = controllerValue.ToString() ?? "Unknown";
                                }

                                if (actionValue != null)
                                {
                                    actionName = actionValue.ToString() ?? "Unknown";
                                }
                            }
                            
                            requestLog.ControllerName = controllerName;
                            requestLog.ActionName = actionName;
                            
                            await hubbleService.UpdateLogAsync(requestLog.Id, requestLog);
                        }
                        else
                        {
                            // Si por alguna razón no existe el log inicial, crear uno nuevo
                            await LogGeneralAsync(
                                context, 
                                hubbleService, 
                                request, 
                                response, 
                                false, 
                                null, 
                                null, 
                                databaseQueries, 
                                executionTime);
                        }
                        
                        // Ejecutar la limpieza de datos históricos si está habilitada
                        await _pruneManager.TryPruneDataAsync(hubbleService);
                    }
                }
                catch (Exception serviceEx)
                {
                    // No propagamos la excepción para que no afecte la respuesta al cliente
                    if (_options.EnableDiagnostics)
                    {
                        Console.WriteLine($"HubbleMiddleware: Error al obtener HubbleService: {serviceEx.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                if (_options.EnableDiagnostics)
                {
                    Console.WriteLine($"HubbleMiddleware: Error: {ex.Message}");
                    Console.WriteLine($"HubbleMiddleware: StackTrace: {ex.StackTrace}");
                }
                
                try
                {
                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var hubbleService = scope.ServiceProvider.GetRequiredService<IHubbleService>();
                        var databaseQueries = context.GetDatabaseQueries();

                        stopwatch.Stop();
                        var executionTime = stopwatch.ElapsedMilliseconds;

                        // Registrar el log de error
                        if (requestLog != null && !string.IsNullOrEmpty(requestLog.Id))
                        {
                            // Actualizar el log existente con la información de error
                            requestLog.StatusCode = context.Response.StatusCode > 0 ? context.Response.StatusCode : 500;
                            requestLog.IsError = true;
                            requestLog.ErrorMessage = ex.Message;
                            requestLog.StackTrace = ex.StackTrace;
                            requestLog.ExecutionTime = executionTime;
                            requestLog.DatabaseQueries = databaseQueries.Select(q => q.ToDatabaseQuery()).ToList();
                            requestLog.QueryParams = context.Request.QueryString.Value ?? string.Empty;
                            
                            await hubbleService.UpdateLogAsync(requestLog.Id, requestLog);
                        }
                        else
                        {
                            // Si no existe el log inicial, crear uno nuevo
                            await LogGeneralAsync(
                                context, 
                                hubbleService, 
                                request, 
                                null, 
                                true, 
                                ex.Message, 
                                ex.StackTrace, 
                                databaseQueries, 
                                executionTime);
                        }
                        
                        // Ejecutar la limpieza de datos históricos si está habilitada
                        await _pruneManager.TryPruneDataAsync(hubbleService);
                    }
                }
                catch (Exception serviceEx)
                {
                    // No propagamos la excepción para que no afecte la respuesta al cliente
                    if (_options.EnableDiagnostics)
                    {
                        Console.WriteLine($"HubbleMiddleware: Error al obtener HubbleService para error: {serviceEx.Message}");
                    }
                }
                
                throw; // Propagamos la excepción original
            }
            finally
            {
                // Copiar la respuesta al stream original
                await responseBody.CopyToAsync(originalBodyStream);
            }
        }
    }

    private bool ShouldIgnoreRequest(HttpContext context, string? path)
    {
        // Ignorar rutas específicas
        if (_options.IgnorePaths != null && path != null)
        {
            foreach (var ignorePath in _options.IgnorePaths)
            {
                if (path.StartsWith(ignorePath.ToLower()))
                {
                    return true;
                }
            }
        }

        // Ignorar rutas de Hubble
        if (path != null && (path.StartsWith(_options.BasePath.ToLower()) || path.StartsWith("/api/hubble")))
        {
            return true;
        }

        // Ignorar extensiones de archivos estáticos
        if (_options.IgnoreStaticFiles && path != null)
        {
            var extension = Path.GetExtension(path).ToLower();
            var staticExtensions = new[] { ".css", ".js", ".jpg", ".jpeg", ".png", ".gif", ".ico", ".svg", ".woff", ".woff2", ".ttf", ".eot" };
            
            if (staticExtensions.Contains(extension))
            {
                return true;
            }
        }

        return false;
    }

    private async Task LogGeneralAsync(
        HttpContext context, 
        IHubbleService hubbleService, 
        string request, 
        string? response, 
        bool isError, 
        string? errorMessage, 
        string? stackTrace, 
        List<DatabaseQueryLog> databaseQueries, 
        long executionTime)
    {
        // Si no está habilitada la captura de logs HTTP, salir
        if (!_options.CaptureHttpRequests)
        {
            return;
        }

        var ipAddress = _httpContextAccessor.HttpContext?.Connection?.RemoteIpAddress?.ToString();
        
        if (ipAddress == "::1" || ipAddress == "127.0.0.1")
        {
            ipAddress = "Localhost";
        }
        else if (string.IsNullOrEmpty(ipAddress))
        {
            ipAddress = "IP not available";
        }

        string controllerName = "Unknown";
        string actionName = "Unknown";

        // Obtener información de controlador y acción si está disponible
        var routeData = context.GetRouteData();
        if (routeData != null)
        {
            var controllerValue = routeData.Values["controller"];
            var actionValue = routeData.Values["action"];

            if (controllerValue != null)
            {
                controllerName = controllerValue.ToString() ?? "Unknown";
            }

            if (actionValue != null)
            {
                actionName = actionValue.ToString() ?? "Unknown";
            }
        }

        var log = new GeneralLog
        {
            ServiceName = _options.ServiceName,
            ControllerName = controllerName,
            ActionName = actionName,
            HttpUrl = context.Request.Path,
            QueryParams = context.Request.QueryString.Value ?? string.Empty,
            Method = context.Request.Method,
            RequestData = request,
            ResponseData = response,
            RequestHeaders = FormatHeaders(context.Request.Headers),
            StatusCode = context.Response.StatusCode,
            IsError = isError,
            ErrorMessage = errorMessage,
            StackTrace = stackTrace,
            IpAddress = ipAddress,
            Timestamp = DateTime.UtcNow,
            ExecutionTime = executionTime,
            DatabaseQueries = databaseQueries.Select(q => q.ToDatabaseQuery()).ToList()
        };

        // Guardar el log en la base de datos
        await hubbleService.CreateLogAsync(log);
        
        // Guardar el log en el contexto HTTP para que los logs de ILogger puedan referenciarlo
        context.Items["Hubble_RequestLog"] = log;
    }

    private async Task<string> FormatRequest(HttpRequest request)
    {
        if (request.ContentLength == null || request.ContentLength == 0)
        {
            return string.Empty;
        }

        try
        {
            request.EnableBuffering();
            var buffer = new byte[Convert.ToInt32(request.ContentLength)];
            await request.Body.ReadAsync(buffer, 0, buffer.Length);
            request.Body.Position = 0;
            return Encoding.UTF8.GetString(buffer);
        }
        catch
        {
            request.Body.Position = 0;
            return string.Empty;
        }
    }

    private async Task<string> FormatResponse(HttpResponse response)
    {
        try
        {
            response.Body.Seek(0, SeekOrigin.Begin);
            var text = await new StreamReader(response.Body).ReadToEndAsync();
            response.Body.Seek(0, SeekOrigin.Begin);
            return text;
        }
        catch
        {
            return string.Empty;
        }
    }
    
    private string FormatHeaders(IHeaderDictionary headers)
    {
        var formattedHeaders = headers.ToDictionary(h => h.Key, h => h.Value.ToString());
        return JsonConvert.SerializeObject(formattedHeaders);
    }
}

/// <summary>
/// Opciones de configuración para el middleware de Hubble.
/// </summary>
public class HubbleOptions
{
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
    /// Indica si se deben capturar los mensajes de ILogger.
    /// </summary>
    public bool CaptureLoggerMessages { get; set; } = false;

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
    /// Indica si se deben destacar los nuevos servicios que se van agregando en tiempo real.
    /// </summary>
    public bool HighlightNewServices { get; set; } = false;
    
    /// <summary>
    /// Duración en segundos que los nuevos servicios permanecerán destacados. Por defecto es 5 segundos.
    /// </summary>
    public int HighlightDurationSeconds { get; set; } = 5;
    
    /// <summary>
    /// Activa o desactiva el sistema de limpieza automática de logs antiguos.
    /// </summary>
    public bool EnableDataPrune { get; set; } = false;
    
    /// <summary>
    /// Intervalo en horas entre cada ejecución del proceso de limpieza de logs.
    /// </summary>
    public int DataPruneIntervalHours { get; set; } = 1;
    
    /// <summary>
    /// Edad máxima en horas que se conservarán los logs antes de ser eliminados.
    /// </summary>
    public int MaxLogAgeHours { get; set; } = 24;
    
    /// <summary>
    /// ID de la zona horaria para mostrar las fechas. Si está vacío, se usará UTC.
    /// </summary>
    public string TimeZoneId { get; set; } = string.Empty;
} 