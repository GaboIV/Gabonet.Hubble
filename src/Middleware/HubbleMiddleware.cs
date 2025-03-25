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

        using (var responseBody = new MemoryStream())
        {
            context.Response.Body = responseBody;

            try
            {
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

                        // Registrar el log general
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
        var ipAddress = _httpContextAccessor.HttpContext?.Connection?.RemoteIpAddress?.ToString();

        if (ipAddress == "::1" || ipAddress == "127.0.0.1")
        {
            ipAddress = "Localhost";
        }
        else if (string.IsNullOrEmpty(ipAddress))
        {
            ipAddress = "IP not available";
        }

        var headers = context.Request.Headers.ToDictionary(h => h.Key, h => h.Value.ToString());
        var headersJson = JsonConvert.SerializeObject(headers);

        var generalLog = new GeneralLog
        {
            Timestamp = DateTime.UtcNow,
            HttpUrl = context.Request.Path + context.Request.QueryString,
            ControllerName = context.GetRouteData()?.Values["controller"]?.ToString() ?? "",
            ActionName = context.GetRouteData()?.Values["action"]?.ToString() ?? "",
            Method = context.Request.Method,
            RequestHeaders = headersJson,
            RequestData = request,
            ResponseData = response,
            StatusCode = context.Response.StatusCode,
            IsError = isError,
            ErrorMessage = errorMessage,
            StackTrace = stackTrace,
            ServiceName = _options.ServiceName,
            IpAddress = ipAddress,
            DatabaseQueries = databaseQueries.Select(q => q.ToDatabaseQuery()).ToList(),
            ExecutionTime = executionTime
        };

        // Verificar si hay un error en la respuesta JSON
        if (!string.IsNullOrEmpty(response) && context.Response.ContentType?.Contains("application/json") == true)
        {
            try
            {
                var responseJson = JsonConvert.DeserializeObject<Dictionary<string, object>>(response);
                if (responseJson != null && responseJson.ContainsKey("success") && (bool)responseJson["success"] == false)
                {
                    generalLog.ErrorMessage = responseJson.ContainsKey("message") ? responseJson["message"].ToString() : "Error desconocido";
                    generalLog.IsError = true;
                }
            }
            catch (JsonReaderException)
            {
                // Ignorar errores al deserializar JSON
            }
        }

        await hubbleService.CreateLogAsync(generalLog);
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
    /// Ruta base para la interfaz de usuario de Hubble.
    /// </summary>
    public string BasePath { get; set; } = "/hubble";
} 