namespace Gabonet.Hubble.Middleware;

using Gabonet.Hubble.Extensions;
using Gabonet.Hubble.Models;
using Gabonet.Hubble.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using MongoDB.Bson;
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
/// Los logs se encolan en memoria y se guardan en segundo plano: la solicitud nunca espera a MongoDB.
/// </summary>
public class HubbleMiddleware
{
    private readonly RequestDelegate _next;
    private readonly HubbleOptions _options;
    private readonly HubbleLogQueue _logQueue;

    /// <summary>
    /// Constructor del middleware de Hubble.
    /// </summary>
    /// <param name="next">Siguiente middleware en la cadena</param>
    /// <param name="options">Opciones de configuración</param>
    /// <param name="logQueue">Cola de logs pendientes de guardar</param>
    public HubbleMiddleware(
        RequestDelegate next,
        HubbleOptions options,
        HubbleLogQueue logQueue)
    {
        _next = next;
        _options = options;
        _logQueue = logQueue;

        // Mostrar información de inicialización
        Console.WriteLine($"[Hubble] Servicio inicializado para: {options.ServiceName}");
        Console.WriteLine($"[Hubble] Limpieza automática de datos: {(options.EnableDataPrune ? "Activada" : "Desactivada")}");

        if (options.EnableDataPrune)
        {
            Console.WriteLine($"[Hubble] Se conservarán logs de las últimas {options.MaxLogAgeHours} hora(s) (índice TTL de MongoDB)");
        }
    }

    /// <summary>
    /// Método principal del middleware que procesa la solicitud HTTP.
    /// </summary>
    /// <param name="context">Contexto HTTP</param>
    /// <returns>Tarea asíncrona</returns>
    public async Task InvokeAsync(HttpContext context)
    {
        // Verificar si la ruta actual debe ser ignorada.
        // Nota: el filtro de IPs (Security.AllowedIps) solo se aplica al dashboard en HubbleUIMiddleware;
        // este middleware nunca bloquea solicitudes de la aplicación.
        var path = context.Request.Path.Value?.ToLower();
        if (!_options.CaptureHttpRequests || ShouldIgnoreRequest(context, path))
        {
            await _next(context);
            return;
        }

        var stopwatch = Stopwatch.StartNew();

        // El Id se genera en memoria, sin consultar MongoDB: los logs de ILogger pueden asociarse
        // a esta solicitud desde el primer momento
        var requestLog = new GeneralLog
        {
            Id = ObjectId.GenerateNewId().ToString(),
            ServiceName = _options.ServiceName,
            HttpUrl = context.Request.Path,
            QueryParams = context.Request.QueryString.Value ?? string.Empty,
            Method = context.Request.Method,
            RequestData = await FormatRequest(context.Request),
            RequestHeaders = FormatHeaders(context.Request.Headers),
            IpAddress = HubbleLogEntryFactory.GetClientIpAddress(context),
            Timestamp = DateTime.UtcNow
        };
        context.Items[HubbleLogEntryFactory.RequestLogItemKey] = requestLog;

        var originalBodyStream = context.Response.Body;
        using var responseBody = new MemoryStream();
        context.Response.Body = responseBody;
        var failed = false;

        try
        {
            // Ejecutar el siguiente middleware en la cadena
            await _next(context);

            requestLog.StatusCode = context.Response.StatusCode;
            requestLog.ResponseData = await FormatResponse(context.Response);
        }
        catch (Exception ex)
        {
            failed = true;
            requestLog.StatusCode = context.Response.HasStarted ? context.Response.StatusCode : StatusCodes.Status500InternalServerError;
            requestLog.IsError = true;
            requestLog.ErrorMessage = ex.Message;
            requestLog.StackTrace = ex.StackTrace;

            if (_options.EnableDiagnostics)
            {
                Console.WriteLine($"HubbleMiddleware: Error: {ex.Message}");
                Console.WriteLine($"HubbleMiddleware: StackTrace: {ex.StackTrace}");
            }

            throw; // Propagamos la excepción original
        }
        finally
        {
            stopwatch.Stop();
            CompleteAndEnqueue(context, requestLog, stopwatch.ElapsedMilliseconds);

            // Restaurar el stream original para que los middlewares externos (por ejemplo, un manejador
            // de excepciones) escriban en la respuesta real y no en el buffer ya liberado
            context.Response.Body = originalBodyStream;

            if (!failed)
            {
                // Copiar la respuesta al stream original
                responseBody.Position = 0;
                await responseBody.CopyToAsync(originalBodyStream);
            }
        }
    }

