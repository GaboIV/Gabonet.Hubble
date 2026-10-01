namespace Gabonet.Hubble.Tests;

using System.Net;
using System.Reflection;
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

public class CaptureTests
{
    private static List<GeneralLog> Drain(HubbleLogQueue queue)
    {
        var items = new List<GeneralLog>();
        while (queue.Reader.TryRead(out var log))
        {
            items.Add(log);
        }

        return items;
    }

    private static Task<WebApplication> StartAsync(bool captureHttp = true) =>
        TestApp.StartAsync(
            options => options.CaptureHttpRequests = captureHttp,
            beforeHubble: app => app.UseExceptionHandler(error => error.Run(async context =>
            {
                context.Response.StatusCode = 500;
                await context.Response.WriteAsync("handled by host");
            })),
            endpoints: app =>
            {
                app.MapGet("/app", (ILoggerFactory factory) =>
                {
                    factory.CreateLogger("Orders.Controller").LogInformation("processing order {Id}", 42);
                    factory.CreateLogger("Gabonet.Hubble.Internal").LogWarning("internal message");
                    return "app ok";
                });
                app.MapGet("/boom", () => { throw new InvalidOperationException("kaboom"); });
            },
            captureLogger: true,
            removeBackgroundServices: true);

    [Fact]
    public async Task Request_and_logger_entries_are_enqueued_and_linked()
    {
        await using var app = await StartAsync();
        var queue = app.Services.GetRequiredService<HubbleLogQueue>();

        var response = await app.Client().SendAsync(TestApp.Request(HttpMethod.Get, "/app", "203.0.113.5"));
        var logs = Drain(queue);
        var requestLog = Assert.Single(logs, l => l.ControllerName != "ApplicationLogger");
        var appLog = Assert.Single(logs, l => l.RequestData == "Orders.Controller");

        Assert.Equal("app ok", await response.Content.ReadAsStringAsync());
        Assert.Equal("/app", requestLog.HttpUrl);
        Assert.Equal(200, requestLog.StatusCode);
        Assert.Equal("app ok", requestLog.ResponseData);
        Assert.Equal("203.0.113.5", requestLog.IpAddress);
        Assert.True(ObjectId.TryParse(requestLog.Id, out _));
        Assert.Equal(requestLog.Id, appLog.RelatedRequestId);
        Assert.StartsWith("processing order 42", appLog.ResponseData);
        Assert.DoesNotContain(logs, l => l.RequestData == "Gabonet.Hubble.Internal");
    }

    [Fact]
    public async Task Unhandled_exception_is_recorded_and_host_handler_still_responds()
    {
        await using var app = await StartAsync();
        var queue = app.Services.GetRequiredService<HubbleLogQueue>();

        var response = await app.Client().SendAsync(TestApp.Request(HttpMethod.Get, "/boom"));
        var logs = Drain(queue);
        var errorLog = Assert.Single(logs, l => l.HttpUrl == "/boom" && l.ControllerName != "ApplicationLogger");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("handled by host", await response.Content.ReadAsStringAsync());
        Assert.True(errorLog.IsError);
        Assert.Equal(500, errorLog.StatusCode);
        Assert.Equal("kaboom", errorLog.ErrorMessage);
        Assert.Contains(logs, l => l.ControllerName == "ApplicationLogger" && l.RelatedRequestId == errorLog.Id);
    }

    [Fact]
    public async Task CaptureHttpRequests_false_disables_http_capture()
    {
        await using var app = await StartAsync(captureHttp: false);
        var queue = app.Services.GetRequiredService<HubbleLogQueue>();

        await app.Client().SendAsync(TestApp.Request(HttpMethod.Get, "/app"));

        Assert.DoesNotContain(Drain(queue), l => l.ControllerName != "ApplicationLogger");
    }

    [Fact]
    public void Full_queue_drops_new_logs()
    {
        var queue = new HubbleLogQueue(capacity: 3);

        var accepted = Enumerable.Range(0, 5).Count(_ => queue.TryEnqueue(new GeneralLog()));

        Assert.Equal(3, accepted);
        Assert.Equal(2, queue.DroppedCount);
    }

    [Fact]
    public async Task Writer_persists_in_batches_recovers_and_flushes_on_shutdown()
    {
        var collection = DispatchProxy.Create<IMongoCollection<GeneralLog>, FakeLogsCollection>();
        var fake = (FakeLogsCollection)(object)collection;
        var queue = new HubbleLogQueue();
        var writer = new HubbleLogWriterService(queue, collection, new HubbleOptions());

        for (var i = 0; i < 450; i++)
        {
            queue.TryEnqueue(new GeneralLog { HttpUrl = "/batch" });
        }

        await writer.StartAsync(CancellationToken.None);
        await Wait.UntilAsync(() => fake.InsertedCount == 450);
        Assert.Equal(450, fake.InsertedCount);
        Assert.All(fake.BatchSizes, size => Assert.True(size <= 200));

        // MongoDB cae y vuelve
        fake.Fail = true;
        var attemptsBefore = fake.Attempts;
        queue.TryEnqueue(new GeneralLog { HttpUrl = "/lost" });
        await Wait.UntilAsync(() => fake.Attempts > attemptsBefore);
        fake.Fail = false;
        queue.TryEnqueue(new GeneralLog { HttpUrl = "/after-recovery" });
        await Wait.UntilAsync(() => fake.HasInserted("/after-recovery"), seconds: 15);
        Assert.True(fake.HasInserted("/after-recovery"));

        // Lo pendiente se guarda al apagar
        fake.Delay = TimeSpan.FromMilliseconds(300);
        for (var i = 0; i < 10; i++)
        {
            queue.TryEnqueue(new GeneralLog { HttpUrl = "/shutdown" });
        }

        await writer.StopAsync(CancellationToken.None);
        lock (fake.Inserted)
        {
            Assert.Equal(10, fake.Inserted.Count(l => l.HttpUrl == "/shutdown"));
        }
    }
}
