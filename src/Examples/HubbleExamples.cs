namespace Gabonet.Hubble.Examples;

using Gabonet.Hubble.Extensions;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Generic;

/// <summary>
/// Ejemplos de uso de la configuración de Hubble
/// </summary>
public static class HubbleExamples
{
    /// <summary>
    /// Ejemplo de cómo configurar rutas a ignorar
    /// </summary>
    public static void ConfigureIgnoredPaths()
    {
        var services = new ServiceCollection();

        // Ejemplo 1: Configuración con rutas a ignorar
        services.AddHubble(options =>
        {
            options.ConnectionString = "mongodb://localhost:27017";
            options.DatabaseName = "hubble";
            options.ServiceName = "MiServicio";

            // Configurar rutas a ignorar (endpoints de health, métricas, etc.)
            options.IgnorePaths = new List<string>
            {
                "/health",
                "/metrics",
                "/test"
            };
        });

        // Ejemplo 2: Agregando rutas a ignorar
        services.AddHubble(options =>
        {
            options.ConnectionString = "mongodb://localhost:27017";
            options.DatabaseName = "hubble";
            
            // Inicializar y agregar rutas
            options.IgnorePaths = new List<string>();
            options.IgnorePaths.Add("/api/status");
            options.IgnorePaths.Add("/swagger");
            options.IgnorePaths.Add("/favicon.ico");
        });
    }
} 