    /// <summary>
    /// Completa el log con los datos finales de la solicitud y lo encola. Nunca lanza excepciones.
    /// </summary>
    private void CompleteAndEnqueue(HttpContext context, GeneralLog requestLog, long executionTime)
    {
        try
        {
            requestLog.ExecutionTime = executionTime;
            requestLog.QueryParams = context.Request.QueryString.Value ?? string.Empty;
            requestLog.DatabaseQueries = context.GetDatabaseQueries().Select(q => q.ToDatabaseQuery()).ToList();

            // Obtener información de controlador y acción si está disponible
            var routeValues = context.GetRouteData()?.Values;
            requestLog.ControllerName = routeValues?["controller"]?.ToString() ?? "Unknown";
            requestLog.ActionName = routeValues?["action"]?.ToString() ?? "Unknown";

            if (!_logQueue.TryEnqueue(requestLog) && _options.EnableDiagnostics)
            {
                Console.WriteLine("HubbleMiddleware: cola de logs llena, se descartó el log de la solicitud");
            }
        }
        catch (Exception ex)
        {
            // No propagamos la excepción para que no afecte la respuesta al cliente
            if (_options.EnableDiagnostics)
            {
                Console.WriteLine($"HubbleMiddleware: Error al registrar la solicitud: {ex.Message}");
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

        // Ignorar rutas de Hubble (comparación por segmentos: "/hubble" no coincide con "/hubblefoo")
        var hubbleBasePath = "/" + (_options.BasePath ?? string.Empty).Trim().Trim('/');
        if (context.Request.Path.StartsWithSegments(hubbleBasePath, StringComparison.OrdinalIgnoreCase) ||
            context.Request.Path.StartsWithSegments("/api/hubble", StringComparison.OrdinalIgnoreCase))
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
            var requestBody = Encoding.UTF8.GetString(buffer);

            // Combinar propiedades de body y request body para el enmascaramiento de la solicitud
            var maskProperties = _options.Security.MaskBodyProperties
                .Union(_options.Security.MaskRequestBodyProperties)
                .ToList();

            // Aplicar enmascaramiento de datos sensibles usando las propiedades configuradas
            return MaskJsonBody(requestBody, maskProperties);
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

            // Combinar propiedades de body y response body para el enmascaramiento de la respuesta
            var maskProperties = _options.Security.MaskBodyProperties
                .Union(_options.Security.MaskResponseBodyProperties)
                .ToList();

            // Aplicar enmascaramiento de datos sensibles
            return MaskJsonBody(text, maskProperties);
        }
        catch
        {
            return string.Empty;
        }
    }

    private string FormatHeaders(IHeaderDictionary headers)
    {
        var formattedHeaders = headers.ToDictionary(h => h.Key, h => h.Value.ToString());

        // Enmascarar headers sensibles (case-insensitive)
        foreach (var headerKey in _options.Security.MaskHeaders)
        {
            var keyToMask = formattedHeaders.Keys.FirstOrDefault(k => k.Equals(headerKey, StringComparison.OrdinalIgnoreCase));
            if (keyToMask != null)
            {
                formattedHeaders[keyToMask] = "*****";
            }
        }

        return JsonConvert.SerializeObject(formattedHeaders);
    }

    private string MaskJsonBody(string jsonBody, List<string> maskProperties)
    {
        if (string.IsNullOrEmpty(jsonBody))
        {
            return jsonBody;
        }

        try
        {
            var jsonObject = JsonConvert.DeserializeObject(jsonBody);
            if (jsonObject == null)
            {
                return jsonBody;
            }

            MaskJsonObject(jsonObject, maskProperties);
            return JsonConvert.SerializeObject(jsonObject);
        }
        catch
        {
            // Si no es JSON válido, devolver sin cambios
            return jsonBody;
        }
    }

    private void MaskJsonObject(object obj, List<string> maskProperties)
    {
        if (obj is Newtonsoft.Json.Linq.JObject jObject)
        {
            foreach (var property in jObject.Properties().ToList())
            {
                // Verificar si la propiedad debe ser enmascarada (case-insensitive)
                if (maskProperties.Contains(property.Name, StringComparer.OrdinalIgnoreCase))
                {
                    property.Value = "*****";
                }
                else
                {
                    MaskJsonObject(property.Value, maskProperties);
                }
            }
        }
        else if (obj is Newtonsoft.Json.Linq.JArray jArray)
        {
            foreach (var item in jArray)
            {
                MaskJsonObject(item, maskProperties);
            }
        }
    }
}
