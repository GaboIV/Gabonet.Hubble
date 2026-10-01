namespace Gabonet.Hubble.Tests;

using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using Gabonet.Hubble.Interfaces;
using Gabonet.Hubble.Models;
using Gabonet.Hubble.Tests.Support;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using static Gabonet.Hubble.Tests.Support.TestApp;

public class DashboardSecurityTests
{
    private const string Username = "admin";
    private const string Password = "S3cret!";
    private const string Xss = "<script>alert('xss')</script>";
    private const string LogId = "507f1f77bcf86cd799439011";

    private readonly FakeHubbleService _logs = new();

    public DashboardSecurityTests()
    {
        _logs.Logs.Add(new GeneralLog
        {
            Id = LogId,
            Timestamp = DateTime.UtcNow,
            HttpUrl = "/api/" + Xss,
            QueryParams = "?q=" + Xss,
            Method = "POST",
            RequestData = "{\"name\":\"" + Xss + "\"}",
            ResponseData = Xss,
            ErrorMessage = "<img src=x onerror=alert(1)>",
            IsError = true,
            StatusCode = 500,
            ControllerName = "Users",
            ActionName = "Create' onmouseover='alert(1)",
            DatabaseQueries = new List<DatabaseQuery> { new() { Query = "SELECT '" + Xss + "'", DatabaseType = "Sql", DatabaseName = "Db" } }
        });
    }

    private Task<WebApplication> StartAsync(bool requireAuthentication = true, List<string>? allowedIps = null) =>
        TestApp.StartAsync(
            options =>
            {
                options.RequireAuthentication = requireAuthentication;
                options.Username = Username;
                options.Password = Password;
                if (allowedIps != null)
                {
                    options.Security.AllowedIps = allowedIps;
                }
            },
            services: services =>
            {
                services.AddScoped<IHubbleService>(_ => _logs);
                services.AddSingleton<IHubbleStatsService, FakeStatsService>();
            },
            endpoints: app =>
            {
                app.MapGet("/app", () => "app ok");
                app.MapGet("/hubblefoo", () => "host route");
            },
            removeBackgroundServices: true);

