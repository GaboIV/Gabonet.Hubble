namespace Gabonet.Hubble.Logging;

using Gabonet.Hubble.Middleware;
using Gabonet.Hubble.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.IO;

/// <summary>
/// Proveedor de logs personalizado para integrar ILogger con Hubble.
/// Los logs se encolan en memoria y se guardan en segundo plano: escribir un log nunca espera a MongoDB.
/// </summary>
public class HubbleLoggerProvider : ILoggerProvider
{
    private readonly HubbleLogQueue _queue;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly HubbleOptions _options;
    private readonly LogLevel _minimumLevel;

    /// <summary>
    /// Constructor del proveedor de logs.
    /// </summary>
    /// <param name="queue">Cola de logs pendientes de guardar</param>
    /// <param name="httpContextAccessor">Acceso al contexto HTTP para asociar los logs a la solicitud en curso</param>
    /// <param name="options">Opciones de Hubble</param>
    /// <param name="minimumLevel">Nivel mínimo de log a capturar</param>
    public HubbleLoggerProvider(
        HubbleLogQueue queue,
        IHttpContextAccessor httpContextAccessor,
        HubbleOptions options,
        LogLevel minimumLevel = LogLevel.Information)
    {
        _queue = queue;
        _httpContextAccessor = httpContextAccessor;
        _options = options;
        _minimumLevel = minimumLevel;
    }

    /// <inheritdoc />
    public ILogger CreateLogger(string categoryName)
    {
        return new HubbleLogger(categoryName, _queue, _httpContextAccessor, _options, _minimumLevel);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        // No hay recursos que liberar
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Logger que encola en Hubble los mensajes de una categoría.
    /// </summary>
    public class HubbleLogger : ILogger
    {
        private readonly string _categoryName;
        private readonly HubbleLogQueue _queue;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly HubbleOptions _options;
        private readonly LogLevel _minimumLevel;
        private readonly bool _isHubbleCategory;

        /// <summary>
        /// Constructor del logger.
        /// </summary>
        public HubbleLogger(
            string categoryName,
            HubbleLogQueue queue,
            IHttpContextAccessor httpContextAccessor,
            HubbleOptions options,
            LogLevel minimumLevel)
        {
            _categoryName = categoryName;
            _queue = queue;
            _httpContextAccessor = httpContextAccessor;
            _options = options;
            _minimumLevel = minimumLevel;
            // Los logs internos de Hubble no se capturan: evita bucles cuando MongoDB falla
            _isHubbleCategory = categoryName.StartsWith("Gabonet.Hubble", StringComparison.Ordinal);
        }

        public IDisposable BeginScope<TState>(TState state)
        {
            return NullScope.Instance;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return _options.CaptureLoggerMessages &&
                   !_isHubbleCategory &&
                   logLevel != LogLevel.None &&
                   logLevel >= _minimumLevel;
        }

        // Implementación requerida por la interfaz ILogger
        public void Log<TState>(
            LogLevel logLevel, 
            EventId eventId, 
            TState state, 
            Exception? exception, 
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
                return;

            // Obtener el mensaje formateado del state y exception
            string message = formatter(state, exception);

            // Extraer información de origen del stack trace
            var sourceInfo = GetSourceInfoFromStackTrace();
            
            // Agregar información de origen al mensaje si se encontró
            string fullMessage = message;
            if (!string.IsNullOrEmpty(sourceInfo))
            {
                fullMessage = $"{message} (File: {sourceInfo})";
            }

            Enqueue(logLevel, fullMessage, exception);
        }

        // Método adicional que permite especificar archivo y línea
        public void LogWithSourceInfo(
            LogLevel logLevel,
            EventId eventId,
            string message,
            Exception? exception = null,
            string sourceFile = "",
            int sourceLine = 0,
            string methodName = "")
        {
            if (!IsEnabled(logLevel))
                return;

            // Agregar archivo y línea al mensaje del log si se proporcionan
            string fullMessage = message;
            if (!string.IsNullOrEmpty(sourceFile) && sourceLine > 0)
            {
                // Si también tenemos el nombre del método, lo incluimos
                if (!string.IsNullOrEmpty(methodName))
                {
                    fullMessage = $"{message} (File: {Path.GetFileName(sourceFile)}, Line: {sourceLine}, Method: {methodName})";
                }
                else
                {
                    fullMessage = $"{message} (File: {Path.GetFileName(sourceFile)}, Line: {sourceLine})";
                }
            }
            else if (!string.IsNullOrEmpty(methodName))
            {
                // Si solo tenemos el nombre del método
                fullMessage = $"{message} (Method: {methodName})";
            }

            Enqueue(logLevel, fullMessage, exception);
        }

        /// <summary>
        /// Construye la entrada en el hilo que escribe el log (donde el HttpContext es el de la solicitud en curso)
        /// y la encola sin bloquear. Si la cola está llena, el log se descarta.
        /// </summary>
        private void Enqueue(LogLevel logLevel, string message, Exception? exception)
        {
            try
            {
                var entry = HubbleLogEntryFactory.CreateApplicationLog(
                    _options.ServiceName,
                    _categoryName,
                    logLevel,
                    message,
                    exception,
                    _httpContextAccessor.HttpContext);

                _queue.TryEnqueue(entry);
            }
            catch
            {
                // Registrar un log nunca debe hacer fallar a la aplicación
            }
        }

        /// <summary>
        /// Obtiene información de origen (archivo y línea) analizando el stack trace
        /// </summary>
        private string GetSourceInfoFromStackTrace()
        {
            try
            {
                // Obtener el stack trace actual
                var stackTrace = new StackTrace(true);
                
                // Buscar el primer frame que no sea parte del sistema de logging
                for (int i = 0; i < stackTrace.FrameCount; i++)
                {
                    var frame = stackTrace.GetFrame(i);
                    if (frame == null) continue;
                    
                    var method = frame.GetMethod();
                    if (method == null) continue;
                    
                    string? declaringTypeName = method.DeclaringType?.FullName;
                    
                    // Ignorar frames de nuestro propio sistema de logging y de Microsoft.Extensions.Logging
                    if (declaringTypeName != null && 
                        !declaringTypeName.StartsWith("Gabonet.Hubble.Logging") &&
                        !declaringTypeName.StartsWith("Microsoft.Extensions.Logging") &&
                        !declaringTypeName.StartsWith("System."))
                    {
                        string? fileName = frame.GetFileName();
                        int line = frame.GetFileLineNumber();
                        string methodName = method.Name;
                        
                        if (!string.IsNullOrEmpty(fileName) && line > 0)
                        {
                            return $"{Path.GetFileName(fileName)}, Line: {line}, Method: {methodName}";
                        }
                        else if (!string.IsNullOrEmpty(methodName))
                        {
                            // Si no podemos obtener el nombre del archivo, al menos devolvemos el nombre del método
                            return $"Method: {declaringTypeName}.{methodName}";
                        }
                    }
                }
                
                // Si no encontramos un frame adecuado, devolver vacío
                return string.Empty;
            }
            catch
            {
                // En caso de error al obtener el stack trace, no fallar
                return string.Empty;
            }
        }

        private class NullScope : IDisposable
        {
            public static NullScope Instance { get; } = new NullScope();

            private NullScope() { }

            public void Dispose() { }
        }
    }
} 