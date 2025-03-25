namespace Gabonet.Hubble.Logging;

using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

/// <summary>
/// Extensiones para ILogger para capturar información del origen del log.
/// </summary>
public static class HubbleLoggerExtensions
{
    /// <summary>
    /// Registra un log con nivel de información incluyendo información del archivo y línea de origen.
    /// </summary>
    public static void LogInformationWithSource(
        this ILogger logger,
        string message,
        params object[] args)
    {
        LogWithSource(logger, LogLevel.Information, 0, null, message, args);
    }

    /// <summary>
    /// Registra un log con nivel de advertencia incluyendo información del archivo y línea de origen.
    /// </summary>
    public static void LogWarningWithSource(
        this ILogger logger,
        string message,
        params object[] args)
    {
        LogWithSource(logger, LogLevel.Warning, 0, null, message, args);
    }

    /// <summary>
    /// Registra un log con nivel de error incluyendo información del archivo y línea de origen.
    /// </summary>
    public static void LogErrorWithSource(
        this ILogger logger,
        string message,
        params object[] args)
    {
        LogWithSource(logger, LogLevel.Error, 0, null, message, args);
    }

    /// <summary>
    /// Registra un log con nivel de error y excepción incluyendo información del archivo y línea de origen.
    /// </summary>
    public static void LogErrorWithSource(
        this ILogger logger,
        Exception exception,
        string message,
        params object[] args)
    {
        LogWithSource(logger, LogLevel.Error, 0, exception, message, args);
    }

    /// <summary>
    /// Registra un log con nivel de depuración incluyendo información del archivo y línea de origen.
    /// </summary>
    public static void LogDebugWithSource(
        this ILogger logger,
        string message,
        params object[] args)
    {
        LogWithSource(logger, LogLevel.Debug, 0, null, message, args);
    }

    /// <summary>
    /// Registra un log con nivel de detalle incluyendo información del archivo y línea de origen.
    /// </summary>
    public static void LogTraceWithSource(
        this ILogger logger,
        string message,
        params object[] args)
    {
        LogWithSource(logger, LogLevel.Trace, 0, null, message, args);
    }

    /// <summary>
    /// Registra un log con nivel crítico incluyendo información del archivo y línea de origen.
    /// </summary>
    public static void LogCriticalWithSource(
        this ILogger logger,
        string message,
        params object[] args)
    {
        LogWithSource(logger, LogLevel.Critical, 0, null, message, args);
    }

    /// <summary>
    /// Registra un log con nivel crítico y excepción incluyendo información del archivo y línea de origen.
    /// </summary>
    public static void LogCriticalWithSource(
        this ILogger logger,
        Exception exception,
        string message,
        params object[] args)
    {
        LogWithSource(logger, LogLevel.Critical, 0, exception, message, args);
    }

    // Método privado para implementar la lógica común
    private static void LogWithSource(
        ILogger logger,
        LogLevel logLevel,
        EventId eventId,
        Exception? exception,
        string message,
        object[] args,
        [CallerFilePath] string filePath = "",
        [CallerLineNumber] int lineNumber = 0,
        [CallerMemberName] string memberName = "")
    {
        // Obtenemos el nombre del archivo para tener un mensaje más limpio
        string fileName = System.IO.Path.GetFileName(filePath);
        
        // Formatear el mensaje con los argumentos
        string formattedMessage;
        try
        {
            formattedMessage = string.Format(message, args);
        }
        catch (FormatException)
        {
            // En caso de error de formato, usamos el mensaje original
            formattedMessage = message;
        }
        
        // Si el logger es HubbleLogger, usamos su método especializado
        if (logger is HubbleLoggerProvider.HubbleLogger hubbleLogger)
        {
            // Intentar conseguir más información sobre el método llamador
            string callerInfo = GetCallerInfo(memberName);
            
            // Usar el método especializado de HubbleLogger
            hubbleLogger.LogWithSourceInfo(
                logLevel, 
                eventId, 
                formattedMessage, 
                exception, 
                filePath, 
                lineNumber,
                callerInfo);
        }
        else
        {
            // Para otros loggers, incorporamos la información de origen en el mensaje
            string messageWithSource = $"{formattedMessage} (File: {fileName}, Line: {lineNumber}, Method: {memberName})";
            logger.Log(logLevel, eventId, messageWithSource, exception, (s, e) => s.ToString());
        }
    }
    
    /// <summary>
    /// Obtiene información adicional sobre el llamador usando StackTrace
    /// </summary>
    private static string GetCallerInfo(string memberName)
    {
        try
        {
            // Obtener stack trace para información adicional
            var stackTrace = new StackTrace(true);
            
            // Buscar el frame correcto que no sea parte del sistema de logging
            for (int i = 0; i < stackTrace.FrameCount; i++)
            {
                var frame = stackTrace.GetFrame(i);
                if (frame == null) continue;
                
                var method = frame.GetMethod();
                if (method == null) continue;
                
                // Si encontramos el método con el nombre que nos pasaron, devolvemos el tipo declarante
                if (method.Name == memberName && method.DeclaringType != null &&
                    !method.DeclaringType.FullName.StartsWith("Gabonet.Hubble.Logging") &&
                    !method.DeclaringType.FullName.StartsWith("Microsoft.Extensions.Logging"))
                {
                    return $"{method.DeclaringType.FullName}.{memberName}";
                }
            }
            
            return memberName;
        }
        catch
        {
            // En caso de error, devolvemos el nombre del miembro original
            return memberName;
        }
    }
} 