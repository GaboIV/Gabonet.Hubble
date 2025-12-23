using Gabonet.Hubble.Extensions;
using Gabonet.Hubble.Utilities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;

namespace Gabonet.Hubble.Examples;

/// <summary>
/// Complete example showing how to use HubbleMaskingHelper to protect sensitive data in logs
/// </summary>
public static class MaskingUsageExample
{
    // Example DTO with sensitive data
    public class PlayerDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Country { get; set; } = string.Empty;
        public DateTime BirthDate { get; set; }
        public string Password { get; set; } = string.Empty;  // Sensitive
        public string Token { get; set; } = string.Empty;     // Sensitive
        public int Ranking { get; set; }
        public bool IsActive { get; set; }
    }

    // Example service with logging
    public class PlayerService
    {
        private readonly ILogger<PlayerService> _logger;
        private readonly List<PlayerDto> _repository = new();

        public PlayerService(ILogger<PlayerService> logger)
        {
            _logger = logger;
        }

        public async Task<PlayerDto> CreatePlayerAsync(PlayerDto playerDto)
        {
            playerDto.Id = Guid.NewGuid();

            // ❌ WRONG: Without masking - sensitive data exposed in logs
            // _logger.LogInformation("Creating player: {Player}", JsonSerializer.Serialize(playerDto));
            // Output: Creating player: {"Id":"...","Name":"John","Password":"Secret123","Token":"abc-xyz-123",...}

            // ✅ CORRECT: With masking - sensitive data protected
            _logger.LogInformation("Creating player: {Player}",
                HubbleMaskingHelper.SerializeMasked(playerDto));
            // Output: Creating player: {"Id":"...","Name":"John","Password":"*****","Token":"*****",...}

            // Simulate async operation
            await Task.Delay(10);
            _repository.Add(playerDto);

            _logger.LogInformation("Player created successfully: {Player}",
                HubbleMaskingHelper.SerializeMasked(playerDto));

            return playerDto;
        }

        public async Task<PlayerDto?> UpdatePlayerAsync(Guid id, PlayerDto playerDto)
        {
            // With indented JSON for better readability in debug logs
            _logger.LogDebug("Updating player {Id}. Data:\n{Player}",
                id,
                HubbleMaskingHelper.SerializeMasked(playerDto, indent: true));

            var existing = _repository.Find(p => p.Id == id);
            if (existing == null)
            {
                _logger.LogWarning("Player {Id} not found for update", id);
                return null;
            }

            // Update logic here
            await Task.Delay(10);

            _logger.LogInformation("Player {Id} updated: {Player}",
                id,
                HubbleMaskingHelper.SerializeMasked(existing));

            return existing;
        }

        public void ProcessJsonString(string playerJson)
        {
            // Example with already serialized JSON
            var maskedJson = HubbleMaskingHelper.MaskJson(playerJson);
            _logger.LogInformation("Processing player JSON: {Json}", maskedJson);
        }

        public void LogWithAdditionalMasking(PlayerDto player)
        {
            // Mask additional fields beyond the configured ones
            _logger.LogInformation("Player with extra masking: {Player}",
                HubbleMaskingHelper.SerializeMasked(player, new List<string> { "country", "birthDate" }));
            // This will mask password, token (from config) AND country, birthDate (additional)
        }
    }

    // Example controller using the service
    [ApiController]
    [Route("api/[controller]")]
    public class PlayerController : ControllerBase
    {
        private readonly ILogger<PlayerController> _logger;
        private readonly PlayerService _playerService;

        public PlayerController(ILogger<PlayerController> logger, PlayerService playerService)
        {
            _logger = logger;
            _playerService = playerService;
        }

        [HttpPost]
        public async Task<IActionResult> CreatePlayer([FromBody] PlayerDto playerDto)
        {
            try
            {
                // Log incoming request with masked data
                _logger.LogInformation("Received create player request: {Player}",
                    HubbleMaskingHelper.SerializeMasked(playerDto));

                var createdPlayer = await _playerService.CreatePlayerAsync(playerDto);

                // Note: HTTP responses are automatically masked by Hubble middleware
                // but if you need to log the response separately, you can also mask it
                _logger.LogInformation("Returning player: {Player}",
                    HubbleMaskingHelper.SerializeMasked(createdPlayer));

                return Ok(createdPlayer);
            }
            catch (Exception ex)
            {
                // Even in error scenarios, avoid logging sensitive data
                _logger.LogError(ex, "Error creating player. Input: {Player}",
                    HubbleMaskingHelper.SerializeMasked(playerDto));
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdatePlayer(Guid id, [FromBody] PlayerDto playerDto)
        {
            _logger.LogInformation("Update request for player {Id}: {Player}",
                id,
                HubbleMaskingHelper.SerializeMasked(playerDto));

            var updatedPlayer = await _playerService.UpdatePlayerAsync(id, playerDto);

            if (updatedPlayer == null)
            {
                return NotFound();
            }

            return Ok(updatedPlayer);
        }
    }

    // Example Program.cs configuration
    public static class ProgramExample
    {
        public static void ConfigureHubbleWithMasking()
        {
            var builder = WebApplication.CreateBuilder();

            // Configure Hubble with masking properties
            builder.Services.AddHubble(options =>
            {
                options.ConnectionString = "mongodb://localhost:27017";
                options.DatabaseName = "HubbleDB";
                options.ServiceName = "PlayerService";

                // Configure which properties should be masked
                options.Security.MaskBodyProperties = new List<string>
                {
                    "password",    // Will mask any field named "password" (case-insensitive)
                    "token",       // Will mask any field named "token"
                    "creditCard",  // Will mask credit card numbers
                    "cvv",         // Will mask CVV codes
                    "pin"          // Will mask PIN numbers
                };

                // Additional properties to mask ONLY in requests
                options.Security.MaskRequestBodyProperties = new List<string>
                {
                    "secretKey"
                };

                // Additional properties to mask ONLY in responses
                options.Security.MaskResponseBodyProperties = new List<string>
                {
                    "internalId",
                    "secretData"
                };

                // Mask sensitive headers
                options.Security.MaskHeaders = new List<string>
                {
                    "Authorization",
                    "X-Api-Key",
                    "Cookie"
                };
            });

            // Add logging with Hubble
            builder.Logging.AddHubbleLogging(LogLevel.Information);

            var app = builder.Build();

            // Use Hubble middleware
            app.UseHubble();

            app.Run();

            // After this configuration:
            // - HTTP requests/responses are automatically masked by Hubble middleware
            // - HubbleMaskingHelper is initialized and ready to use
            // - Any logs using HubbleMaskingHelper will use these masking properties
        }
    }

    // Example showing different masking scenarios
    public static class AdvancedMaskingExamples
    {
        public class NestedDataExample
        {
            public string Name { get; set; } = string.Empty;
            public AddressDto Address { get; set; } = new();
            public List<PaymentMethodDto> PaymentMethods { get; set; } = new();
        }

        public class AddressDto
        {
            public string Street { get; set; } = string.Empty;
            public string City { get; set; } = string.Empty;
            public string Country { get; set; } = string.Empty;
        }

        public class PaymentMethodDto
        {
            public string Type { get; set; } = string.Empty;
            public string CreditCard { get; set; } = string.Empty;  // Sensitive
            public string CVV { get; set; } = string.Empty;         // Sensitive
        }

        public static void DemonstrateNestedMasking(ILogger logger)
        {
            var data = new NestedDataExample
            {
                Name = "John Doe",
                Address = new AddressDto
                {
                    Street = "123 Main St",
                    City = "New York",
                    Country = "USA"
                },
                PaymentMethods = new List<PaymentMethodDto>
                {
                    new PaymentMethodDto
                    {
                        Type = "Visa",
                        CreditCard = "4111111111111111",
                        CVV = "123"
                    },
                    new PaymentMethodDto
                    {
                        Type = "MasterCard",
                        CreditCard = "5500000000000004",
                        CVV = "456"
                    }
                }
            };

            // HubbleMaskingHelper recursively masks sensitive data in nested objects and arrays
            logger.LogInformation("User payment data: {Data}",
                HubbleMaskingHelper.SerializeMasked(data));

            // Output will have all "creditCard" and "cvv" fields masked as "*****"
            // even though they're nested in objects within an array
        }
    }
}

