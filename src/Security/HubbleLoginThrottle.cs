namespace Gabonet.Hubble.Security;

using System;
using System.Collections.Concurrent;

/// <summary>
/// Limita los intentos fallidos de autenticación por IP para mitigar ataques de fuerza bruta.
/// </summary>
public sealed class HubbleLoginThrottle
{
    private const int MaxTrackedClients = 10_000;

    private readonly int _maxFailures;
    private readonly TimeSpan _window;
    private readonly TimeSpan _lockout;
    private readonly ConcurrentDictionary<string, Entry> _entries = new();

    /// <summary>
    /// Constructor del limitador de intentos.
    /// </summary>
    /// <param name="maxFailures">Intentos fallidos permitidos dentro de la ventana</param>
    /// <param name="window">Ventana de tiempo en la que se cuentan los fallos</param>
    /// <param name="lockout">Tiempo de bloqueo al superar el límite</param>
    public HubbleLoginThrottle(int maxFailures = 5, TimeSpan? window = null, TimeSpan? lockout = null)
    {
        _maxFailures = maxFailures;
        _window = window ?? TimeSpan.FromMinutes(15);
        _lockout = lockout ?? TimeSpan.FromMinutes(15);
    }

    /// <summary>
    /// Indica si el cliente está bloqueado temporalmente.
    /// </summary>
    public bool IsLockedOut(string clientKey)
    {
        return _entries.TryGetValue(clientKey, out var entry) &&
               entry.LockedUntil.HasValue &&
               entry.LockedUntil.Value > DateTime.UtcNow;
    }

    /// <summary>
    /// Registra un intento fallido.
    /// </summary>
    public void RegisterFailure(string clientKey)
    {
        var now = DateTime.UtcNow;

        if (_entries.Count >= MaxTrackedClients)
        {
            PurgeExpired(now);
        }

        _entries.AddOrUpdate(
            clientKey,
            _ => new Entry(1, now, null),
            (_, entry) =>
            {
                // Reiniciar el contador si la ventana o el bloqueo anterior ya expiraron
                if (now - entry.WindowStart > _window || (entry.LockedUntil.HasValue && entry.LockedUntil.Value <= now))
                {
                    return new Entry(1, now, null);
                }

                var failures = entry.Failures + 1;
                return new Entry(failures, entry.WindowStart, failures >= _maxFailures ? now.Add(_lockout) : null);
            });
    }

    /// <summary>
    /// Limpia el historial del cliente tras una autenticación correcta.
    /// </summary>
    public void RegisterSuccess(string clientKey)
    {
        _entries.TryRemove(clientKey, out _);
    }

    private void PurgeExpired(DateTime now)
    {
        foreach (var pair in _entries)
        {
            var entry = pair.Value;
            var lockExpired = !entry.LockedUntil.HasValue || entry.LockedUntil.Value <= now;
            if (lockExpired && now - entry.WindowStart > _window)
            {
                _entries.TryRemove(pair.Key, out _);
            }
        }
    }

    private sealed record Entry(int Failures, DateTime WindowStart, DateTime? LockedUntil);
}
