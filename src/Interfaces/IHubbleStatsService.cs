namespace Gabonet.Hubble.Interfaces;

using Gabonet.Hubble.Models;
using System;
using System.Threading.Tasks;

/// <summary>
/// Interfaz para el servicio de estadísticas de Hubble
/// </summary>
public interface IHubbleStatsService
{
    /// <summary>
    /// Obtiene las estadísticas actuales del sistema Hubble
    /// </summary>
    /// <returns>Estadísticas actualizadas</returns>
    Task<HubbleStatistics> GetStatisticsAsync();

    /// <summary>
    /// Actualiza las estadísticas después de una operación de limpieza (prune)
    /// </summary>
    /// <param name="pruneDate">Fecha de la limpieza</param>
    /// <param name="logsDeleted">Cantidad de logs eliminados</param>
    /// <returns>Estadísticas actualizadas</returns>
    Task<HubbleStatistics> UpdatePruneStatisticsAsync(DateTime pruneDate, long logsDeleted);

    /// <summary>
    /// Obtiene la configuración actual del sistema
    /// </summary>
    /// <returns>Configuración del sistema</returns>
    Task<HubbleSystemConfiguration> GetSystemConfigurationAsync();

    /// <summary>
    /// Guarda o actualiza la configuración del sistema
    /// </summary>
    /// <param name="config">Configuración a guardar</param>
    /// <returns>Configuración guardada</returns>
    Task<HubbleSystemConfiguration> SaveSystemConfigurationAsync(HubbleSystemConfiguration config);

    /// <summary>
    /// Recalcula todas las estadísticas (cuenta total de logs, errores, etc.)
    /// </summary>
    /// <returns>Estadísticas actualizadas</returns>
    Task<HubbleStatistics> RecalculateStatisticsAsync();
} 