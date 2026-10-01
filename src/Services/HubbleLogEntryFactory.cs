namespace Gabonet.Hubble.Services;

using Gabonet.Hubble.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System;
using System.Text.RegularExpressions;

/// <summary>
/// Construcción de las entradas de log que comparten el middleware, el proveedor de ILogger y <see cref="HubbleService"/>.
/// </summary>
internal static class HubbleLogEntryFactory
{
    /// <summary>
    /// Clave de <see cref="HttpContext.Items"/> donde el middleware guarda el log de la solicitud en curso,
    /// para que los logs de ILogger puedan asociarse a ella.
    /// </summary>
    public const string RequestLogItemKey = "Hubble_RequestLog";

    private static readonly Regex FileLineMethodPattern = new(@"\(File: ([^,]+), Line: (\d+), Method: ([^\)]+)\)$", RegexOptions.Compiled);
    private static readonly Regex FileLinePattern = new(@"\(File: ([^,]+), Line: (\d+)\)$", RegexOptions.Compiled);
    private static readonly Regex MethodPattern = new(@"\(File: Method: ([^\)]+)\)$", RegexOptions.Compiled);

    /// <summary>
    /// Obtiene la dirección IP del cliente en el formato que muestra el dashboard.
    /// </summary>
    public static string GetClientIpAddress(HttpContext? httpContext)
    {
        var ipAddress = httpContext?.Connection?.RemoteIpAddress?.ToString();

        if (ipAddress == "::1" || ipAddress == "127.0.0.1")
        {
            return "Localhost";
        }

        return string.IsNullOrEmpty(ipAddress) ? "IP not available" : ipAddress;
    }

    /// <summary>
    /// Crea la entrada de un log de ILogger. Si hay una solicitud HTTP en curso capturada por Hubble,
    /// la entrada queda asociada a ella mediante <see cref="GeneralLog.RelatedRequestId"/>.
    /// </summary>
    public static GeneralLog CreateApplicationLog(
        string serviceName,
        string category,
        LogLevel logLevel,
        string message,
        Exception? exception,
        HttpContext? httpContext)
    {
        var (sourceInfo, cleanMessage) = ExtractSourceInfo(message);
        GeneralLog? requestLog = null;
        if (httpContext != null && httpContext.Items.TryGetValue(RequestLogItemKey, out var item))
        {
            requestLog = item as GeneralLog;
        }

        return new GeneralLog
        {
            ServiceName = serviceName,
            ControllerName = "ApplicationLogger",
            // Incluir la información de origen junto al nivel de log si está disponible
            ActionName = sourceInfo.Length > 0 ? $"{logLevel} [{sourceInfo}]" : logLevel.ToString(),
            HttpUrl = httpContext?.Request.Path.ToString() ?? "No URL available",
            Method = httpContext?.Request.Method ?? "No Method",
            RequestData = category,
            ResponseData = cleanMessage,
            StatusCode = logLevel >= LogLevel.Error ? 500 : 200,
            IsError = logLevel >= LogLevel.Error,
            ErrorMessage = exception?.Message,
            StackTrace = exception?.StackTrace,
            IpAddress = GetClientIpAddress(httpContext),
            Timestamp = DateTime.UtcNow,
            ExecutionTime = 0,
            RelatedRequestId = requestLog?.Id
        };
    }

    /// <summary>
    /// Separa la información de origen "(File: ..., Line: ..., Method: ...)" que el logger añade al final del mensaje.
    /// </summary>
    private static (string SourceInfo, string CleanMessage) ExtractSourceInfo(string message)
    {
        var match = FileLineMethodPattern.Match(message);
        if (match.Success)
        {
            return ($"{match.Groups[1].Value}:{match.Groups[2].Value} → {match.Groups[3].Value}", message.Substring(0, match.Index).Trim());
        }

        match = FileLinePattern.Match(message);
        if (match.Success)
        {
            return ($"{match.Groups[1].Value}:{match.Groups[2].Value}", message.Substring(0, match.Index).Trim());
        }

        match = MethodPattern.Match(message);
        if (match.Success)
        {
            return (match.Groups[1].Value, message.Substring(0, match.Index).Trim());
        }

        return (string.Empty, message);
    }
}
