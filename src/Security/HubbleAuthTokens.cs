namespace Gabonet.Hubble.Security;

using Microsoft.AspNetCore.DataProtection;
using System;
using System.Security.Cryptography;
using System.Text;

/// <summary>
/// Utilidades para validar credenciales y emitir/validar la cookie de sesión del dashboard de Hubble.
/// </summary>
public static class HubbleAuthTokens
{
    /// <summary>
    /// Nombre de la cookie de sesión del dashboard.
    /// </summary>
    public const string CookieName = "HubbleAuth";

    /// <summary>
    /// Duración de la sesión del dashboard.
    /// </summary>
    public static readonly TimeSpan SessionLifetime = TimeSpan.FromHours(8);

    private const string ProtectorPurpose = "Gabonet.Hubble.AuthCookie.v1";

    /// <summary>
    /// Compara las credenciales recibidas con las configuradas en tiempo constante.
    /// Si no hay credenciales configuradas, la validación siempre falla.
    /// </summary>
    public static bool CredentialsMatch(string? username, string? password, string expectedUsername, string expectedPassword)
    {
        if (string.IsNullOrEmpty(expectedUsername) || string.IsNullOrEmpty(expectedPassword))
        {
            return false;
        }

        // Evaluar ambas comparaciones siempre para no revelar cuál de los dos campos falló
        var userMatches = FixedTimeEquals(username ?? string.Empty, expectedUsername);
        var passwordMatches = FixedTimeEquals(password ?? string.Empty, expectedPassword);
        return userMatches & passwordMatches;
    }

    /// <summary>
    /// Genera un token de sesión cifrado y firmado con Data Protection, con caducidad incluida.
    /// </summary>
    public static string CreateSessionToken(IDataProtectionProvider dataProtectionProvider, string username, string password)
    {
        var protector = dataProtectionProvider.CreateProtector(ProtectorPurpose).ToTimeLimitedDataProtector();
        return protector.Protect(GetCredentialFingerprint(username, password), SessionLifetime);
    }

    /// <summary>
    /// Valida un token de sesión. Falla si el token fue manipulado, expiró o si las credenciales configuradas cambiaron.
    /// </summary>
    public static bool ValidateSessionToken(IDataProtectionProvider dataProtectionProvider, string? token, string username, string password)
    {
        if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
        {
            return false;
        }

        try
        {
            var protector = dataProtectionProvider.CreateProtector(ProtectorPurpose).ToTimeLimitedDataProtector();
            var payload = protector.Unprotect(token, out _);
            return FixedTimeEquals(payload, GetCredentialFingerprint(username, password));
        }
        catch (CryptographicException)
        {
            return false;
        }
    }

    /// <summary>
    /// Huella de las credenciales configuradas: si se cambia el usuario o la contraseña, las sesiones previas dejan de ser válidas.
    /// </summary>
    private static string GetCredentialFingerprint(string username, string password)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"{username}\n{password}"));
        return Convert.ToBase64String(hash);
    }

    private static bool FixedTimeEquals(string left, string right)
    {
        // Se comparan los hashes para que la longitud de las cadenas no afecte al tiempo de comparación
        var leftHash = SHA256.HashData(Encoding.UTF8.GetBytes(left));
        var rightHash = SHA256.HashData(Encoding.UTF8.GetBytes(right));
        return CryptographicOperations.FixedTimeEquals(leftHash, rightHash);
    }
}
