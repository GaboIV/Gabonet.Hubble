namespace Gabonet.Hubble.BackgroundServices;

using Gabonet.Hubble.Interfaces;
using Gabonet.Hubble.Middleware;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Servicio en segundo plano para la limpieza automática de logs antiguos
/// </summary>
public class DataPruneService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly HubbleOptions _options;
    private readonly ILogger<DataPruneService> _logger;
    
    /// <summary>
    /// Constructor del servicio de limpieza de datos
    /// </summary>
    /// <param name="serviceProvider">Proveedor de servicios</param>
    /// <param name="options">Opciones de configuración de Hubble</param>
    /// <param name="logger">Logger</param>
    public DataPruneService(
        IServiceProvider serviceProvider,
        HubbleOptions options,
        ILogger<DataPruneService> logger)
    {
        _serviceProvider = serviceProvider;
        _options = options;
        _logger = logger;
    }
    
    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Servicio de limpieza de datos de Hubble iniciado");
        
        if (!_options.EnableDataPrune)
        {
            _logger.LogInformation("Limpieza automática de datos desactivada en la configuración");
            return;
        }
        
        // Limpiar datos al iniciar el servicio
        await PruneData();
        
        // Configurar el intervalo de limpieza (por defecto, cada hora)
        var interval = TimeSpan.FromHours(_options.DataPruneIntervalHours > 0 ? _options.DataPruneIntervalHours : 1);
        
        _logger.LogInformation($"Configurado para ejecutar limpieza cada {interval.TotalHours} horas");
        
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Esperar hasta el próximo intervalo de limpieza
                await Task.Delay(interval, stoppingToken);
                
                // Ejecutar la limpieza de datos
                if (!stoppingToken.IsCancellationRequested)
                {
                    await PruneData();
                }
            }
            catch (OperationCanceledException)
            {
                // Operación cancelada, salir del bucle
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error durante la limpieza automática de datos");
                
                // Esperar un tiempo antes de reintentar en caso de error
                try
                {
                    await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
        
        _logger.LogInformation("Servicio de limpieza de datos de Hubble detenido");
    }
    
    /// <summary>
    /// Ejecuta la limpieza de datos antiguos
    /// </summary>
    private async Task PruneData()
    {
        _logger.LogInformation("Iniciando limpieza de datos antiguos");
        
        try
        {
            using var scope = _serviceProvider.CreateScope();
            IHubbleService hubbleService = null;
            IHubbleStatsService statsService = null;
            
            try
            {
                hubbleService = scope.ServiceProvider.GetRequiredService<IHubbleService>();
                statsService = scope.ServiceProvider.GetRequiredService<IHubbleStatsService>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener los servicios necesarios para la limpieza de datos");
                return;
            }
            
            // Calcular la fecha de corte según la configuración
            var maxAgeHours = _options.MaxLogAgeHours > 0 ? _options.MaxLogAgeHours : 24;
            var cutoffDate = DateTime.UtcNow.AddHours(-maxAgeHours);
            
            _logger.LogInformation($"Eliminando logs anteriores a {cutoffDate} (antigüedad máxima: {maxAgeHours} horas)");
            
            // Eliminar logs antiguos
            long logsDeleted = 0;
            try
            {
                logsDeleted = await hubbleService.DeleteLogsOlderThanAsync(cutoffDate);
                _logger.LogInformation($"Limpieza completada: {logsDeleted} logs eliminados");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al eliminar logs antiguos");
                return;
            }
            
            // Actualizar estadísticas
            try
            {
                if (statsService != null)
                {
                    await statsService.UpdatePruneStatisticsAsync(DateTime.UtcNow, logsDeleted);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al actualizar las estadísticas de limpieza");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error durante la limpieza de datos antiguos");
        }
    }
} 