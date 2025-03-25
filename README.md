# Gabonet.Hubble

Gabonet.Hubble is a NuGet package that provides monitoring and logging capabilities for .NET applications. It captures HTTP requests, responses, and database queries, enabling complete visibility of service-consuming processes in your application.

## Features

- 🔍 **HTTP Request Monitoring**: Automatically captures all HTTP requests and responses.
- 📊 **Database Query Logging**: Captures SQL queries and their parameters for different database providers.
- 🚀 **Modern User Interface**: View logs with a modern and user-friendly web interface.
- 🔄 **Filtering and Search**: Filter logs by HTTP method, URL, and more.
- ⚡ **Optimized Performance**: Designed to have minimal impact on your application's performance.
- 🛠️ **Easy Integration**: Seamlessly integrates into existing ASP.NET Core applications.
- 🕒 **Time Zone Support**: Display logs in your preferred time zone.

## Installation

Install the NuGet package using the NuGet Package Manager:

```
Install-Package Gabonet.Hubble
```

Or using the .NET CLI:

```
dotnet add package Gabonet.Hubble
```

## Basic Usage

### 1. Configuration in Program.cs

```csharp
using Gabonet.Hubble.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Add Hubble services
builder.Services.AddHubble(options =>
{
    options.ConnectionString = "mongodb://localhost:27017";
    options.DatabaseName = "HubbleDB";
    options.TimeZoneId = "America/New_York"; // Optional: Set your preferred time zone
});

var app = builder.Build();

// Configure Hubble middleware
app.UseHubble();

// Rest of the application configuration...
```

### 2. Capturing Database Queries with Entity Framework Core

Configure your DbContext to use the Hubble interceptor by adding the AddHubbleInterceptor extension method in the OnConfiguring method:

```csharp
using Gabonet.Hubble.Extensions;
using Microsoft.EntityFrameworkCore;

public class MyDbContext : DbContext
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    
    public MyDbContext(
        DbContextOptions<MyDbContext> options,
        IHttpContextAccessor httpContextAccessor) : base(options)
    {
        _httpContextAccessor = httpContextAccessor;
    }
    
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.AddHubbleInterceptor(_httpContextAccessor, "MyDatabase");
        base.OnConfiguring(optionsBuilder);
    }
    
    // DbSets definition...
}
```

Alternatively, you can configure the interceptor when registering your DbContext in the service collection:

```csharp
// In Program.cs or Startup.cs
services.AddDbContext<MyDbContext>((serviceProvider, optionsBuilder) =>
{
    var httpContextAccessor = serviceProvider.GetRequiredService<IHttpContextAccessor>();
    optionsBuilder
        .UseSqlServer(Configuration.GetConnectionString("DefaultConnection"))
        .AddHubbleInterceptor(httpContextAccessor, "MyDatabase");
});
```

### 3. Capturing Direct ADO.NET Queries

There are two ways to capture ADO.NET queries that don't go through Entity Framework (like stored procedures executed directly):

#### Option 1: Using GetTrackedConnection (recommended)

This method is the simplest and most automatic, as any command created from the connection will be captured automatically:

```csharp
using Gabonet.Hubble.Extensions;
using Microsoft.Data.SqlClient;
using System.Data;

public async Task<string> GetStoresProcedureById(string id)
{
    var response = new List<string>();

    // Get a wrapped connection that automatically captures all commands
    using (var connection = _sqlServerDbContext.GetTrackedConnection(_httpContextAccessor, "SQLServerDB"))
    {
        await connection.OpenAsync();
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "[dbo].[SP_DBO_GETVALUESBYID]";
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.Add(new SqlParameter("@ID", id));

            // No special action needed, commands are captured automatically

            using (var reader = await command.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    response.Add(reader["Response"].ToString());
                    break;
                }
            }
        }
    }

    return response.FirstOrDefault();
}
```

#### Option 2: Manual capture with CaptureAdoNetCommand

If you prefer more control or can't modify the existing code much, you can capture the command manually just before executing it:

```csharp
using Gabonet.Hubble.Extensions;
using Microsoft.Data.SqlClient;
using System.Data;

public async Task<string> GetStoresProcedureById(string id)
{
    var response = new List<string>();

    using (var connection = (SqlConnection)_sqlServerDbContext.Database.GetDbConnection())
    {
        await connection.OpenAsync();
        using (var command = new SqlCommand("[dbo].[SP_DBO_GETVALUESBYID]", connection))
        {
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.AddWithValue("@ID", id);
            
            // Capture the command before executing it
            command.CaptureAdoNetCommand(_httpContextAccessor, "SQLServerDB");

            using (var reader = await command.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    response.Add(reader["Response"].ToString());
                    break;
                }
            }
        }
    }

    return conditions.FirstOrDefault();
}
```

