namespace Gabonet.Hubble.Middleware;

using Gabonet.Hubble.Interfaces;
using System;
using System.Threading.Tasks;

/// <summary>
/// Administrador de limpieza de datos históricos para Hubble.
/// </summary>
public class HubbleDataPruneManager
{
    private readonly HubbleOptions _options;
    private DateTime? _lastPruneAttempt;
    private static readonly object _lockObject = new object();
    private static bool _isPruneRunning = false;
    private readonly TimeZoneInfo _timeZone;

    /// <summary>
    /// Constructor del administrador de limpieza de datos.
    /// </summary>
    /// <param name="options">Opciones de configuración de Hubble</param>
    public HubbleDataPruneManager(HubbleOptions options)
    {
        _options = options;
        _lastPruneAttempt = null;
        
        // Determinar la zona horaria a utilizar
        try
        {
            if (string.IsNullOrEmpty(options.TimeZoneId))
            {
                _timeZone = TimeZoneInfo.Utc;
            }
            else
            {
                _timeZone = TimeZoneInfo.FindSystemTimeZoneById(options.TimeZoneId);
            }
        }
        catch (Exception)
        {
            _timeZone = TimeZoneInfo.Utc;
        }
        
        if (options.EnableDataPrune)
        {
            Console.WriteLine($"[Hubble] Administrador de limpieza de datos inicializado");
        }
    }

    /// <summary>
    /// Ejecuta el proceso de limpieza de datos si es necesario.
    /// </summary>
    /// <param name="hubbleService">Servicio de Hubble para acceder a los logs</param>
    /// <returns>Tarea asíncrona</returns>
    public async Task TryPruneDataAsync(IHubbleService hubbleService)
    {
        // Si la limpieza no está habilitada, salir
        if (!_options.EnableDataPrune)
        {
            return;
        }

        // Verificar si ya ha pasado el intervalo de limpieza desde el último intento
        var now = DateTime.UtcNow;
        var shouldPrune = false;

        lock (_lockObject)
        {
            if (_isPruneRunning)
            {
                return;
            }

            if (_lastPruneAttempt == null)
            {
                shouldPrune = true;
                _isPruneRunning = true;
                _lastPruneAttempt = now;
                
                // Mostrar la fecha en la zona horaria configurada
                var localTime = TimeZoneInfo.ConvertTimeFromUtc(now, _timeZone);
            }
            else if ((now - _lastPruneAttempt.Value).TotalHours >= _options.DataPruneIntervalHours)
            {
                shouldPrune = true;
                _isPruneRunning = true;
                _lastPruneAttempt = now;
            }
            else
            {
                // Calcular cuánto tiempo falta para la próxima limpieza
                var hoursToNextPrune = _options.DataPruneIntervalHours - (now - _lastPruneAttempt.Value).TotalHours;
            }
        }

        if (shouldPrune)
        {
            try
            {
                // Convertir la fecha a la zona horaria configurada para mostrarla
                var localTime = TimeZoneInfo.ConvertTimeFromUtc(now, _timeZone);
                Console.WriteLine($"[Hubble] Iniciando proceso de limpieza de datos históricos ({localTime})");
                
                // Calcular la fecha límite para eliminar logs
                var cutoffDate = now.AddHours(-_options.MaxLogAgeHours);
                
                // Convertir la fecha límite a la zona horaria configurada para mostrarla
                var localCutoffDate = TimeZoneInfo.ConvertTimeFromUtc(cutoffDate, _timeZone);
                Console.WriteLine($"[Hubble] Eliminando logs anteriores a {localCutoffDate}");
                
                // Ejecutar la limpieza (usar UTC para la operación real)
                var deletedCount = await hubbleService.DeleteLogsOlderThanAsync(cutoffDate);
                
                Console.WriteLine($"[Hubble] Proceso de limpieza completado. {deletedCount} logs eliminados.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Hubble] Error durante el proceso de limpieza: {ex.Message}");
                Console.WriteLine($"[Hubble] Detalles: {ex.StackTrace}");
            }
            finally
            {
                lock (_lockObject)
                {
                    _isPruneRunning = false;
                }
            }
        }
    }
} 