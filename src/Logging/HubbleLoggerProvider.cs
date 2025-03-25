namespace Gabonet.Hubble.Logging;

using Gabonet.Hubble.Interfaces;
using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

/// <summary>
/// Proveedor de logs personalizado para integrar ILogger con Hubble.
/// </summary>
public class HubbleLoggerProvider : ILoggerProvider
{
    private readonly Func<IHubbleService> _hubbleServiceFactory;
    private readonly LogLevel _minimumLevel;

    /// <summary>
    /// Constructor del proveedor de logs.
    /// </summary>
    /// <param name="hubbleService">Servicio de Hubble</param>
    /// <param name="minimumLevel">Nivel mínimo de log a capturar</param>
    public HubbleLoggerProvider(IHubbleService hubbleService, LogLevel minimumLevel = LogLevel.Information)
        : this(() => hubbleService, minimumLevel)
    {
    }

    /// <summary>
    /// Constructor del proveedor de logs con factory para resolver IHubbleService en el scope correcto.
    /// </summary>
    /// <param name="hubbleServiceFactory">Factory para obtener el servicio de Hubble</param>
    /// <param name="minimumLevel">Nivel mínimo de log a capturar</param>
    public HubbleLoggerProvider(Func<IHubbleService> hubbleServiceFactory, LogLevel minimumLevel = LogLevel.Information)
    {
        _hubbleServiceFactory = hubbleServiceFactory;
        _minimumLevel = minimumLevel;
    }

    /// <inheritdoc />
    public ILogger CreateLogger(string categoryName)
    {
        return new HubbleLogger(categoryName, _hubbleServiceFactory, _minimumLevel);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        // No hay recursos que liberar
        GC.SuppressFinalize(this);
    }

    public class HubbleLogger : ILogger
    {
        private readonly string _categoryName;
        private readonly Func<IHubbleService> _hubbleServiceFactory;
        private readonly LogLevel _minimumLevel;

        public HubbleLogger(string categoryName, Func<IHubbleService> hubbleServiceFactory, LogLevel minimumLevel)
        {
            _categoryName = categoryName;
            _hubbleServiceFactory = hubbleServiceFactory;
            _minimumLevel = minimumLevel;
        }

        public IDisposable BeginScope<TState>(TState state)
        {
            return NullScope.Instance;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return logLevel >= _minimumLevel;
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

            // Obtenemos la instancia del servicio cuando se necesita
            var hubbleService = _hubbleServiceFactory();

            // Ejecutar de forma asíncrona pero sin esperar el resultado
            Task.Run(() => hubbleService.LogApplicationLogAsync(_categoryName, logLevel, fullMessage, exception));
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

            // Obtenemos la instancia del servicio cuando se necesita
            var hubbleService = _hubbleServiceFactory();

            // Ejecutar de forma asíncrona pero sin esperar el resultado
            Task.Run(() => hubbleService.LogApplicationLogAsync(_categoryName, logLevel, fullMessage, exception));
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