### 4. Capturing MongoDB Queries

For comprehensive MongoDB monitoring, you can implement a custom context that automatically tracks all queries:

```csharp
using MongoDB.Bson;
using MongoDB.Driver;
using Microsoft.AspNetCore.Http;
using MongoDB.Driver.Core.Events;
using Microsoft.Extensions.Logging;
using Gabonet.Hubble.Models;
using Gabonet.Hubble.Extensions;
using Microsoft.Extensions.Configuration;

public class MongoDbContext
{
    private readonly IMongoDatabase _database;
    private readonly ILogger<MongoDbContext> _logger;
    private readonly IHttpContextAccessor _httpContextAccessor;

    // Your collections
    public IMongoCollection<User> Users { get; private set; }
    public IMongoCollection<Account> Accounts { get; private set; }
    public IMongoCollection<Profile> Profiles { get; private set; }
    // Other collections...

    public MongoDbContext(
        IConfiguration configuration, 
        ILogger<MongoDbContext> logger,
        IHttpContextAccessor httpContextAccessor
    ) {
        _logger = logger;
        _httpContextAccessor = httpContextAccessor;
        var mongoConnectionString = Environment.GetEnvironmentVariable("MONGO_CONNECTION_STRING");
        var mongoDatabaseName = Environment.GetEnvironmentVariable("MONGO_DATABASE_NAME");

        try
        {
            var settings = MongoClientSettings.FromConnectionString(mongoConnectionString);

            // Configure MongoDB event subscription for Hubble monitoring
            settings.ClusterConfigurator = cb =>
            {
                cb.Subscribe<CommandStartedEvent>(e =>
                {
                    _logger.LogInformation("Mongo Query: {CommandName} - {Command}", e.CommandName, e.Command.ToJson());
                    
                    // Capture the query for Hubble
                    if (_httpContextAccessor.HttpContext != null)
                    {
                        var query = new DatabaseQueryLog(
                            databaseType: "MongoDB",
                            databaseName: mongoDatabaseName,
                            query: e.Command.ToJson(),
                            parameters: null,
                            callerMethod: MongoDbExtensions.GetCallerMethod(),
                            tableName: e.Command.GetCollectionName(),
                            operationType: e.CommandName
                        );
                        
                        _httpContextAccessor.HttpContext.AddDatabaseQuery(query);
                    }
                });
            };

            var client = new MongoClient(settings);
            _database = client.GetDatabase(mongoDatabaseName);

            // Initialize collections
            Users = _database.GetCollection<User>("User");
            Accounts = _database.GetCollection<Account>("Account");
            Profiles = _database.GetCollection<Profile>("Profile");
            // Initialize other collections...
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initializing MongoDB context.");
        }
    }

    public IMongoCollection<T> GetCollection<T>(string collectionName)
    {
        return _database.GetCollection<T>(collectionName);
    }
}
```

This approach provides detailed monitoring of all MongoDB operations, including:
- Query content and command type
- Name of the collection being accessed
- Caller method information
- Automatic integration with Hubble's HTTP request tracking

### 5. Accessing the User Interface

Once configured, you can access the Hubble user interface by navigating to:

```
https://your-application.com/hubble
```

Or, if you've customized the base path:

```
https://your-application.com/your-custom-path
```

## Configuration Options

You can customize Hubble's behavior with the following options:

```csharp
builder.Services.AddHubble(options =>
{
    // Required: MongoDB connection string
    options.ConnectionString = "mongodb://localhost:27017";
    
    // Required: Database name
    options.DatabaseName = "HubbleDB";
    
    // Optional: Time zone ID for displaying dates (default: UTC)
    // Use standard IANA time zone IDs like "America/New_York" or Windows time zone IDs
    options.TimeZoneId = "Eastern Standard Time";
    
    // Optional: Paths to ignore
    options.IgnorePaths.Add("/health");
    options.IgnorePaths.Add("/metrics");
    
    // Optional: Ignore static files (default: true)
    options.IgnoreStaticFiles = true;
    
    // Optional: Enable diagnostic messages (default: false)
    options.EnableDiagnostics = false;
    
    // Optional: Custom base path for Hubble UI (default: "/hubble")
    options.BasePath = "/logs";
});
```

## Requirements

- .NET 7.0 or higher
- MongoDB (for storing logs)

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## Contributing

Contributions are welcome. Please open an issue or pull request for suggestions or improvements.

---

Developed by Gabonet 