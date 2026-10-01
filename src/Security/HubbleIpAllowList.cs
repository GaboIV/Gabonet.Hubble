namespace Gabonet.Hubble.Security;

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;

/// <summary>
/// Evalúa si una dirección IP está permitida según una lista de IPs individuales y rangos CIDR (IPv4 e IPv6).
/// </summary>
public static class HubbleIpAllowList
{
    /// <summary>
    /// Indica si la IP remota está permitida.
    /// Una lista vacía o que contenga "*" permite cualquier IP.
    /// </summary>
    /// <param name="remoteIp">IP del cliente (normalmente HttpContext.Connection.RemoteIpAddress)</param>
    /// <param name="rules">IPs individuales o rangos CIDR (por ejemplo "10.0.0.0/8" o "fd00::/8")</param>
    /// <returns>true si la IP está permitida</returns>
    public static bool IsAllowed(IPAddress? remoteIp, IEnumerable<string>? rules)
    {
        var hasRules = false;

        if (rules != null)
        {
            foreach (var rawRule in rules)
            {
                var rule = rawRule?.Trim();
                if (string.IsNullOrEmpty(rule))
                {
                    continue;
                }

                hasRules = true;

                if (rule == "*")
                {
                    return true;
                }

                if (remoteIp != null && Matches(Normalize(remoteIp), rule))
                {
                    return true;
                }
            }
        }

        // Sin reglas configuradas se permite el acceso a cualquier IP
        return !hasRules;
    }

    private static bool Matches(IPAddress ip, string rule)
    {
        var slashIndex = rule.IndexOf('/');
        if (slashIndex < 0)
        {
            return IPAddress.TryParse(rule, out var single) && Normalize(single).Equals(ip);
        }

        if (!IPAddress.TryParse(rule.AsSpan(0, slashIndex), out var network) ||
            !int.TryParse(rule.AsSpan(slashIndex + 1), out var prefixLength))
        {
            return false;
        }

        network = Normalize(network);
        if (network.AddressFamily != ip.AddressFamily)
        {
            return false;
        }

        var networkBytes = network.GetAddressBytes();
        var ipBytes = ip.GetAddressBytes();
        if (prefixLength < 0 || prefixLength > networkBytes.Length * 8)
        {
            return false;
        }

        var fullBytes = prefixLength / 8;
        for (var i = 0; i < fullBytes; i++)
        {
            if (networkBytes[i] != ipBytes[i])
            {
                return false;
            }
        }

        var remainingBits = prefixLength % 8;
        if (remainingBits == 0)
        {
            return true;
        }

        var mask = (byte)(0xFF << (8 - remainingBits));
        return (networkBytes[fullBytes] & mask) == (ipBytes[fullBytes] & mask);
    }

    /// <summary>
    /// Convierte direcciones IPv4 mapeadas en IPv6 (::ffff:a.b.c.d) a IPv4 y "::1" a "127.0.0.1".
    /// </summary>
    private static IPAddress Normalize(IPAddress ip)
    {
        if (ip.IsIPv4MappedToIPv6)
        {
            return ip.MapToIPv4();
        }

        if (ip.AddressFamily == AddressFamily.InterNetworkV6 && IPAddress.IPv6Loopback.Equals(ip))
        {
            return IPAddress.Loopback;
        }

        return ip;
    }
}
