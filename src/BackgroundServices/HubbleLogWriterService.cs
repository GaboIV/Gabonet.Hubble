namespace Gabonet.Hubble.BackgroundServices;

using Gabonet.Hubble.Middleware;
using Gabonet.Hubble.Models;
using Gabonet.Hubble.Services;
using Microsoft.Extensions.Hosting;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Servicio en segundo plano que vacía la <see cref="HubbleLogQueue"/> y guarda los logs en MongoDB por lotes.
/// </summary>
internal sealed class HubbleLogWriterService : BackgroundService
{
    private const int MaxBatchSize = 200;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan ShutdownFlushTimeout = TimeSpan.FromSeconds(5);

    private readonly HubbleLogQueue _queue;
    private readonly IMongoCollection<GeneralLog> _logsCollection;
    private readonly HubbleOptions _options;
    private bool _storageUnavailable;

    public HubbleLogWriterService(HubbleLogQueue queue, IMongoCollection<GeneralLog> logsCollection, HubbleOptions options)
    {
        _queue = queue;
        _logsCollection = logsCollection;
        _options = options;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Ceder el control de inmediato para no retrasar el arranque de la aplicación
        await Task.Yield();

        var batch = new List<GeneralLog>(MaxBatchSize);

        try
        {
            while (await _queue.Reader.WaitToReadAsync(stoppingToken))
            {
                FillBatch(batch);

                if (!await TryWriteBatchAsync(batch, stoppingToken))
                {
                    // MongoDB no está disponible: esperar antes de volver a intentarlo. Mientras tanto la cola
                    // acotada descarta los logs nuevos en lugar de consumir memoria sin límite.
                    await Task.Delay(RetryDelay, stoppingToken);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Apagado de la aplicación
        }

        await FlushRemainingAsync(batch);
    }

    private void FillBatch(List<GeneralLog> batch)
    {
        while (batch.Count < MaxBatchSize && _queue.Reader.TryRead(out var log))
        {
            batch.Add(log);
        }
    }

    /// <summary>
    /// Guarda el lote. Devuelve false si MongoDB no está disponible (el lote se descarta).
    /// Si se cancela, el lote se conserva para el vaciado final.
    /// </summary>
    private async Task<bool> TryWriteBatchAsync(List<GeneralLog> batch, CancellationToken cancellationToken)
    {
        if (batch.Count == 0)
        {
            return true;
        }

        try
        {
            await _logsCollection.InsertManyAsync(batch, new InsertManyOptions { IsOrdered = false }, cancellationToken);
            OnWriteSucceeded();
            batch.Clear();
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (MongoBulkWriteException)
        {
            // Escritura parcial (por ejemplo, documentos duplicados): el resto del lote ya se guardó
            OnWriteSucceeded();
            batch.Clear();
            return true;
        }
        catch (Exception ex)
        {
            OnWriteFailed(batch.Count, ex);
            batch.Clear();
            return false;
        }
    }

    private async Task FlushRemainingAsync(List<GeneralLog> batch)
    {
        // Si MongoDB ya estaba caído, no retrasar el apagado intentando guardar lo pendiente
        if (_storageUnavailable)
        {
            return;
        }

        // Intentar guardar lo pendiente durante un tiempo limitado para no retrasar el apagado
        using var timeout = new CancellationTokenSource(ShutdownFlushTimeout);

        try
        {
            do
            {
                FillBatch(batch);
                if (batch.Count == 0 || !await TryWriteBatchAsync(batch, timeout.Token))
                {
                    return;
                }
            }
            while (true);
        }
        catch (OperationCanceledException)
        {
            // Se agotó el tiempo de vaciado
        }
    }

    private void OnWriteSucceeded()
    {
        if (_storageUnavailable)
        {
            _storageUnavailable = false;
            Console.WriteLine($"[Hubble] Conexión con MongoDB recuperada. Logs descartados mientras no estuvo disponible: {_queue.DroppedCount}");
        }
    }

    private void OnWriteFailed(int batchSize, Exception ex)
    {
        // Avisar siempre al primer fallo; los siguientes solo con diagnósticos activados
        if (!_storageUnavailable || _options.EnableDiagnostics)
        {
            Console.WriteLine($"[Hubble] No se pudieron guardar {batchSize} logs en MongoDB ({ex.GetType().Name}: {Shorten(ex.Message)}). " +
                              $"Los logs se descartarán hasta que MongoDB vuelva a estar disponible.");
        }

        _storageUnavailable = true;
    }

    private static string Shorten(string message)
    {
        // Los errores de selección de servidor incluyen la descripción completa del clúster
        const int maxLength = 200;
        return message.Length <= maxLength ? message : message.Substring(0, maxLength) + "...";
    }
}
