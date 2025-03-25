namespace Gabonet.Hubble.Logging;

using Gabonet.Hubble.Interfaces;
using Microsoft.Extensions.Logging;
using System;
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

    private class HubbleLogger : ILogger
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

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
                return;

            string message = formatter(state, exception);
            
            // Obtenemos la instancia del servicio cuando se necesita
            var hubbleService = _hubbleServiceFactory();
            
            // Ejecutar de forma asíncrona pero sin esperar el resultado
            Task.Run(() => hubbleService.LogApplicationLogAsync(_categoryName, logLevel, message, exception));
        }

        private class NullScope : IDisposable
        {
            public static NullScope Instance { get; } = new NullScope();

            private NullScope() { }

            public void Dispose() { }
        }
    }
} 