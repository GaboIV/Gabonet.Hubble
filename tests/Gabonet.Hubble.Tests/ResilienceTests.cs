namespace Gabonet.Hubble.Tests;

using System.Diagnostics;
using Gabonet.Hubble.Tests.Support;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Logging;
using Xunit;

/// <summary>
/// Con MongoDB caído, la aplicación anfitriona no debe esperar a Hubble ni al arrancar ni en cada solicitud.
/// </summary>
public class ResilienceTests
{
    private static readonly TimeSpan Budget = TimeSpan.FromSeconds(2);

    [Fact]
    public async Task Startup_requests_and_shutdown_do_not_wait_for_an_unreachable_mongodb()
    {
        var stopwatch = Stopwatch.StartNew();
        var app = await TestApp.StartAsync(
            options =>
            {
                // Sin serverSelectionTimeoutMS en la cadena: se usa el timeout por defecto de Hubble
                options.ConnectionString = "mongodb://127.0.0.1:27999";
                options.EnableDataPrune = true;
            },
            endpoints: a => a.MapGet("/app", (ILoggerFactory factory) =>
            {
                factory.CreateLogger("Orders").LogInformation("handling request");
                return "app ok";
            }),
            captureLogger: true);
        Assert.True(stopwatch.Elapsed < Budget, $"Startup took {stopwatch.Elapsed}");

        var client = app.Client();
        for (var i = 0; i < 3; i++)
        {
            stopwatch.Restart();
            var response = await client.SendAsync(TestApp.Request(HttpMethod.Get, "/app"));
            Assert.Equal("app ok", await response.Content.ReadAsStringAsync());
            Assert.True(stopwatch.Elapsed < Budget, $"Request {i} took {stopwatch.Elapsed}");
        }

        stopwatch.Restart();
        await app.DisposeAsync();
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(7), $"Shutdown took {stopwatch.Elapsed}");
    }
}
