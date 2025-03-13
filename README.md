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

### 3. Accessing the User Interface

Once configured, you can access the Hubble user interface by navigating to:

```
https://your-application.com/hubble
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