    private static async Task<string> LoginAsync(HttpClient client, string ip = "127.0.0.1")
    {
        var page = await client.SendAsync(Request(HttpMethod.Get, "/hubble", ip));
        var cookies = MergeCookies(page);
        var post = Request(HttpMethod.Post, "/hubble/login", ip, cookies);
        post.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["username"] = Username,
            ["password"] = Password,
            ["__RequestVerificationToken"] = AntiforgeryToken(await page.Content.ReadAsStringAsync())
        });

        var response = await client.SendAsync(post);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        return MergeCookies(response, cookies);
    }

    private static AuthenticationHeaderValue Basic(string user = Username, string password = Password) =>
        new("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{user}:{password}")));

    [Fact]
    public async Task Ip_allow_list_protects_only_the_dashboard()
    {
        await using var app = await StartAsync(requireAuthentication: false, allowedIps: new() { "127.0.0.1", "10.0.0.0/8" });
        var client = app.Client();

        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(Request(HttpMethod.Get, "/app", "8.8.8.8"))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.SendAsync(Request(HttpMethod.Get, "/hubble", "8.8.8.8"))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.SendAsync(Request(HttpMethod.Get, "/hubble/api/logs", "8.8.8.8"))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(Request(HttpMethod.Get, "/hubble", "10.20.30.40"))).StatusCode);
    }

    [Fact]
    public async Task Base_path_is_matched_by_segment()
    {
        await using var app = await StartAsync();
        var response = await app.Client().SendAsync(Request(HttpMethod.Get, "/hubblefoo"));

        Assert.Equal("host route", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Login_page_has_antiforgery_token_and_security_headers()
    {
        await using var app = await StartAsync();
        var response = await app.Client().SendAsync(Request(HttpMethod.Get, "/hubble"));
        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("login-form", html);
        Assert.NotEmpty(AntiforgeryToken(html));
        Assert.True(response.Headers.Contains("Content-Security-Policy"));
        Assert.Equal("DENY", response.Headers.GetValues("X-Frame-Options").Single());
        Assert.True(response.Headers.CacheControl?.NoStore);
    }

    [Fact]
    public async Task Api_requires_authentication()
    {
        await using var app = await StartAsync();
        var response = await app.Client().SendAsync(Request(HttpMethod.Get, "/hubble/api/logs"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.NotEmpty(response.Headers.WwwAuthenticate);
    }

    [Fact]
    public async Task Forged_and_tampered_session_cookies_are_rejected()
    {
        await using var app = await StartAsync();
        var client = app.Client();

        var forged = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{Username}:{DateTime.UtcNow.Ticks}"));
        var response = await client.SendAsync(Request(HttpMethod.Get, "/hubble/api/logs", cookies: $"HubbleAuth={forged}"));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        var session = await LoginAsync(client);
        var tampered = Regex.Replace(session, "HubbleAuth=([^;]+)", m => "HubbleAuth=" + m.Groups[1].Value[..^4] + "AAAA");
        response = await client.SendAsync(Request(HttpMethod.Get, "/hubble/api/logs", cookies: tampered));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Valid_login_issues_a_hardened_session_cookie()
    {
        await using var app = await StartAsync();
        var client = app.Client();
        var page = await client.SendAsync(Request(HttpMethod.Get, "/hubble"));
        var post = Request(HttpMethod.Post, "/hubble/login", cookies: MergeCookies(page));
        post.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["username"] = Username,
            ["password"] = Password,
            ["__RequestVerificationToken"] = AntiforgeryToken(await page.Content.ReadAsStringAsync())
        });

        var response = await client.SendAsync(post);
        var setCookie = string.Join(" | ", response.Headers.GetValues("Set-Cookie")).ToLowerInvariant();

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("hubbleauth=", setCookie);
        Assert.Contains("httponly", setCookie);
        Assert.Contains("samesite=strict", setCookie);
    }

    [Fact]
    public async Task Login_without_antiforgery_token_is_rejected()
    {
        await using var app = await StartAsync();
        var client = app.Client();
        var page = await client.SendAsync(Request(HttpMethod.Get, "/hubble"));
        var post = Request(HttpMethod.Post, "/hubble/login", cookies: MergeCookies(page));
        post.Content = new FormUrlEncodedContent(new Dictionary<string, string> { ["username"] = Username, ["password"] = Password });

        Assert.Equal(HttpStatusCode.BadRequest, (await client.SendAsync(post)).StatusCode);
    }

    [Fact]
    public async Task Repeated_failed_logins_lock_the_client_out()
    {
        await using var app = await StartAsync();
        var client = app.Client();
        const string ip = "192.0.2.10";
        HttpStatusCode last = 0;

        for (var attempt = 0; attempt < 6; attempt++)
        {
            var page = await client.SendAsync(Request(HttpMethod.Get, "/hubble", ip));
            var post = Request(HttpMethod.Post, "/hubble/login", ip, MergeCookies(page));
            post.Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["username"] = Username,
                ["password"] = "wrong" + attempt,
                ["__RequestVerificationToken"] = AntiforgeryToken(await page.Content.ReadAsStringAsync())
            });
            last = (await client.SendAsync(post)).StatusCode;
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, last);

        var api = Request(HttpMethod.Get, "/hubble/api/logs", ip);
        api.Headers.Authorization = Basic();
        Assert.Equal(HttpStatusCode.TooManyRequests, (await client.SendAsync(api)).StatusCode);
    }

    [Fact]
    public async Task Captured_data_is_html_encoded()
    {
        await using var app = await StartAsync();
        var client = app.Client();
        var session = await LoginAsync(client);

        var home = await (await client.SendAsync(Request(HttpMethod.Get, "/hubble?url=" + Uri.EscapeDataString("'><script>alert(2)</script>"), cookies: session)))
            .Content.ReadAsStringAsync();
        var detail = await (await client.SendAsync(Request(HttpMethod.Get, $"/hubble/detail/{LogId}", cookies: session)))
            .Content.ReadAsStringAsync();

        Assert.DoesNotContain(Xss, home);
        Assert.Contains("&lt;script&gt;", home);
        Assert.DoesNotContain("'><script>alert(2)", home);
        Assert.DoesNotContain("<script>alert", detail);
        Assert.DoesNotContain("<img src=x", detail);
        Assert.DoesNotContain("' onmouseover='", detail);
    }

    [Fact]
    public async Task Delete_all_requires_post_and_antiforgery_token()
    {
        await using var app = await StartAsync();
        var client = app.Client();
        var session = await LoginAsync(client);
        var homeResponse = await client.SendAsync(Request(HttpMethod.Get, "/hubble", cookies: session));
        session = MergeCookies(homeResponse, session);
        var token = AntiforgeryToken(await homeResponse.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.MethodNotAllowed, (await client.SendAsync(Request(HttpMethod.Get, "/hubble/delete-all", cookies: session))).StatusCode);

        var withoutToken = Request(HttpMethod.Post, "/hubble/delete-all", cookies: session);
        withoutToken.Content = new FormUrlEncodedContent(new Dictionary<string, string>());
        Assert.Equal(HttpStatusCode.BadRequest, (await client.SendAsync(withoutToken)).StatusCode);
        Assert.Single(_logs.Logs);

        var withToken = Request(HttpMethod.Post, "/hubble/delete-all", cookies: session);
        withToken.Content = new FormUrlEncodedContent(new Dictionary<string, string> { ["__RequestVerificationToken"] = token });
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(withToken)).StatusCode);
        Assert.Empty(_logs.Logs);
    }

    [Fact]
    public async Task Api_state_changes_require_post_with_json()
    {
        await using var app = await StartAsync();
        var client = app.Client();

        var get = Request(HttpMethod.Get, "/hubble/api/prune");
        get.Headers.Authorization = Basic();
        Assert.Equal(HttpStatusCode.MethodNotAllowed, (await client.SendAsync(get)).StatusCode);

        var textPost = Request(HttpMethod.Post, "/hubble/api/prune");
        textPost.Headers.Authorization = Basic();
        textPost.Content = new StringContent("", Encoding.UTF8, "text/plain");
        Assert.Equal(HttpStatusCode.UnsupportedMediaType, (await client.SendAsync(textPost)).StatusCode);

        var delete = Request(HttpMethod.Delete, "/hubble/api/logs");
        delete.Headers.Authorization = Basic();
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(delete)).StatusCode);
        Assert.Empty(_logs.Logs);
    }

    [Fact]
    public async Task Page_size_is_capped()
    {
        await using var app = await StartAsync();
        var request = Request(HttpMethod.Get, "/hubble/api/logs?pageSize=100000");
        request.Headers.Authorization = Basic();

        await app.Client().SendAsync(request);

        Assert.Equal(200, _logs.LastPageSize);
    }
}
