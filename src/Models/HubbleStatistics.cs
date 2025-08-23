using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;
using System.Collections.Generic;

namespace Gabonet.Hubble.Models
{
    /// <summary>
    /// Modelo para almacenar estadísticas del sistema Hubble
    /// </summary>
    public class HubbleStatistics
    {
        /// <summary>
        /// ID único de las estadísticas
        /// </summary>
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = ObjectId.GenerateNewId().ToString();

        /// <summary>
        /// Fecha de creación del registro
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Cantidad total de logs
        /// </summary>
        public long TotalLogs { get; set; }

        /// <summary>
        /// Cantidad de logs exitosos (código 200-299)
        /// </summary>
        public long SuccessfulLogs { get; set; }

        /// <summary>
        /// Cantidad de logs fallidos (código 400+)
        /// </summary>
        public long FailedLogs { get; set; }

        /// <summary>
        /// Cantidad de logs de logger
        /// </summary>
        public long LoggerLogs { get; set; }

        /// <summary>
        /// Estadísticas de prune de datos
        /// </summary>
        public PruneStatistics LastPrune { get; set; } = new PruneStatistics();
    }

    /// <summary>
    /// Estadísticas de la última operación de limpieza de logs (prune)
    /// </summary>
    public class PruneStatistics
    {
        /// <summary>
        /// Fecha de la última limpieza
        /// </summary>
        public DateTime? LastPruneDate { get; set; }

        /// <summary>
        /// Cantidad de logs eliminados en la última limpieza
        /// </summary>
        public long LogsDeleted { get; set; }
    }

    /// <summary>
    /// Modelo para la configuración del sistema Hubble
    /// </summary>
    public class HubbleSystemConfiguration
    {
        /// <summary>
        /// ID único de la configuración
        /// </summary>
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string Id { get; set; } = ObjectId.GenerateNewId().ToString();

        /// <summary>
        /// Fecha de creación/actualización de la configuración
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Nombre del servicio
        /// </summary>
        public string ServiceName { get; set; } = string.Empty;

        /// <summary>
        /// ID de zona horaria
        /// </summary>
        public string TimeZoneId { get; set; } = string.Empty;

        /// <summary>
        /// Estado de la limpieza automática de datos
        /// </summary>
        public bool EnableDataPrune { get; set; }

        /// <summary>
        /// Intervalo en horas para la limpieza de datos
        /// </summary>
        public int DataPruneIntervalHours { get; set; }

        /// <summary>
        /// Edad máxima de logs en horas antes de ser eliminados
        /// </summary>
        public int MaxLogAgeHours { get; set; }

        /// <summary>
        /// Indica si se capturan mensajes del logger
        /// </summary>
        public bool CaptureLoggerMessages { get; set; }

        /// <summary>
        /// Indica si se capturan solicitudes HTTP
        /// </summary>
        public bool CaptureHttpRequests { get; set; }

        /// <summary>
        /// Rutas ignoradas por el middleware
        /// </summary>
        public List<string> IgnorePaths { get; set; } = new List<string>();

        /// <summary>
        /// Nivel mínimo de log a capturar
        /// </summary>
        public string MinimumLogLevel { get; set; } = string.Empty;

        /// <summary>
        /// Información del sistema
        /// </summary>
        public SystemInfo SystemInfo { get; set; } = new SystemInfo();
    }

    /// <summary>
    /// Información del sistema
    /// </summary>
    public class SystemInfo
    {
        /// <summary>
        /// Versión de Hubble
        /// </summary>
        public string Version { get; set; } = "0.2.9.0";

        /// <summary>
        /// Versión de MongoDB
        /// </summary>
        public string MongoDBVersion { get; set; } = string.Empty;

        /// <summary>
        /// Nombre de la base de datos
        /// </summary>
        public string DatabaseName { get; set; } = string.Empty;

        /// <summary>
        /// Cadena de conexión (ofuscada por seguridad)
        /// </summary>
        public string ConnectionString { get; set; } = string.Empty;

        /// <summary>
        /// Fecha de inicio del servicio
        /// </summary>
        public DateTime StartTime { get; set; } = DateTime.UtcNow;
    }
} 