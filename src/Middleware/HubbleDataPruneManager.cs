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

    /// <summary>
    /// Constructor del administrador de limpieza de datos.
    /// </summary>
    /// <param name="options">Opciones de configuración de Hubble</param>
    public HubbleDataPruneManager(HubbleOptions options)
    {
        _options = options;
        _lastPruneAttempt = null;
        
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
                Console.WriteLine($"[Hubble] Primera ejecución del proceso de limpieza");
            }
            else if ((now - _lastPruneAttempt.Value).TotalHours >= _options.DataPruneIntervalHours)
            {
                shouldPrune = true;
                _isPruneRunning = true;
                Console.WriteLine($"[Hubble] Han pasado {Math.Round((now - _lastPruneAttempt.Value).TotalHours, 2)} horas desde la última limpieza");
                _lastPruneAttempt = now;
            }
            else
            {
                // Calcular cuánto tiempo falta para la próxima limpieza
                var hoursToNextPrune = _options.DataPruneIntervalHours - (now - _lastPruneAttempt.Value).TotalHours;
                Console.WriteLine($"[Hubble] Próxima limpieza en {Math.Round(hoursToNextPrune, 2)} hora(s)");
            }
        }

        if (shouldPrune)
        {
            try
            {
                Console.WriteLine($"[Hubble] Iniciando proceso de limpieza de datos históricos ({now})");
                
                // Calcular la fecha límite para eliminar logs
                var cutoffDate = now.AddHours(-_options.MaxLogAgeHours);
                Console.WriteLine($"[Hubble] Eliminando logs anteriores a {cutoffDate}");
                
                // Ejecutar la limpieza
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