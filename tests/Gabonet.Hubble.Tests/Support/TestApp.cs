namespace Gabonet.Hubble.Tests.Support;

using System.Net;
using System.Text.RegularExpressions;
using Gabonet.Hubble.Extensions;
using Gabonet.Hubble.Middleware;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

/// <summary>
/// Crea aplicaciones de prueba en memoria (TestServer) con Hubble configurado.
/// </summary>
internal static class TestApp
{
    /// <summary>
    /// Puerto sin servidor: simula MongoDB caído. El timeout corto evita esperas en los servicios en segundo plano.
    /// </summary>
    public const string UnreachableMongo = "mongodb://127.0.0.1:27999/?serverSelectionTimeoutMS=1000";

    /// <summary>
    /// Cabecera que los tests usan para simular la IP del cliente.
    /// </summary>
    public const string ClientIpHeader = "X-Test-IP";

    public static async Task<WebApplication> StartAsync(
        Action<HubbleOptions>? configure = null,
        Action<IServiceCollection>? services = null,
        Action<WebApplication>? beforeHubble = null,
        Action<WebApplication>? endpoints = null,
        bool captureLogger = false,
        bool removeBackgroundServices = false)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();

        builder.Services.AddHubble(options =>
        {
            options.ConnectionString = UnreachableMongo;
            options.DatabaseName = "HubbleTests";
            configure?.Invoke(options);
        });

        if (captureLogger)
        {
            builder.Logging.AddHubbleLogging();
        }

        if (removeBackgroundServices)
        {
            // Quita el writer y el inicializador de índices para poder inspeccionar la cola directamente
            foreach (var descriptor in builder.Services
                         .Where(d => d.ServiceType == typeof(IHostedService) && d.ImplementationFactory != null)
                         .ToList())
            {
                builder.Services.Remove(descriptor);
            }
        }

        services?.Invoke(builder.Services);

        var app = builder.Build();
        app.Use((context, next) =>
        {
            var ip = context.Request.Headers[ClientIpHeader].FirstOrDefault() ?? "127.0.0.1";
            context.Connection.RemoteIpAddress = IPAddress.Parse(ip);
            return next();
        });

        beforeHubble?.Invoke(app);
        app.UseHubble();
        endpoints?.Invoke(app);

        await app.StartAsync();
        return app;
    }

    public static HttpClient Client(this WebApplication app) => app.GetTestServer().CreateClient();

    public static HttpRequestMessage Request(HttpMethod method, string url, string ip = "127.0.0.1", string? cookies = null)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Add(ClientIpHeader, ip);
        if (cookies != null)
        {
            request.Headers.Add("Cookie", cookies);
        }

        return request;
    }

    /// <summary>
    /// Combina las cookies existentes con las que establece la respuesta (Set-Cookie).
    /// </summary>
    public static string MergeCookies(HttpResponseMessage response, string? existing = null)
    {
        var jar = new Dictionary<string, string>();

        if (existing != null)
        {
            foreach (var part in existing.Split("; ", StringSplitOptions.RemoveEmptyEntries))
            {
                var pair = part.Split('=', 2);
                jar[pair[0]] = pair[1];
            }
        }

        if (response.Headers.TryGetValues("Set-Cookie", out var setCookies))
        {
            foreach (var setCookie in setCookies)
            {
                var pair = setCookie.Split(';')[0].Split('=', 2);
                jar[pair[0]] = pair[1];
            }
        }

        return string.Join("; ", jar.Select(p => $"{p.Key}={p.Value}"));
    }

    /// <summary>
    /// Extrae el token antiforgery de un formulario del dashboard.
    /// </summary>
    public static string AntiforgeryToken(string html) =>
        Regex.Match(html, "name='__RequestVerificationToken' value='([^']+)'").Groups[1].Value;
}
