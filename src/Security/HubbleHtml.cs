namespace Gabonet.Hubble.Security;

using System.Net;

/// <summary>
/// Codificación de valores para insertarlos de forma segura en el HTML del dashboard.
/// </summary>
internal static class HubbleHtml
{
    /// <summary>
    /// Codifica un valor para texto o atributos HTML (incluye comillas simples y dobles).
    /// </summary>
    public static string Encode(object? value)
    {
        return WebUtility.HtmlEncode(value?.ToString() ?? string.Empty);
    }
}
