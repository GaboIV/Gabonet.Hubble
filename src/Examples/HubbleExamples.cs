namespace Gabonet.Hubble.Examples;

using Gabonet.Hubble.Extensions;
using Gabonet.Hubble.Utilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Threading.Tasks;
using System;

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

    /// <summary>
    /// Example of how to use HubbleMaskingHelper to mask sensitive data in logs
    /// </summary>
    public static class MaskingExamples
    {
        public class UserDto
        {
            public Guid Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public string Email { get; set; } = string.Empty;
            public string Password { get; set; } = string.Empty;  // Sensitive
            public string Token { get; set; } = string.Empty;     // Sensitive
            public string CreditCard { get; set; } = string.Empty; // Sensitive
        }

        public class UserService
        {
            private readonly ILogger<UserService> _logger;

            public UserService(ILogger<UserService> logger)
            {
                _logger = logger;
            }

            public async Task<UserDto> CreateUserAsync(UserDto userDto)
            {
                // Example 1: Basic usage - logs user data with sensitive fields masked
                _logger.LogInformation("Creating user: {User}",
                    HubbleMaskingHelper.SerializeMasked(userDto));

                // Simulate database operation
                await Task.Delay(100);

                // Example 2: With additional properties to mask (beyond configured ones)
                _logger.LogInformation("User created with details: {User}",
                    HubbleMaskingHelper.SerializeMasked(userDto, new List<string> { "email" }));

                // Example 3: With indented JSON for better readability in logs
                _logger.LogDebug("User object created:\n{User}",
                    HubbleMaskingHelper.SerializeMasked(userDto, indent: true));

                return userDto;
            }

            public void ProcessExistingJson(string userJson)
            {
                // Example 4: Mask an already serialized JSON string
                var maskedJson = HubbleMaskingHelper.MaskJson(userJson);
                _logger.LogInformation("Processing user: {Json}", maskedJson);

                // Example 5: Mask JSON with additional properties
                var extraMaskedJson = HubbleMaskingHelper.MaskJson(
                    userJson,
                    new List<string> { "ssn", "phoneNumber" }
                );
                _logger.LogInformation("User with extra masking: {Json}", extraMaskedJson);
            }

            public void LogDatabaseEntity(object entity)
            {
                // Example 6: Works with any object type (entities, DTOs, etc.)
                _logger.LogInformation("Entity state: {Entity}",
                    HubbleMaskingHelper.SerializeMasked(entity));
            }
        }

        /// <summary>
        /// Example showing how masking respects Hubble's configuration
        /// </summary>
        public static void DemonstrateConfigurationIntegration()
        {
            var services = new ServiceCollection();

            // Configure Hubble with specific masking properties
            services.AddHubble(options =>
            {
                options.ConnectionString = "mongodb://localhost:27017";
                options.DatabaseName = "hubble";

                // Configure masking properties
                options.Security.MaskBodyProperties = new List<string>
                {
                    "password",
                    "token",
                    "creditCard",
                    "cvv"
                };

                // Request-specific masking
                options.Security.MaskRequestBodyProperties = new List<string>
                {
                    "pin",
                    "secretKey"
                };

                // Response-specific masking
                options.Security.MaskResponseBodyProperties = new List<string>
                {
                    "internalId",
                    "secretData"
                };
            });

            // After AddHubble is called, HubbleMaskingHelper is automatically initialized
            // and will use these masking properties

            var serviceProvider = services.BuildServiceProvider();
            var logger = serviceProvider.GetRequiredService<ILogger<UserService>>();
            var userService = new UserService(logger);

            // Now when using the masking helper, it will automatically mask
            // all properties defined in the Hubble configuration
            var user = new UserDto
            {
                Id = Guid.NewGuid(),
                Name = "John Doe",
                Email = "john@example.com",
                Password = "SecretPass123",  // Will be masked as "*****"
                Token = "abc123xyz",         // Will be masked as "*****"
                CreditCard = "4111111111111111" // Will be masked as "*****"
            };

            // This will log the user with password, token, and creditCard masked
            logger.LogInformation("User data: {User}",
                HubbleMaskingHelper.SerializeMasked(user));
        }
    }
}