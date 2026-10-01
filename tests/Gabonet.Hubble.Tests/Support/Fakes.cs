namespace Gabonet.Hubble.Tests.Support;

using System.Reflection;
using Gabonet.Hubble.Interfaces;
using Gabonet.Hubble.Models;
using Microsoft.Extensions.Logging;

/// <summary>
/// Servicio de logs en memoria para probar el dashboard sin MongoDB.
/// </summary>
internal sealed class FakeHubbleService : IHubbleService
{
    public List<GeneralLog> Logs { get; } = new();

    public int LastPageSize { get; private set; }

    public Task<List<GeneralLog>> GetAllLogsAsync() => Task.FromResult(Logs.ToList());

    public Task<GeneralLog> GetLogByIdAsync(string id) => Task.FromResult(Logs.FirstOrDefault(l => l.Id == id)!);

    public Task<List<GeneralLog>> GetFilteredLogsAsync(string? method = null, string? url = null, int page = 1, int pageSize = 50) =>
        Task.FromResult(Logs.ToList());

    public Task<List<GeneralLog>> GetFilteredLogsWithRelatedAsync(string? method = null, string? url = null, bool excludeRelatedLogs = true, int page = 1, int pageSize = 50)
    {
        LastPageSize = pageSize;
        return Task.FromResult(Logs.ToList());
    }

    public Task CreateLogAsync(GeneralLog newLog) => Task.CompletedTask;

    public Task UpdateLogAsync(string id, GeneralLog updatedLog) => Task.CompletedTask;

    public Task DeleteLogAsync(string id) => Task.CompletedTask;

    public Task DeleteAllLogsAsync()
    {
        Logs.Clear();
        return Task.CompletedTask;
    }

    public Task LogAsync(string title, string logType, string serviceName, string responseOrError, int statusCode, string route, string method,
        string? request, bool isError, string? errorMessage = null, string? stackTrace = null, string? errorDetails = null, long? executionTime = null) =>
        Task.CompletedTask;

    public Task LogApplicationLogAsync(string category, LogLevel logLevel, string message, Exception? exception = null) => Task.CompletedTask;

    public Task<List<GeneralLog>> GetRelatedLogsAsync(string requestId) => Task.FromResult(new List<GeneralLog>());

    public Task<long> GetTotalLogsCountAsync(string? method = null, string? url = null, bool excludeRelatedLogs = true) =>
        Task.FromResult((long)Logs.Count);

    public Task<long> DeleteLogsOlderThanAsync(DateTime cutoffDate) => Task.FromResult(0L);
}

/// <summary>
/// Servicio de estadísticas en memoria.
/// </summary>
internal sealed class FakeStatsService : IHubbleStatsService
{
    public Task<HubbleStatistics> GetStatisticsAsync() => Task.FromResult(new HubbleStatistics());

    public Task<HubbleStatistics> UpdatePruneStatisticsAsync(DateTime pruneDate, long logsDeleted) => Task.FromResult(new HubbleStatistics());

    public Task<HubbleSystemConfiguration> GetSystemConfigurationAsync() => Task.FromResult(new HubbleSystemConfiguration());

    public Task<HubbleSystemConfiguration> SaveSystemConfigurationAsync(HubbleSystemConfiguration config) => Task.FromResult(config);

    public Task<HubbleStatistics> RecalculateStatisticsAsync() => Task.FromResult(new HubbleStatistics());
}

/// <summary>
/// Colección de MongoDB falsa (solo InsertManyAsync) para probar el writer en segundo plano.
/// Se crea con <c>DispatchProxy.Create&lt;IMongoCollection&lt;GeneralLog&gt;, FakeLogsCollection&gt;()</c>.
/// </summary>
public class FakeLogsCollection : DispatchProxy
{
    private int _attempts;

    public List<GeneralLog> Inserted { get; } = new();

    public List<int> BatchSizes { get; } = new();

    public int Attempts => Volatile.Read(ref _attempts);

    public volatile bool Fail;

    public TimeSpan Delay { get; set; } = TimeSpan.Zero;

    public int InsertedCount
    {
        get { lock (Inserted) { return Inserted.Count; } }
    }

    public bool HasInserted(string url)
    {
        lock (Inserted)
        {
            return Inserted.Any(l => l.HttpUrl == url);
        }
    }

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        if (targetMethod!.Name == "InsertManyAsync" && args![0] is IEnumerable<GeneralLog> documents)
        {
            Interlocked.Increment(ref _attempts);
            return InsertAsync(documents.ToList(), (CancellationToken)args[2]!);
        }

        throw new NotSupportedException(targetMethod.Name);
    }

    private async Task InsertAsync(List<GeneralLog> batch, CancellationToken cancellationToken)
    {
        if (Delay > TimeSpan.Zero)
        {
            await Task.Delay(Delay, cancellationToken);
        }

        if (Fail)
        {
            throw new TimeoutException("simulated: MongoDB unavailable");
        }

        lock (Inserted)
        {
            Inserted.AddRange(batch);
            BatchSizes.Add(batch.Count);
        }
    }
}

internal static class Wait
{
    public static async Task UntilAsync(Func<bool> condition, int seconds = 5)
    {
        var deadline = DateTime.UtcNow.AddSeconds(seconds);
        while (!condition() && DateTime.UtcNow < deadline)
        {
            await Task.Delay(20);
        }
    }
}
