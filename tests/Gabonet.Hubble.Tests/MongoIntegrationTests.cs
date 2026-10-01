namespace Gabonet.Hubble.Tests;

using Gabonet.Hubble.BackgroundServices;
using Gabonet.Hubble.Middleware;
using Gabonet.Hubble.Models;
using Gabonet.Hubble.Services;
using Gabonet.Hubble.Tests.Support;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

/// <summary>
/// Índices, retención TTL y recorrido completo contra un MongoDB real (requiere HUBBLE_TEST_MONGODB).
/// Cada test usa su propia base de datos y la elimina al terminar.
/// </summary>
public class MongoIntegrationTests : IAsyncLifetime
{
    private readonly string _databaseName = "HubbleTests_" + Guid.NewGuid().ToString("N")[..12];
    private MongoClient? _client;

    private MongoClient Client => _client ??= new MongoClient(MongoFactAttribute.ConnectionString);

    private IMongoCollection<GeneralLog> Logs => Client.GetDatabase(_databaseName).GetCollection<GeneralLog>("HubbleLogs");

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        if (!string.IsNullOrWhiteSpace(MongoFactAttribute.ConnectionString))
        {
            await Client.DropDatabaseAsync(_databaseName);
        }
    }

    private Task EnsureIndexesAsync(bool retention, int maxAgeHours = 24) =>
        new HubbleStorageInitializer(Logs, new HubbleOptions { EnableDataPrune = retention, MaxLogAgeHours = maxAgeHours }, new HubbleStorageStatus())
            .EnsureIndexesAsync(CancellationToken.None);

    private async Task<List<BsonDocument>> IndexesAsync() => await (await Logs.Indexes.ListAsync()).ToListAsync();

    private static BsonDocument? Index(IEnumerable<BsonDocument> indexes, string name) =>
        indexes.FirstOrDefault(i => i["name"].AsString == name);

    private static long? Ttl(BsonDocument? index) =>
        index != null && index.TryGetValue("expireAfterSeconds", out var value) ? value.ToInt64() : null;

    [MongoFact]
    public async Task Creates_indexes_idempotently()
    {
        await EnsureIndexesAsync(retention: false);
        var indexes = await IndexesAsync();

        var timestamp = Index(indexes, HubbleStorageInitializer.TimestampIndexName);
        var related = Index(indexes, HubbleStorageInitializer.RelatedRequestIndexName);
        Assert.NotNull(timestamp);
        Assert.Null(Ttl(timestamp));
        Assert.True(related?.Contains("partialFilterExpression"));

        await EnsureIndexesAsync(retention: false);
        Assert.Equal(indexes.Count, (await IndexesAsync()).Count);
    }

    [MongoFact]
    public async Task Retention_settings_drive_the_ttl_index()
    {
        await EnsureIndexesAsync(retention: true, maxAgeHours: 24);
        Assert.Equal(86_400, Ttl(Index(await IndexesAsync(), HubbleStorageInitializer.TimestampIndexName)));

        await EnsureIndexesAsync(retention: true, maxAgeHours: 48);
        Assert.Equal(172_800, Ttl(Index(await IndexesAsync(), HubbleStorageInitializer.TimestampIndexName)));

        await EnsureIndexesAsync(retention: false);
        var timestamp = Index(await IndexesAsync(), HubbleStorageInitializer.TimestampIndexName);
        Assert.NotNull(timestamp);
        Assert.Null(Ttl(timestamp));
    }

    [MongoFact]
    public async Task Indexes_created_by_hand_are_respected()
    {
        // Como los crea mongosh: claves double
        await Client.GetDatabase(_databaseName).RunCommandAsync<BsonDocument>(BsonDocument.Parse(
            "{ createIndexes: 'HubbleLogs', indexes: [" +
            "  { key: { timestamp: 1.0 }, name: 'timestamp_1', expireAfterSeconds: 3600 }," +
            "  { key: { relatedRequestId: 1.0 }, name: 'relatedRequestId_1' } ] }"));

        await EnsureIndexesAsync(retention: false);
        var indexes = await IndexesAsync();
        Assert.Equal(3600, Ttl(Index(indexes, "timestamp_1")));
        Assert.Null(Index(indexes, HubbleStorageInitializer.TimestampIndexName));
        Assert.Null(Index(indexes, HubbleStorageInitializer.RelatedRequestIndexName));

        await EnsureIndexesAsync(retention: true, maxAgeHours: 24);
        indexes = await IndexesAsync();
        Assert.Null(Index(indexes, "timestamp_1"));
        Assert.Equal(86_400, Ttl(Index(indexes, HubbleStorageInitializer.TimestampIndexName)));
    }

    [MongoFact]
    public async Task End_to_end_capture_is_persisted_masked_and_shown_in_the_dashboard()
    {
        await using var app = await TestApp.StartAsync(
            options =>
            {
                options.ConnectionString = MongoFactAttribute.ConnectionString!;
                options.DatabaseName = _databaseName;
                options.EnableDataPrune = true;
                options.MaxLogAgeHours = 72;
            },
            endpoints: a => a.MapGet("/orders/{id}", (int id, ILoggerFactory factory) =>
            {
                factory.CreateLogger("Orders.Api").LogInformation("loading order {Id}", id);
                return Results.Ok(new { id, password = "s3cret" });
            }),
            captureLogger: true);
        var client = app.Client();

        await client.SendAsync(TestApp.Request(HttpMethod.Get, "/orders/7"));

        List<GeneralLog> stored = new();
        await Wait.UntilAsync(() =>
        {
            stored = Logs.Find(FilterDefinition<GeneralLog>.Empty).ToList();
            return stored.Count >= 2;
        }, seconds: 10);

        var requestLog = Assert.Single(stored, l => l.ControllerName != "ApplicationLogger");
        var appLog = Assert.Single(stored, l => l.RequestData == "Orders.Api");
        Assert.Equal(200, requestLog.StatusCode);
        Assert.Contains("*****", requestLog.ResponseData);
        Assert.DoesNotContain("s3cret", requestLog.ResponseData);
        Assert.Equal(requestLog.Id, appLog.RelatedRequestId);

        var status = app.Services.GetRequiredService<HubbleStorageStatus>();
        await Wait.UntilAsync(() => status.IndexesReady, seconds: 10);
        Assert.True(status.IndexesReady);
        Assert.Equal(72 * 3600, Ttl(Index(await IndexesAsync(), HubbleStorageInitializer.TimestampIndexName)));

        var home = await (await client.SendAsync(TestApp.Request(HttpMethod.Get, "/hubble"))).Content.ReadAsStringAsync();
        var detail = await (await client.SendAsync(TestApp.Request(HttpMethod.Get, $"/hubble/detail/{requestLog.Id}"))).Content.ReadAsStringAsync();
        var config = await (await client.SendAsync(TestApp.Request(HttpMethod.Get, "/hubble/config"))).Content.ReadAsStringAsync();
        Assert.Contains("/orders/7", home);
        Assert.Contains("loading order 7", detail);
        Assert.Contains("72 horas", config);
        Assert.Contains("Creados", config);
    }
}
