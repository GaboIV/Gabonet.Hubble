namespace Gabonet.Hubble.BackgroundServices;

using Gabonet.Hubble.Middleware;
using Gabonet.Hubble.Models;
using Gabonet.Hubble.Services;
using Microsoft.Extensions.Hosting;
using MongoDB.Bson;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Crea en segundo plano los índices de la colección de logs y aplica la retención mediante un índice TTL de MongoDB.
/// Con la retención activa, es MongoDB quien elimina los logs con más de <see cref="HubbleOptions.MaxLogAgeHours"/> horas.
/// </summary>
internal sealed class HubbleStorageInitializer : BackgroundService
{
    /// <summary>
    /// Índice sobre la fecha del log: ordena el listado del dashboard y, con la retención activa, es el índice TTL.
    /// </summary>
    internal const string TimestampIndexName = "hubble_timestamp";

    /// <summary>
    /// Índice parcial para obtener los logs de ILogger asociados a una solicitud.
    /// </summary>
    internal const string RelatedRequestIndexName = "hubble_related_request";

    private const string TimestampField = "timestamp";
    private const string RelatedRequestField = "relatedRequestId";
    private const int IndexNotFoundErrorCode = 27;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(30);

    private readonly IMongoCollection<GeneralLog> _logsCollection;
    private readonly HubbleOptions _options;
    private readonly HubbleStorageStatus _status;

    public HubbleStorageInitializer(IMongoCollection<GeneralLog> logsCollection, HubbleOptions options, HubbleStorageStatus status)
    {
        _logsCollection = logsCollection;
        _options = options;
        _status = status;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Ceder el control de inmediato: el arranque de la aplicación no debe esperar a MongoDB
        await Task.Yield();

        var failureReported = false;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await EnsureIndexesAsync(stoppingToken);
                _status.MarkReady();

                if (failureReported)
                {
                    Console.WriteLine("[Hubble] Índices de MongoDB creados correctamente tras el reintento.");
                }

                return;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _status.MarkFailed($"{ex.GetType().Name}: {ex.Message}");

                if (!failureReported || _options.EnableDiagnostics)
                {
                    var message = ex.Message.Length > 200 ? ex.Message.Substring(0, 200) + "..." : ex.Message;
                    Console.WriteLine($"[Hubble] No se pudieron preparar los índices de MongoDB ({ex.GetType().Name}: {message}). " +
                                      $"Se reintentará cada {RetryDelay.TotalSeconds:0} segundos.");
                }

                failureReported = true;
            }

            try
            {
                await Task.Delay(RetryDelay, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    /// <summary>
    /// Crea o ajusta los índices. Es idempotente: si ya están como deben, no modifica nada.
    /// </summary>
    internal async Task EnsureIndexesAsync(CancellationToken cancellationToken)
    {
        var existingIndexes = await (await _logsCollection.Indexes.ListAsync(cancellationToken)).ToListAsync(cancellationToken);

        await EnsureTimestampIndexAsync(existingIndexes, cancellationToken);
        await EnsureRelatedRequestIndexAsync(existingIndexes, cancellationToken);
    }

    private async Task EnsureTimestampIndexAsync(List<BsonDocument> existingIndexes, CancellationToken cancellationToken)
    {
        var keys = new BsonDocument(TimestampField, 1);
        var existing = existingIndexes.FirstOrDefault(index => KeysEqual(index, keys));
        TimeSpan? desiredTtl = _options.EnableDataPrune ? TimeSpan.FromHours(_options.MaxLogAgeHours) : null;

        if (existing != null)
        {
            var existingTtl = GetExpireAfter(existing);
            var isOwnIndex = existing.GetValue("name", string.Empty).AsString == TimestampIndexName;

            // Ya está como debe estar
            if (existingTtl == desiredTtl)
            {
                return;
            }

            // Retención desactivada y el TTL lo creó el usuario a mano: respetarlo
            if (desiredTtl == null && !isOwnIndex)
            {
                return;
            }

            // Hay que cambiar el TTL: se recrea el índice (drop + create solo requiere el rol readWrite)
            await DropIndexAsync(existing.GetValue("name").AsString, cancellationToken);
        }

        var options = new CreateIndexOptions { Name = TimestampIndexName };
        if (desiredTtl != null)
        {
            options.ExpireAfter = desiredTtl;
        }

        await _logsCollection.Indexes.CreateOneAsync(
            new CreateIndexModel<GeneralLog>(keys, options),
            cancellationToken: cancellationToken);
    }

    private async Task EnsureRelatedRequestIndexAsync(List<BsonDocument> existingIndexes, CancellationToken cancellationToken)
    {
        var keys = new BsonDocument(RelatedRequestField, 1);
        if (existingIndexes.Any(index => KeysEqual(index, keys)))
        {
            return;
        }

        // Índice parcial: solo los logs de ILogger asociados a una solicitud tienen relatedRequestId
        var options = new CreateIndexOptions<GeneralLog>
        {
            Name = RelatedRequestIndexName,
            PartialFilterExpression = new BsonDocument(RelatedRequestField, new BsonDocument("$type", "string"))
        };

        await _logsCollection.Indexes.CreateOneAsync(
            new CreateIndexModel<GeneralLog>(keys, options),
            cancellationToken: cancellationToken);
    }

    private async Task DropIndexAsync(string name, CancellationToken cancellationToken)
    {
        try
        {
            await _logsCollection.Indexes.DropOneAsync(name, cancellationToken);
        }
        catch (MongoCommandException ex) when (ex.Code == IndexNotFoundErrorCode)
        {
            // Otra instancia de la aplicación ya lo eliminó
        }
    }

    private static TimeSpan? GetExpireAfter(BsonDocument index)
    {
        return index.TryGetValue("expireAfterSeconds", out var value) && value.IsNumeric
            ? TimeSpan.FromSeconds(value.ToDouble())
            : null;
    }

    /// <summary>
    /// Compara la definición de claves de un índice existente con la deseada. Los números se comparan por valor,
    /// porque los índices creados desde mongosh usan dobles (1.0) y los del driver enteros (1).
    /// </summary>
    private static bool KeysEqual(BsonDocument index, BsonDocument expectedKeys)
    {
        if (!index.TryGetValue("key", out var keyValue) || !keyValue.IsBsonDocument)
        {
            return false;
        }

        var keys = keyValue.AsBsonDocument;
        if (keys.ElementCount != expectedKeys.ElementCount)
        {
            return false;
        }

        for (var i = 0; i < keys.ElementCount; i++)
        {
            var actual = keys.GetElement(i);
            var expected = expectedKeys.GetElement(i);

            if (actual.Name != expected.Name)
            {
                return false;
            }

            var sameValue = actual.Value.IsNumeric && expected.Value.IsNumeric
                ? actual.Value.ToDouble() == expected.Value.ToDouble()
                : actual.Value.Equals(expected.Value);

            if (!sameValue)
            {
                return false;
            }
        }

        return true;
    }
}
