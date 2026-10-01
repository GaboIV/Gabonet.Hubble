namespace Gabonet.Hubble.Services;

using System;

/// <summary>
/// Estado de la preparación del almacenamiento de Hubble en MongoDB (índices y retención).
/// Se muestra en la página de configuración del dashboard.
/// </summary>
public sealed class HubbleStorageStatus
{
    private readonly object _lock = new();
    private bool _indexesReady;
    private string? _lastError;
    private DateTime? _lastAttemptUtc;

    /// <summary>
    /// Indica si los índices (incluido el TTL de retención) están creados y al día.
    /// </summary>
    public bool IndexesReady
    {
        get { lock (_lock) { return _indexesReady; } }
    }

    /// <summary>
    /// Último error al preparar los índices, si lo hubo.
    /// </summary>
    public string? LastError
    {
        get { lock (_lock) { return _lastError; } }
    }

    /// <summary>
    /// Fecha (UTC) del último intento de preparar los índices.
    /// </summary>
    public DateTime? LastAttemptUtc
    {
        get { lock (_lock) { return _lastAttemptUtc; } }
    }

    internal void MarkReady()
    {
        lock (_lock)
        {
            _indexesReady = true;
            _lastError = null;
            _lastAttemptUtc = DateTime.UtcNow;
        }
    }

    internal void MarkFailed(string error)
    {
        lock (_lock)
        {
            _indexesReady = false;
            _lastError = error;
            _lastAttemptUtc = DateTime.UtcNow;
        }
    }
}
