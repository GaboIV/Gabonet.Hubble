# Hubble for .NET

Hubble is an embedded monitoring and logging dashboard for ASP.NET Core applications, in the spirit of Laravel Telescope. It captures HTTP requests and responses, `ILogger` messages and database queries, correlates them per request, stores them in MongoDB and shows them in a built-in web UI — no extra service to deploy.

- [Features](#features)
- [Requirements](#requirements)
- [Quick start](#quick-start)
- [Configuration](#configuration)
- [Securing the dashboard](#securing-the-dashboard)
- [Capturing ILogger messages](#capturing-ilogger-messages)
- [Capturing database queries](#capturing-database-queries)
- [Masking sensitive data](#masking-sensitive-data)
- [How capture works (and what happens if MongoDB is down)](#how-capture-works-and-what-happens-if-mongodb-is-down)
- [Data retention and MongoDB setup](#data-retention-and-mongodb-setup)
- [Healthy implementation checklist](#healthy-implementation-checklist)
- [Web UI and REST API](#web-ui-and-rest-api)
- [Building from source](#building-from-source)
- [Troubleshooting](#troubleshooting)
- [Known limitations](#known-limitations)
- [Upgrade notes](#upgrade-notes)

## Features

- **HTTP capture**: method, path, query string, headers, request and response bodies, status code and duration.
- **ILogger correlation**: log messages written while a request is being processed are linked to that request and shown in its detail page.
- **Database queries**: SQL captured through Entity Framework Core interceptors or ADO.NET, plus an example for MongoDB command monitoring.
- **Data masking**: case-insensitive, recursive masking of JSON properties and headers, with a helper for your own logs.
- **Built-in dashboard**: filtering, search, pagination and a JSON REST API.
- **Dashboard security**: optional login with a signed session cookie, CSRF protection, brute-force lockout, IP/CIDR allow-list and hardened response headers.
- **Non-blocking capture**: logs are queued in memory and written to MongoDB in the background, so a slow or unavailable MongoDB never slows down or breaks your application.
- **Retention**: optional automatic pruning of old logs.

## Requirements

| Component | Version |
|---|---|
| Host application | ASP.NET Core on .NET 6, 7, 8 or later (the package targets `net6.0`, `net7.0` and `net8.0`; .NET 9+ uses the `net8.0` build) |
| Storage | A MongoDB server supported by `MongoDB.Driver` 3.2 |
| Optional | Entity Framework Core for SQL capture (the package depends on EF Core 6, 7 or 8 to match your target framework) |

## Quick start

### 1. Install the package

```bash
dotnet add package Gabonet.Hubble
```

### 2. Register and enable Hubble

```csharp
using Gabonet.Hubble.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHubble(options =>
{
    options.ConnectionString = builder.Configuration.GetConnectionString("Hubble")!; // required
    options.DatabaseName = "HubbleDB";                                                 // required
    options.ServiceName = "Orders.Api";

    // Never monitor health checks, metrics, docs or streaming endpoints (see "Known limitations")
    options.IgnorePaths = new List<string> { "/health", "/metrics", "/swagger" };

    // Protect the dashboard (credentials come from configuration / secrets, never source code)
    options.RequireAuthentication = true;
    options.Username = builder.Configuration["Hubble:Username"]!;
    options.Password = builder.Configuration["Hubble:Password"]!;
});

// Optional: capture ILogger messages and link them to the current request
builder.Logging.AddHubbleLogging(LogLevel.Information);

var app = builder.Build();

app.UseHubble(); // before UseRouting() so that every request is captured

app.UseRouting();
app.MapControllers();

app.Run();
```

### 3. Open the dashboard

Browse to `https://your-app/hubble` (or the `BasePath` you configured).

### Middleware order

The position of `UseHubble()` matters:

```csharp
var app = builder.Build();

// 1. Behind a reverse proxy / load balancer: restore the real client IP first,
//    otherwise AllowedIps and the login lockout only ever see the proxy's address.
app.UseForwardedHeaders();

// 2. Global exception handler (optional). Placed before Hubble, it still receives the
//    exception after Hubble has recorded it, because Hubble re-throws.
app.UseExceptionHandler("/error");

// 3. Hubble: capture middleware + dashboard.
app.UseHubble();

// 4. The rest of your pipeline.
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
```

To trust forwarded headers safely, tell ASP.NET Core which proxies are yours:

```csharp
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownProxies.Add(IPAddress.Parse("10.0.0.10")); // your proxy
});
```

## Configuration

Hubble can be configured in code or from `appsettings.json`.

### In code

```csharp
builder.Services.AddHubble(options =>
{
    options.ConnectionString = "mongodb://localhost:27017";
    options.DatabaseName = "HubbleDB";
    options.ServiceName = "Orders.Api";
    options.BasePath = "/hubble";
    options.TimeZoneId = "America/New_York";
    options.IgnorePaths = new List<string> { "/health", "/metrics" };

    options.EnableDataPrune = true; // MongoDB deletes logs older than MaxLogAgeHours (TTL index)
    options.MaxLogAgeHours = 72;

    options.RequireAuthentication = true;
    options.Username = builder.Configuration["Hubble:Username"]!;
    options.Password = builder.Configuration["Hubble:Password"]!;
    options.AllowDeleteAll = false;

    options.Security.MaskBodyProperties = new List<string> { "password", "token", "creditCard", "cvv" };
    options.Security.MaskRequestBodyProperties = new List<string> { "pin" };
    options.Security.MaskResponseBodyProperties = new List<string> { "internalId" };
    options.Security.MaskHeaders = new List<string> { "Authorization", "Cookie", "X-Api-Key" };
    options.Security.AllowedIps = new List<string> { "127.0.0.1", "10.0.0.0/8" };
});
```

### From appsettings.json

```json
{
  "ConnectionStrings": {
    "Hubble": "mongodb://hubble_user@mongo:27017/HubbleDB"
  },
  "Hubble": {
    "ServiceName": "Orders.Api",
    "BasePath": "/hubble",
    "RequireAuthentication": true,
    "Username": "admin",
    "IgnorePaths": [ "/health", "/metrics", "/swagger" ],
    "EnableDataPrune": true,
    "MaxLogAgeHours": 72,
    "AllowDeleteAll": false,
    "Security": {
      "MaskBodyProperties": [ "password", "token", "creditCard", "cvv" ],
      "MaskRequestBodyProperties": [ "pin" ],
      "MaskResponseBodyProperties": [ "internalId" ],
      "MaskHeaders": [ "Authorization", "Cookie", "X-Api-Key" ],
      "AllowedIps": [ "10.0.0.0/8" ]
    }
  }
}
```

```csharp
builder.Services.AddHubble(
    builder.Configuration.GetSection("Hubble"),
    options =>
    {
        // Runs after the section is read: complete or override values here
        options.ConnectionString = builder.Configuration.GetConnectionString("Hubble")!;
        options.DatabaseName = "HubbleDB";
    });
```

Lists in the section (`IgnorePaths`, `Security.MaskHeaders`, ...) **replace** the defaults instead of being appended to them. Every setting can also be placed in the section itself (`"ConnectionString"`, `"DatabaseName"`), in which case the second argument is optional.

`HubbleOptions` is also registered as `IOptions<HubbleOptions>` if you need to read the effective configuration elsewhere.

Keep the password out of the file. ASP.NET Core does **not** expand `${VARIABLES}` inside JSON; provide it through [user secrets](https://learn.microsoft.com/aspnet/core/security/app-secrets), a vault, or an environment variable using `__` as the section separator:

```bash
export Hubble__Password="a-long-random-password"
```

### Options reference

| Option | Default | Description |
|---|---|---|
| `ConnectionString` | — | **Required.** MongoDB connection string. Unless it sets `serverSelectionTimeoutMS`, Hubble uses a 5-second server selection timeout. |
| `DatabaseName` | — | **Required.** Database that stores the `HubbleLogs`, `HubbleStats` and `HubbleConfig` collections. |
| `ServiceName` | `HubbleService` | Name stored with every log entry. |
| `BasePath` | `/hubble` | Dashboard route. Matched by path segment (`/hubble` does not match `/hubblefoo`). Cannot be empty or `/`. |
| `PrefixPath` | `""` | Prefix prepended to dashboard links and the session cookie path when the app is served under a sub-path by a proxy. |
| `TimeZoneId` | `""` (UTC) | Time zone used to display timestamps (`TimeZoneInfo` id). |
| `IgnorePaths` | `[]` | Path prefixes that are never captured. |
| `IgnoreStaticFiles` | `true` | Skip common static file extensions. |
| `CaptureHttpRequests` | `true` | Capture HTTP traffic. When `false`, requests pass through untouched. |
| `CaptureLoggerMessages` | `true` | Capture `ILogger` messages (requires `AddHubbleLogging()`). |
| `MinimumLogLevel` | `Information` | Minimum `ILogger` level captured, unless `AddHubbleLogging(level)` sets one explicitly. |
| `EnableDiagnostics` | `false` | Write Hubble's internal errors to the console. |
| `EnableDataPrune` | `false` | Automatic retention: MongoDB deletes logs older than `MaxLogAgeHours` through a TTL index managed by Hubble. |
| `MaxLogAgeHours` | `24` | Retention in hours when `EnableDataPrune` is on (1 to 596,523). The app fails at startup if it is out of range. |
| `DataPruneIntervalHours` | `1` | No longer used (MongoDB checks for expired logs every minute). Kept for compatibility. |
| `HighlightNewServices` | `false` | Auto-refresh the list and highlight new entries. |
| `HighlightDurationSeconds` | `5` | How long new entries stay highlighted. |
| `RequireAuthentication` | `false` | Require a login for the dashboard and API. The app refuses to start if enabled with an empty `Username` or `Password`. |
| `Username` / `Password` | `""` | Dashboard credentials. |
| `AllowDeleteAll` | `true` | Allow deleting every log from the UI and API. |
| `Security.MaskBodyProperties` | see source | JSON properties masked in request **and** response bodies. |
| `Security.MaskRequestBodyProperties` | `[]` | Extra properties masked only in request bodies. |
| `Security.MaskResponseBodyProperties` | `[]` | Extra properties masked only in response bodies. |
| `Security.MaskHeaders` | `Authorization`, `X-Api-Key`, `Cookie` | Request headers stored as `*****`. |
| `Security.AllowedIps` | `[]` | IPs / CIDR ranges (IPv4 and IPv6) allowed to reach the **dashboard and API**. Empty or `*` allows everyone. Never affects the rest of your application. |

Invalid settings make the application **fail at startup** with a single message listing every problem (missing `ConnectionString` or `DatabaseName`, authentication without credentials, an unknown `TimeZoneId`, an out-of-range `MaxLogAgeHours`, ...), instead of running with a broken or insecure configuration.

## Securing the dashboard

Hubble stores request bodies, headers, SQL and log messages: treat the dashboard as an administrative tool that exposes sensitive data.

### What Hubble enforces

- **Authentication** (`RequireAuthentication = true`): an HTML login form for browsers and HTTP Basic authentication for API clients. Credentials are compared in constant time.
- **Signed session cookie**: the `HubbleAuth` cookie is encrypted and signed with ASP.NET Core Data Protection, expires after 8 hours, is `HttpOnly`, `SameSite=Strict`, `Secure` over HTTPS and scoped to the dashboard path. Changing the username or password invalidates existing sessions.
- **Brute-force lockout**: 5 failed attempts from the same IP within 15 minutes lock that IP out for 15 minutes (`429`).
- **CSRF protection**: every state-changing UI action is `POST`-only and requires an anti-forgery token; state-changing API calls require `Content-Type: application/json`.
- **Output encoding**: all captured data is HTML-encoded before rendering, so payloads such as `<script>` in a request body are displayed as text.
- **Response headers**: `Content-Security-Policy`, `X-Frame-Options: DENY`, `X-Content-Type-Options: nosniff`, `Referrer-Policy: no-referrer` and `Cache-Control: no-store` on every dashboard response.
- **IP allow-list**: `Security.AllowedIps` restricts who can reach the dashboard and API.

### What you must configure

1. **Enable authentication** outside local development and load the password from a secret store.
2. **Serve the app over HTTPS**, otherwise credentials and the session cookie travel in clear text.
3. **Restrict by network** with `Security.AllowedIps` (VPN / office ranges), and call `UseForwardedHeaders()` before `UseHubble()` when behind a proxy.
4. **Persist Data Protection keys when running more than one instance** (or when instances are recycled), so a session cookie issued by one instance is valid on the others:

   ```csharp
   builder.Services.AddDataProtection()
       .SetApplicationName("Orders.Api")
       .PersistKeysToFileSystem(new DirectoryInfo("/var/keys/orders-api")); // or Redis, Azure Blob, etc.
   ```

5. **Do not expose Hubble routes through a permissive CORS policy** (for example `SetIsOriginAllowed(_ => true).AllowCredentials()`), which would defeat the API's CSRF protection.
6. Consider `AllowDeleteAll = false` in shared environments.

## Capturing ILogger messages

```csharp
builder.Logging.AddHubbleLogging(); // uses options.MinimumLogLevel (Information by default)
```

Messages logged while a request is being processed are linked to it and appear in the **Loggers** section of the request detail page; messages logged outside a request are stored as standalone entries.

Keep the volume under control with the standard logging filters, scoped to Hubble's provider:

```csharp
using Gabonet.Hubble.Logging;

builder.Logging.AddHubbleLogging(LogLevel.Information);
builder.Logging.AddFilter<HubbleLoggerProvider>("Microsoft", LogLevel.Warning);
builder.Logging.AddFilter<HubbleLoggerProvider>("System", LogLevel.Warning);
```

`AddHubbleLogging()` depends on the services registered by `AddHubble()`. If you enable Hubble conditionally, register both (and `UseHubble()`) under the same condition.

## Capturing database queries

### Entity Framework Core

```csharp
builder.Services.AddDbContext<AppDbContext>((serviceProvider, options) =>
{
    var httpContextAccessor = serviceProvider.GetRequiredService<IHttpContextAccessor>();
    options
        .UseSqlServer(builder.Configuration.GetConnectionString("Default"))
        .AddHubbleInterceptor(httpContextAccessor, "AppDatabase");
});
```

### ADO.NET

Wrap the connection so every command is captured:

```csharp
using var connection = dbContext.GetTrackedConnection(httpContextAccessor, "AppDatabase");
await connection.OpenAsync();

using var command = connection.CreateCommand();
command.CommandText = "[dbo].[GetOrderById]";
command.CommandType = CommandType.StoredProcedure;
command.Parameters.Add(new SqlParameter("@Id", id));

using var reader = await command.ExecuteReaderAsync();
```

Or capture a single command explicitly before executing it:

```csharp
command.CaptureAdoNetCommand(httpContextAccessor, "AppDatabase");
```

### MongoDB

Subscribe to the driver's command events in your own `MongoClient`:

```csharp
var settings = MongoClientSettings.FromConnectionString(connectionString);
settings.ClusterConfigurator = cb =>
{
    cb.Subscribe<CommandStartedEvent>(e =>
    {
        var httpContext = httpContextAccessor.HttpContext;
        if (httpContext == null) return;

        httpContext.AddDatabaseQuery(new DatabaseQueryLog(
            databaseType: "MongoDB",
            databaseName: databaseName,
            query: e.Command.ToJson(),
            parameters: null,
            callerMethod: MongoDbExtensions.GetCallerMethod(),
            tableName: e.Command.GetCollectionName(),
            operationType: e.CommandName));
    });
};
var client = new MongoClient(settings);
```

> SQL parameters and MongoDB commands are stored **as executed, without masking**. Avoid capturing queries that carry secrets, or exclude those endpoints with `IgnorePaths`.

## Masking sensitive data

Masking applies to JSON request and response bodies and to request headers:

- Property matching is **case-insensitive** and **recursive** (nested objects and arrays).
- `MaskBodyProperties` applies to both directions; `MaskRequestBodyProperties` and `MaskResponseBodyProperties` add direction-specific properties.
- Masked values are replaced with `*****` before anything is written to MongoDB.

Bodies that are not JSON (form posts, plain text, query strings) are **not** masked; see [Known limitations](#known-limitations).

### Masking your own logs

`HubbleMaskingHelper` reuses the same configuration:

```csharp
using Gabonet.Hubble.Utilities;

_logger.LogInformation("Creating user: {User}", HubbleMaskingHelper.SerializeMasked(userDto));
// Creating user: {"Name":"John Doe","Email":"john@example.com","Password":"*****"}

// Extra properties for a single call, optional indentation
var json = HubbleMaskingHelper.SerializeMasked(order, new List<string> { "iban" }, indent: true);

// Already-serialized JSON
var masked = HubbleMaskingHelper.MaskJson(rawJson);
```

## How capture works (and what happens if MongoDB is down)

Hubble never makes your application wait for MongoDB:

1. When a request arrives, Hubble creates the log entry **in memory** (its id is generated locally), so `ILogger` messages can be linked to it immediately.
2. When the request finishes, the entry is placed in a bounded in-memory queue (up to 5,000 entries). `ILogger` messages go to the same queue.
3. A background service drains the queue and inserts the entries in batches of up to 200.

If MongoDB is slow or unavailable:

- Your endpoints and application startup are unaffected.
- The first failed write prints a `[Hubble]` warning to the console (later failures only with `EnableDiagnostics`), and a recovery message is printed when MongoDB comes back.
- While MongoDB is down, new entries are **dropped** once the queue is full, instead of growing memory without limit.
- On shutdown Hubble tries to flush pending entries for up to 5 seconds (skipped if MongoDB is already known to be down).
- The dashboard itself needs MongoDB; it reports an error after the server selection timeout (5 seconds by default).

Entries appear in the dashboard a few milliseconds after the request completes.

## Data retention and MongoDB setup

Give Hubble its **own database** and a dedicated MongoDB user with `readWrite` on that database only. Hubble creates its own `MongoClient` from `ConnectionString` and does not register it in dependency injection, so it is fully independent from any `IMongoClient` your application uses. Hubble uses three collections: `HubbleLogs`, `HubbleStats` and `HubbleConfig`.

### Indexes and retention are managed for you

At startup a background service prepares the `HubbleLogs` collection, without delaying your application:

| Index | Keys | Purpose |
|---|---|---|
| `hubble_timestamp` | `{ timestamp: 1 }` | Sorts the dashboard list. With `EnableDataPrune = true` it is a **TTL index**: MongoDB deletes logs older than `MaxLogAgeHours`. |
| `hubble_related_request` | `{ relatedRequestId: 1 }` (partial) | Loads the `ILogger` entries shown in a request's detail page. |

- Retention is enforced by MongoDB itself (its TTL monitor runs about once a minute), so there is no prune job in your application and it keeps working even if the app is stopped.
- Changing `MaxLogAgeHours` or toggling `EnableDataPrune` updates the index on the next startup. The index is dropped and recreated, which only needs the `readWrite` role.
- Indexes you created by hand are respected: an existing index on the same keys is reused. The only exception is a TTL on `{ timestamp: 1 }` that conflicts with `EnableDataPrune = true`, which is replaced by Hubble's.
- If MongoDB is unreachable, the service retries every 30 seconds. The result is shown under **Índices de MongoDB** on the dashboard's `/config` page.
- The dashboard's manual prune (`POST /api/prune`) is still available and uses the configured `MaxLogAgeHours`.

## Healthy implementation checklist

- [ ] `UseHubble()` is registered early, after `UseForwardedHeaders()` and before `UseRouting()`.
- [ ] Health checks, metrics, Swagger, file downloads, SSE/streaming and other high-volume endpoints are in `IgnorePaths`.
- [ ] `RequireAuthentication` is on outside local development, with the password loaded from a secret store.
- [ ] The app is served over HTTPS.
- [ ] `Security.AllowedIps` limits the dashboard to trusted networks.
- [ ] Data Protection keys are persisted and shared when running multiple instances.
- [ ] Masking lists cover the sensitive fields of **your** domain (the defaults are only a starting point).
- [ ] Retention is enabled (`EnableDataPrune` + `MaxLogAgeHours`) and `/config` reports the MongoDB indexes as created.
- [ ] Hubble uses its own MongoDB database and least-privilege credentials.
- [ ] `ILogger` capture is filtered so framework noise does not flood the dashboard.

## Web UI and REST API

| Route (relative to `BasePath`) | Method | Description |
|---|---|---|
| `/` | GET | Log list with filters and pagination |
| `/detail/{id}` | GET | Request detail, related `ILogger` messages and queries |
| `/config` | GET | Statistics and effective configuration |
| `/delete-all` | POST | Delete every log (anti-forgery token required; honours `AllowDeleteAll`) |
| `/login`, `/logout` | POST / GET | Session management when authentication is enabled |
| `/api/...` | GET / POST / DELETE | JSON API |

The JSON API (logs, details, configuration, prune, statistics) is documented in [docs/API_ENDPOINTS.md](docs/API_ENDPOINTS.md). API clients authenticate with HTTP Basic:

```bash
curl -u admin:password "https://your-app/hubble/api/logs?pageSize=20"
curl -u admin:password -X POST -H "Content-Type: application/json" "https://your-app/hubble/api/prune"
```

`pageSize` is capped at 200.

## Building from source

### Prerequisites

- .NET SDK 8.0 or later. Running the tests for every target also needs the .NET 6 and .NET 8 runtimes.
- A MongoDB instance for manual testing (for example `docker run -d -p 27017:27017 mongo:7`).

### Build and pack

```bash
git clone https://github.com/GaboIV/Gabonet.Hubble.git
cd Gabonet.Hubble
dotnet build src/src.csproj -c Release
```

The project has `GeneratePackageOnBuild` enabled, so a Release build also produces the `.nupkg` under `src/bin/Release`. To produce it in a known folder:

```bash
dotnet pack src/src.csproj -c Release -o ./artifacts
```

### Running the tests

```bash
dotnet test
```

The suite runs without any external service. Four integration tests (indexes, TTL and the full capture pipeline) need a real MongoDB and are reported as **skipped** unless the `HUBBLE_TEST_MONGODB` environment variable points to one. Each of them uses its own temporary database and drops it afterwards.

```bash
docker run -d --name hubble-test-mongo -p 27017:27017 mongo:7
HUBBLE_TEST_MONGODB=mongodb://localhost:27017 dotnet test
```

### Testing a local build in another application

```bash
dotnet add package Gabonet.Hubble --source /path/to/Gabonet.Hubble/artifacts
```

Or reference the project directly while developing:

```xml
<ProjectReference Include="..\Gabonet.Hubble\src\src.csproj" />
```

### Repository layout

| Path | Content |
|---|---|
| `src/Middleware` | Capture middleware and `HubbleOptions` |
| `src/BackgroundServices` | Background log writer and storage initializer (indexes, TTL retention) |
| `src/UI` | Dashboard middleware, HTML rendering and API models |
| `src/Security` | IP allow-list, login throttling, session tokens, HTML encoding |
| `src/Services` | Log queue, MongoDB-backed log and statistics services |
| `src/Logging` | `ILogger` provider |
| `src/Extensions` | Service registration, EF Core / ADO.NET capture, MongoDB helpers |
| `src/Utilities` | `HubbleMaskingHelper` |
| `docs/` | API reference and masking guide |
| `examples/` | Sample `Program.cs`, `appsettings.json` and authentication guide |
| `tests/Gabonet.Hubble.Tests` | xUnit test suite (security, capture, resilience, options, MongoDB integration) |

## Troubleshooting

| Symptom | Cause and fix |
|---|---|
| Dashboard returns `403 Access denied: IP not allowed` | Your IP is not in `Security.AllowedIps`. Behind a proxy, configure `UseForwardedHeaders()` before `UseHubble()`. |
| Login returns `429` | Too many failed attempts from your IP. Wait 15 minutes. |
| "Invalid or missing anti-forgery token" | The page was open too long or cookies are blocked. Reload the page and retry. |
| Logged out after a deploy or on another instance | Data Protection keys are not persisted/shared. See [Securing the dashboard](#securing-the-dashboard). |
| App fails at startup with "RequireAuthentication está activado…" | Authentication is enabled with an empty username or password. Provide both. |
| API returns `415` | `POST` requests must send `Content-Type: application/json`. |
| No `ILogger` entries under a request | Check `AddHubbleLogging()` is registered, the level/filters allow the message, and that it was logged during the request. |
| Dashboard pages are slow | Check on `/config` that the MongoDB indexes are created, and enable retention so the collection does not grow forever. |
| `[Hubble] No se pudieron preparar los índices de MongoDB` in the console | MongoDB was unreachable at startup or the user lacks `readWrite`. Hubble retries every 30 seconds; the app is not affected. |
| `[Hubble] No se pudieron guardar N logs en MongoDB` in the console | MongoDB is unreachable from the app. Your application keeps working; logs produced meanwhile are dropped. Check the connection string and network. |

## Known limitations

These are tracked for upcoming releases. Plan around them today:

- **Response buffering**: every captured response is buffered in memory. Add streaming endpoints, server-sent events and large downloads to `IgnorePaths`.
- **ILogger provider cost**: each captured message walks the stack trace to find its source. Use filters to limit volume.
- **Dashboard configuration page is read-only**: the effective configuration comes from code / `appsettings.json`.
- **Masking scope**: only JSON bodies and request headers are masked; query strings, form posts, SQL parameters and `ILogger` messages are stored as-is.

## Upgrade notes

This release hardens the dashboard and contains **breaking changes**:

- `Security.AllowedIps` now applies **only** to the dashboard and API. Previously it was evaluated for every request and could block the whole application.
- When configuring from `appsettings.json`, `Security.AllowedIps` no longer defaults to `127.0.0.1`; an empty list allows any IP.
- `Security.MaskRequestBodyProperties` and `Security.MaskResponseBodyProperties` are now read from `appsettings.json`.
- `GET /api/prune` and `GET /api/recalculate-stats` were replaced by `POST` (with `Content-Type: application/json`). Every `POST` to the API now requires that content type.
- `/delete-all`, `/run-prune`, `/recalculate-stats` and the `/save-*` routes only accept `POST` with an anti-forgery token.
- Session cookies issued by previous versions are no longer accepted; users must log in again.
- The application fails at startup if `RequireAuthentication` is enabled with an empty username or password, or if `BasePath` is empty or `/`.
- The package now uses the shared ASP.NET Core framework (`Microsoft.AspNetCore.App`) instead of the deprecated `Microsoft.AspNetCore.*` 2.2 packages, and registers Data Protection and Antiforgery services.
- `HubbleController`'s constructor now also requires `IAntiforgery` and `IHttpContextAccessor` (both registered by `AddHubble()`).
- The logout button is only shown when authentication is enabled.
- Captured logs are written asynchronously in batches by a hosted service instead of inline in each request. Requests no longer wait for MongoDB, and logs are dropped (not queued forever) while MongoDB is unavailable.
- `HubbleMiddleware` now takes `HubbleOptions` and `HubbleLogQueue`; `HubbleLoggerProvider` now takes `HubbleLogQueue`, `IHttpContextAccessor` and `HubbleOptions` (the constructors based on `IHubbleService` were removed). Both are created for you by `UseHubble()` / `AddHubbleLogging()`.
- Retention is now enforced by a MongoDB TTL index that Hubble creates and maintains. `HubbleDataPruneManager` and `DataPruneService` were removed: nothing deletes logs from inside your application anymore, and `DataPruneIntervalHours` is ignored.
- Hubble now creates its indexes at startup (`hubble_timestamp`, `hubble_related_request`). Indexes created by hand from the previous README are reused, except a `{ timestamp: 1 }` TTL with a different age when `EnableDataPrune` is on, which is replaced.
- The application fails at startup if `EnableDataPrune` is on and `MaxLogAgeHours` is outside 1–596,523.
- The `/config` page shows the effective retention settings and the index status instead of the values stored in MongoDB on first run, and the manual prune uses the configured `MaxLogAgeHours`.
- `HubbleStatsService` no longer queries MongoDB in its constructor, and neither the prune service nor the log writer delays application startup.
- Hubble's MongoDB client defaults to a 5-second server selection timeout unless the connection string sets `serverSelectionTimeoutMS`.
- `AddHubble()` no longer registers an `IMongoClient` in the service collection. Hubble keeps its own private client, so it can no longer replace the client your application registers for its own data (previously, whichever registration came last won). If your code resolved `IMongoClient` from DI only because Hubble registered it, register your own client explicitly.
- `CaptureHttpRequests = false` now fully disables HTTP capture, and request logs now include the client IP address.
- Log categories starting with `Gabonet.Hubble` are no longer captured by the `ILogger` provider.
- **Single options class.** `HubbleOptions` (namespace `Gabonet.Hubble.Middleware`) is now the only configuration type and includes `ConnectionString`, `DatabaseName` and `MinimumLogLevel`. `HubbleConfiguration`, `HubbleAuthConfiguration` and the duplicate `Gabonet.Hubble.Models.SecurityConfiguration` were removed. Code using `AddHubble(options => { ... })` keeps compiling unchanged.
- New `AddHubble(IConfigurationSection, Action<HubbleOptions>?)` overload based on the standard configuration binder; every option can now be set from `appsettings.json` and from code (previously some options were only available in one of them).
- `AddHubble(connectionString, databaseName, ...)` and `AddHubble(IConfiguration, connectionString, databaseName, sectionName)` still work but are marked `[Obsolete]`. The former now also registers the statistics service, so the `/config` page works with it.
- All registrations validate the options and fail at startup with every error listed. A `TimeZoneId` that does not exist on the machine is now an error instead of silently falling back to UTC.
- `CaptureLoggerMessages` (default now `true`) and `MinimumLogLevel` are honoured. `AddHubbleLogging()` without arguments uses `MinimumLogLevel`; passing a level still takes precedence.
- The `/config` page shows the effective configuration (service, database, time zone, capture settings, ignored paths) instead of values stored in MongoDB on first run or read from undocumented `HUBBLE_*` environment variables.

## License

Released under the MIT License.

## Contributing

Issues and pull requests are welcome.
