namespace Gabonet.Hubble.Utilities;

using Gabonet.Hubble.Middleware;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

/// <summary>
/// Helper class to mask sensitive data in JSON strings using Hubble's configuration.
/// This utility allows developers to mask data in logs, database entries, or any other place
/// where sensitive information needs to be protected.
/// </summary>
public static class HubbleMaskingHelper
{
    private static HubbleOptions? _options;

    /// <summary>
    /// Initializes the masking helper with Hubble options.
    /// This is called automatically by Hubble during service registration.
    /// </summary>
    /// <param name="options">Hubble configuration options</param>
    internal static void Initialize(HubbleOptions options)
    {
        _options = options;
    }

    /// <summary>
    /// Serializes an object to JSON and masks sensitive properties according to Hubble's configuration.
    /// Uses the MaskBodyProperties, MaskRequestBodyProperties, and MaskResponseBodyProperties from Security settings.
    /// </summary>
    /// <typeparam name="T">Type of the object to serialize</typeparam>
    /// <param name="obj">Object to serialize and mask</param>
    /// <param name="additionalMaskProperties">Optional additional properties to mask beyond the configured ones</param>
    /// <param name="indent">If true, the output JSON will be indented for readability</param>
    /// <returns>Masked JSON string</returns>
    public static string SerializeMasked<T>(T obj, List<string>? additionalMaskProperties = null, bool indent = false)
    {
        if (obj == null)
        {
            return "null";
        }

        if (_options == null)
        {
            // If Hubble is not initialized, serialize without masking but log a warning
            return System.Text.Json.JsonSerializer.Serialize(obj, new JsonSerializerOptions
            {
                WriteIndented = indent
            });
        }

        try
        {
            // Combine all mask properties from configuration
            var maskProperties = _options.Security.MaskBodyProperties
                .Union(_options.Security.MaskRequestBodyProperties ?? new List<string>())
                .Union(_options.Security.MaskResponseBodyProperties ?? new List<string>())
                .ToList();

            // Add any additional mask properties provided by the caller
            if (additionalMaskProperties != null && additionalMaskProperties.Any())
            {
                maskProperties = maskProperties.Union(additionalMaskProperties).ToList();
            }

            // Serialize the object to JSON
            var json = System.Text.Json.JsonSerializer.Serialize(obj, new JsonSerializerOptions
            {
                WriteIndented = false
            });

            // Apply masking
            var maskedJson = MaskJsonBody(json, maskProperties);

            // Optionally re-format with indentation
            if (indent)
            {
                var tempObj = JsonConvert.DeserializeObject(maskedJson);
                return JsonConvert.SerializeObject(tempObj, Formatting.Indented);
            }

            return maskedJson;
        }
        catch (Exception ex)
        {
            return $"[Serialization Error: {ex.Message}]";
        }
    }

    /// <summary>
    /// Masks sensitive properties in an already serialized JSON string according to Hubble's configuration.
    /// </summary>
    /// <param name="jsonString">JSON string to mask</param>
    /// <param name="additionalMaskProperties">Optional additional properties to mask beyond the configured ones</param>
    /// <returns>Masked JSON string</returns>
    public static string MaskJson(string jsonString, List<string>? additionalMaskProperties = null)
    {
        if (string.IsNullOrEmpty(jsonString))
        {
            return jsonString;
        }

        if (_options == null)
        {
            // If Hubble is not initialized, return original JSON
            return jsonString;
        }

        try
        {
            // Combine all mask properties from configuration
            var maskProperties = _options.Security.MaskBodyProperties
                .Union(_options.Security.MaskRequestBodyProperties ?? new List<string>())
                .Union(_options.Security.MaskResponseBodyProperties ?? new List<string>())
                .ToList();

            // Add any additional mask properties provided by the caller
            if (additionalMaskProperties != null && additionalMaskProperties.Any())
            {
                maskProperties = maskProperties.Union(additionalMaskProperties).ToList();
            }

            return MaskJsonBody(jsonString, maskProperties);
        }
        catch (Exception ex)
        {
            return $"[Masking Error: {ex.Message}]";
        }
    }

    /// <summary>
    /// Masks sensitive properties in a JSON body string (same logic as HubbleMiddleware).
    /// </summary>
    private static string MaskJsonBody(string jsonBody, List<string> maskProperties)
    {
        if (string.IsNullOrEmpty(jsonBody))
        {
            return jsonBody;
        }

        try
        {
            var jsonObject = JsonConvert.DeserializeObject(jsonBody);
            if (jsonObject == null)
            {
                return jsonBody;
            }

            MaskJsonObject(jsonObject, maskProperties);
            return JsonConvert.SerializeObject(jsonObject);
        }
        catch
        {
            // If not valid JSON, return unchanged
            return jsonBody;
        }
    }

    /// <summary>
    /// Recursively masks properties in a JSON object (same logic as HubbleMiddleware).
    /// </summary>
    private static void MaskJsonObject(object obj, List<string> maskProperties)
    {
        if (obj is JObject jObject)
        {
            foreach (var property in jObject.Properties().ToList())
            {
                // Check if property should be masked (case-insensitive)
                if (maskProperties.Contains(property.Name, StringComparer.OrdinalIgnoreCase))
                {
                    property.Value = "*****";
                }
                else
                {
                    MaskJsonObject(property.Value, maskProperties);
                }
            }
        }
        else if (obj is JArray jArray)
        {
            foreach (var item in jArray)
            {
                MaskJsonObject(item, maskProperties);
            }
        }
    }
}

