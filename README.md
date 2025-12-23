# Hubble para .NET

Hubble es una biblioteca para monitoreo y logging de aplicaciones .NET que permite capturar y visualizar solicitudes HTTP, logs de ILogger y consultas a bases de datos en una interfaz web integrada.

## Configuración

### 1. Instalación

Añade Hubble a tu proyecto:

```bash
dotnet add package Gabonet.Hubble
```

### 2. Configuración en Program.cs

```csharp
// Program.cs o Startup.cs
using Gabonet.Hubble.Extensions;
using Microsoft.Extensions.Logging;

// ...

// Agregar Hubble con la configuración necesaria
builder.Services.AddHubble(options =>
{
    options.ConnectionString = "mongodb://localhost:27017"; // Requerido: Conexión a MongoDB
    options.DatabaseName = "HubbleDB";                      // Requerido: Nombre de la base de datos
    options.ServiceName = "MiAplicación";                   // Opcional: Nombre del servicio
    options.TimeZoneId = "Romance Standard Time";           // Opcional: Zona horaria para mostrar logs
    options.EnableDiagnostics = true;                       // Opcional: Mostrar mensajes de diagnóstico
    options.CaptureLoggerMessages = true;                   // Opcional: Capturar logs de ILogger (true por defecto)
    options.BasePath = "/logs";                             // Opcional: Ruta personalizada para acceder a Hubble (por defecto: /hubble)
});

// Añadir el proveedor de logs de Hubble para capturar los logs de ILogger
builder.Logging.AddHubbleLogging(LogLevel.Information);  // Puedes cambiar el nivel mínimo de logs

// ...

// Agregar el middleware de Hubble (debe ir antes de app.UseRouting())
app.UseHubble();
```

### Ignorar rutas específicas

Hubble permite configurar rutas específicas que serán ignoradas por el middleware, lo que es útil para endpoints como health checks, métricas o cualquier otra ruta que no desees monitorear.

```csharp
services.AddHubble(options =>
{
    options.ConnectionString = "mongodb://localhost:27017";
    options.DatabaseName = "hubble";
    options.ServiceName = "MiServicio";

    // Configurar rutas a ignorar
    options.IgnorePaths = new List<string>
    {
        "/health",
        "/metrics",
        "/test",
        "/swagger"
    };
});
```

Todas las rutas que comiencen con cualquiera de los prefijos especificados en `IgnorePaths` serán ignoradas por el middleware de Hubble. Por ejemplo, si especificas `/health`, se ignorarán rutas como `/health`, `/health/status`, `/health/check`, etc.

### Ejemplo completo de configuración

```csharp
services.AddHubble(options =>
{
    // Configuración obligatoria
    options.ConnectionString = "mongodb://localhost:27017";
    options.DatabaseName = "hubble";
    
    // Configuración general
    options.ServiceName = "MiServicio";
    options.BasePath = "/hubble";
    options.TimeZoneId = "America/Argentina/Buenos_Aires";
    options.EnableDiagnostics = false;
    
    // Captura de datos
    options.CaptureLoggerMessages = true;
    options.CaptureHttpRequests = true;
    options.IgnoreStaticFiles = true;
    
    // Rutas a ignorar
    options.IgnorePaths = new List<string>
# Hubble para .NET

Hubble es una biblioteca para monitoreo y logging de aplicaciones .NET que permite capturar y visualizar solicitudes HTTP, logs de ILogger y consultas a bases de datos en una interfaz web integrada.

## Configuración

### 1. Instalación

Añade Hubble a tu proyecto:

```bash
dotnet add package Gabonet.Hubble
```

### 2. Configuración en Program.cs

```csharp
// Program.cs o Startup.cs
using Gabonet.Hubble.Extensions;
using Microsoft.Extensions.Logging;

// ...

// Agregar Hubble con la configuración necesaria
builder.Services.AddHubble(options =>
{
    options.ConnectionString = "mongodb://localhost:27017"; // Requerido: Conexión a MongoDB
    options.DatabaseName = "HubbleDB";                      // Requerido: Nombre de la base de datos
    options.ServiceName = "MiAplicación";                   // Opcional: Nombre del servicio
    options.TimeZoneId = "Romance Standard Time";           // Opcional: Zona horaria para mostrar logs
    options.EnableDiagnostics = true;                       // Opcional: Mostrar mensajes de diagnóstico
    options.CaptureLoggerMessages = true;                   // Opcional: Capturar logs de ILogger (true por defecto)
    options.BasePath = "/logs";                             // Opcional: Ruta personalizada para acceder a Hubble (por defecto: /hubble)
});

// Añadir el proveedor de logs de Hubble para capturar los logs de ILogger
builder.Logging.AddHubbleLogging(LogLevel.Information);  // Puedes cambiar el nivel mínimo de logs

// ...

// Agregar el middleware de Hubble (debe ir antes de app.UseRouting())
app.UseHubble();
```

### Ignorar rutas específicas

Hubble permite configurar rutas específicas que serán ignoradas por el middleware, lo que es útil para endpoints como health checks, métricas o cualquier otra ruta que no desees monitorear.

```csharp
services.AddHubble(options =>
{
    options.ConnectionString = "mongodb://localhost:27017";
    options.DatabaseName = "hubble";
    options.ServiceName = "MiServicio";

    // Configurar rutas a ignorar
    options.IgnorePaths = new List<string>
    {
        "/health",
        "/metrics",
        "/test",
        "/swagger"
    };
});
```

Todas las rutas que comiencen con cualquiera de los prefijos especificados en `IgnorePaths` serán ignoradas por el middleware de Hubble. Por ejemplo, si especificas `/health`, se ignorarán rutas como `/health`, `/health/status`, `/health/check`, etc.

### Ejemplo completo de configuración

```csharp
services.AddHubble(options =>
{
    // Configuración obligatoria
    options.ConnectionString = "mongodb://localhost:27017";
    options.DatabaseName = "hubble";
    
    // Configuración general
    options.ServiceName = "MiServicio";
    options.BasePath = "/hubble";
    options.TimeZoneId = "America/Argentina/Buenos_Aires";
    options.EnableDiagnostics = false;
    
    // Captura de datos
    options.CaptureLoggerMessages = true;
    options.CaptureHttpRequests = true;
    options.IgnoreStaticFiles = true;
    
    // Rutas a ignorar
    options.IgnorePaths = new List<string>
    {
        "/health",
        "/metrics",
        "/swagger"
    };
    
    // Configuración de Seguridad
    options.Security = new SecurityConfiguration
    {
        // Enmascaramiento de datos (Case-Insensitive)
        // Se aplica tanto a Request como a Response
        MaskBodyProperties = new List<string> { "password", "token", "tarjeta", "cvv" },
        
        // Propiedades adicionales para enmascarar SOLO en el Request
        MaskRequestBodyProperties = new List<string> { "pin", "clave" },

        // Propiedades adicionales para enmascarar SOLO en el Response
        MaskResponseBodyProperties = new List<string> { "internalId", "secretData" },
        
        // Headers a enmascarar
        MaskHeaders = new List<string> { "Authorization", "X-Api-Key", "Cookie" },
        
        // Filtrado de IPs (CIDR soportado)
        // Dejar lista vacía o incluir "*" para permitir todas las IPs
        AllowedIps = new List<string> { "127.0.0.1", "192.168.1.0/24" }
    };

    // Limpieza automática de datos
    options.EnableDataPrune = true;
    options.DataPruneIntervalHours = 24; // Ejecutar cada 24 horas
    options.MaxLogAgeHours = 72;         // Mantener logs por 3 días

    // UI
    options.HighlightNewServices = true;
    options.HighlightDurationSeconds = 10;
    
    // Autenticación para el dashboard
    options.RequireAuthentication = true;
    options.Username = "admin";
    options.Password = "securePassword123";
});

// Agregar el middleware a la pipeline
app.UseHubble();
```

### Características de Seguridad

#### Enmascaramiento de Datos (Masking)
Hubble permite proteger información sensible en los logs mediante enmascaramiento:
- **Case-Insensitive**: El enmascaramiento no distingue entre mayúsculas y minúsculas (ej. "password", "Password", "PASSWORD" serán enmascarados).
- **Body Request/Response**: `MaskBodyProperties` aplica a ambos.
- **Request Específico**: `MaskRequestBodyProperties` permite definir campos que solo deben ocultarse en la solicitud.
- **Response Específico**: `MaskResponseBodyProperties` permite definir campos que solo deben ocultarse en la respuesta.
- **Headers**: `MaskHeaders` protege cabeceras sensibles como tokens de autorización.

#### Control de Acceso por IP
Puedes restringir el acceso al dashboard de Hubble:
- **Lista Vacía**: Si `AllowedIps` está vacía, se permite el acceso a **todas** las IPs.
- **Comodín**: Si la lista contiene `*`, se permite el acceso a **todas** las IPs.
- **CIDR**: Soporta notación CIDR para rangos de IP (ej. `192.168.1.0/24`).
- **IPs Individuales**: Soporta IPs específicas (ej. `127.0.0.1`).

## Uso

### Visualización de logs

Para ver los logs, accede a la interfaz web integrada. Por defecto, la ruta es:

```
https://tu-aplicacion/hubble
```

Si has configurado una ruta personalizada con `options.BasePath`, deberás usar esa ruta en su lugar:

```
https://tu-aplicacion/logs  // Si configuraste options.BasePath = "/logs"
```

### Captura de logs de ILogger y asociación con solicitudes HTTP

Hubble ahora puede capturar los logs generados con `ILogger` y asociarlos automáticamente a la solicitud HTTP que los generó. Esto facilita enormemente la depuración de problemas.

#### 1. Usando ILogger en tus controladores y servicios

En tus controladores y servicios, simplemente usa ILogger como siempre:

```csharp
public class MiControlador : Controller
{
    private readonly ILogger<MiControlador> _logger;

    public MiControlador(ILogger<MiControlador> logger)
    {
        _logger = logger;
    }

    public IActionResult Index()
    {
        _logger.LogInformation("Solicitud recibida para la página principal");
        // Tu código aquí
        _logger.LogDebug("Operación completada");
        return View();
    }
}
```

#### 2. Visualización de logs relacionados

Cuando accedas a la página de detalles de una solicitud HTTP en `/hubble/detail/{id}`, verás una sección llamada "Logger" que muestra todos los logs de ILogger relacionados con esa solicitud, agrupados por categoría (namespace del logger).

### Enmascaramiento de datos en logs personalizados

Hubble proporciona una utilidad para enmascarar datos sensibles en tus propios logs, utilizando la misma configuración de enmascaramiento que se aplica a las solicitudes y respuestas HTTP.

#### Uso del Helper de Enmascaramiento

```csharp
using Gabonet.Hubble.Utilities;

public class PlayerService
{
    private readonly ILogger<PlayerService> _logger;
    private readonly IPlayerRepository _playerRepository;

    public PlayerService(
        ILogger<PlayerService> logger,
        IPlayerRepository playerRepository)
    {
        _logger = logger;
        _playerRepository = playerRepository;
    }

    public async Task<PlayerDto> CreatePlayerAsync(PlayerDto playerDto)
    {
        var player = MapToEntity(playerDto);
        player.Id = Guid.NewGuid();
        
        // Use HubbleMaskingHelper to serialize and mask sensitive data
        _logger.LogInformation("Creating player: {Player}", 
            HubbleMaskingHelper.SerializeMasked(playerDto));
        
        var createdPlayer = await _playerRepository.AddAsync(player);
        
        // Also works with entity objects
        _logger.LogInformation("Player created: {Player}", 
            HubbleMaskingHelper.SerializeMasked(createdPlayer));
        
        return MapToDto(createdPlayer);
    }
}
```

#### Métodos disponibles

**`SerializeMasked<T>(T obj, List<string>? additionalMaskProperties = null, bool indent = false)`**

Serializa un objeto a JSON y enmascara las propiedades sensibles según la configuración de Hubble.

```csharp
// Basic usage
var maskedJson = HubbleMaskingHelper.SerializeMasked(myObject);

// With additional properties to mask
var maskedJson = HubbleMaskingHelper.SerializeMasked(
    myObject, 
    new List<string> { "customField", "secretData" }
);

// With indented JSON for better readability
var maskedJson = HubbleMaskingHelper.SerializeMasked(myObject, indent: true);
```

**`MaskJson(string jsonString, List<string>? additionalMaskProperties = null)`**

Enmascara propiedades sensibles en un string JSON ya serializado.

```csharp
var jsonString = JsonSerializer.Serialize(myObject);
var maskedJson = HubbleMaskingHelper.MaskJson(jsonString);

// With additional mask properties
var maskedJson = HubbleMaskingHelper.MaskJson(
    jsonString, 
    new List<string> { "customField" }
);
```

#### Características

- **Automático**: Utiliza la configuración de `MaskBodyProperties`, `MaskRequestBodyProperties` y `MaskResponseBodyProperties` de Hubble.
- **Case-Insensitive**: No distingue entre mayúsculas y minúsculas.
- **Recursivo**: Enmascara propiedades en objetos anidados y arrays.
- **Propiedades adicionales**: Permite agregar propiedades adicionales a enmascarar en tiempo de ejecución.
- **Seguro**: Si Hubble no está inicializado o hay errores, devuelve el JSON sin enmascarar o un mensaje de error.

#### Ejemplo con datos sensibles

```csharp
public class UserDto
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public string Email { get; set; }
    public string Password { get; set; }  // Sensitive
    public string Token { get; set; }     // Sensitive
    public string CreditCard { get; set; } // Sensitive
}

var user = new UserDto 
{
    Id = Guid.NewGuid(),
    Name = "John Doe",
    Email = "john@example.com",
    Password = "SecretPass123",
    Token = "abc123xyz",
    CreditCard = "4111111111111111"
};

// Log with masking
_logger.LogInformation("User data: {User}", 
    HubbleMaskingHelper.SerializeMasked(user));

// Output: User data: {"Id":"...","Name":"John Doe","Email":"john@example.com","Password":"*****","Token":"*****","CreditCard":"*****"}
```

### Implementación técnica

La asociación entre logs de ILogger y solicitudes HTTP funciona de la siguiente manera:

1. Cuando llega una solicitud HTTP, el middleware `HubbleMiddleware` crea un registro de log y lo guarda en el contexto HTTP (`HttpContext.Items["Hubble_RequestLog"]`).

2. Cuando se genera un log con ILogger, el proveedor `HubbleLoggerProvider` comprueba si existe una solicitud HTTP activa con un log asociado y, si es así, registra el log con una referencia al ID de la solicitud.

3. Cuando se consulta el detalle de una solicitud en la interfaz web, se buscan y muestran todos los logs relacionados con esa solicitud.

## Solución de problemas

Si no ves los logs relacionados en la interfaz:

1. Asegúrate de que has configurado correctamente Hubble con `AddHubble()` y `UseHubble()`.

2. Verifica que has añadido el proveedor de logs con `builder.Logging.AddHubbleLogging()`.

3. Comprueba que estás usando `ILogger<T>` en tus clases para generar logs.

4. Asegúrate de que los logs se generan durante el procesamiento de solicitudes HTTP, no antes o después.

5. Revisa la consola y los logs para ver si hay mensajes de diagnóstico que indiquen algún problema.

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

### 5. Capturing ILogger Messages

Hubble ahora puede capturar mensajes de ILogger y asociarlos automáticamente con las solicitudes HTTP a las que pertenecen, permitiendo una visión completa del flujo de ejecución.

#### Integración automática con el flujo HTTP

Una de las características más potentes de Hubble es la agrupación automática de mensajes de ILogger con sus solicitudes HTTP correspondientes:

1. **Logs en contexto**: Los logs emitidos durante el procesamiento de una solicitud HTTP se asocian automáticamente con esa solicitud
2. **Visualización integrada**: Al ver los detalles de una solicitud, también verás todos los logs generados durante su procesamiento
3. **Visibilidad completa**: Esto te permite ver todo el ciclo de vida de una solicitud, desde controladores hasta servicios y repositorios

#### Configuración (importante: orden de registro)

Asegúrate de seguir este orden para configurar correctamente:

```csharp
var builder = WebApplication.CreateBuilder(args);

// 1. Primero añade los servicios de Hubble
builder.Services.AddHubble(options =>
{
    options.ConnectionString = "mongodb://localhost:27017";
    options.DatabaseName = "HubbleDB";
    options.CaptureLoggerMessages = true; // Habilitar captura de ILogger
    options.MinimumLogLevel = LogLevel.Information; // Nivel mínimo a capturar
});

// 2. Luego configura el logger de Hubble
builder.Logging.AddHubbleLogging(LogLevel.Information);

var app = builder.Build();

// 3. Finalmente configura el middleware
app.UseHubble();
```

#### Ejemplo en un controlador

No necesitas hacer nada especial en tu código. Las llamadas normales a ILogger se capturarán automáticamente:

```csharp
[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    private readonly ILogger<UsersController> _logger;
    private readonly IUserService _userService;

    public UsersController(ILogger<UsersController> logger, IUserService userService)
    {
        _logger = logger;
        _userService = userService;
    }

    [HttpGet]
    public async Task<IActionResult> GetUsers()
    {
        _logger.LogInformation("Obteniendo listado de usuarios"); // Este log se asociará a la solicitud HTTP
        
        try 
        {
            var users = await _userService.GetAllAsync();
            _logger.LogInformation("Se encontraron {count} usuarios", users.Count); // También se asociará
            return Ok(users);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener usuarios"); // Los errores también se asocian
            return StatusCode(500, "Error interno del servidor");
        }
    }
}
```

#### Visualización en la interfaz

En la interfaz de Hubble, ahora tendrás:

1. **Vista principal**: Un nuevo filtro para seleccionar tipos de logs (HTTP o ILogger)
2. **Vista de detalle**: Una nueva sección "Logs de Aplicación Asociados" que muestra todos los logs de ILogger relacionados con esa solicitud HTTP
3. **Códigos de colores**: Los logs se muestran con un código de colores según su nivel (información, advertencia, error, etc.)

Con esta configuración, obtendrás automáticamente una traza completa de logs para cada solicitud HTTP, lo que facilita enormemente la depuración y el monitoreo de tu aplicación.

### 6. Accessing the User Interface

Once configured, you can access the Hubble user interface by navigating to:

```
https://your-application.com/hubble
```

### 7. Securing the Hubble UI with Authentication

For sensitive environments, you can secure the Hubble UI with basic username and password authentication:

```csharp
builder.Services.AddHubble(options =>
{
    options.ConnectionString = "mongodb://localhost:27017";
    options.DatabaseName = "HubbleDB";
    
    // Enable authentication
    options.RequireAuthentication = true;
    options.Username = "admin";
    options.Password = "your_secure_password";
});
```

When authentication is enabled:
- Users will be redirected to a login form when accessing Hubble
- Session is maintained using a secure HTTP-only cookie
- Sessions expire after 8 hours of inactivity
- A logout option is provided in the UI

For maximum security, consider:
- Using a strong, unique password
- Deploying your application with HTTPS enabled
- Using environment variables for the username and password instead of hardcoding them

```csharp
// Using environment variables for credentials
builder.Services.AddHubble(options =>
{
    // ... other options
    options.RequireAuthentication = true;
    options.Username = Environment.GetEnvironmentVariable("HUBBLE_USERNAME") ?? "admin";
    options.Password = Environment.GetEnvironmentVariable("HUBBLE_PASSWORD") ?? "default_password";
});
```

### Masking Sensitive Data in Custom Logs

Hubble provides a utility to mask sensitive data in your own logs, using the same masking configuration that applies to HTTP requests and responses.

#### Using the Masking Helper

```csharp
using Gabonet.Hubble.Utilities;

public class PlayerService
{
    private readonly ILogger<PlayerService> _logger;
    private readonly IPlayerRepository _playerRepository;

    public PlayerService(
        ILogger<PlayerService> logger,
        IPlayerRepository playerRepository)
    {
        _logger = logger;
        _playerRepository = playerRepository;
    }

    public async Task<PlayerDto> CreatePlayerAsync(PlayerDto playerDto)
    {
        var player = MapToEntity(playerDto);
        player.Id = Guid.NewGuid();
        
        // Use HubbleMaskingHelper to serialize and mask sensitive data
        _logger.LogInformation("Creating player: {Player}", 
            HubbleMaskingHelper.SerializeMasked(playerDto));
        
        var createdPlayer = await _playerRepository.AddAsync(player);
        
        // Also works with entity objects
        _logger.LogInformation("Player created: {Player}", 
            HubbleMaskingHelper.SerializeMasked(createdPlayer));
        
        return MapToDto(createdPlayer);
    }
}
```

#### Available Methods

**`SerializeMasked<T>(T obj, List<string>? additionalMaskProperties = null, bool indent = false)`**

Serializes an object to JSON and masks sensitive properties according to Hubble's configuration.

```csharp
// Basic usage
var maskedJson = HubbleMaskingHelper.SerializeMasked(myObject);

// With additional properties to mask
var maskedJson = HubbleMaskingHelper.SerializeMasked(
    myObject, 
    new List<string> { "customField", "secretData" }
);

// With indented JSON for better readability
var maskedJson = HubbleMaskingHelper.SerializeMasked(myObject, indent: true);
```

**`MaskJson(string jsonString, List<string>? additionalMaskProperties = null)`**

Masks sensitive properties in an already serialized JSON string.

```csharp
var jsonString = JsonSerializer.Serialize(myObject);
var maskedJson = HubbleMaskingHelper.MaskJson(jsonString);

// With additional mask properties
var maskedJson = HubbleMaskingHelper.MaskJson(
    jsonString, 
    new List<string> { "customField" }
);
```

#### Features

- **Automatic**: Uses Hubble's `MaskBodyProperties`, `MaskRequestBodyProperties`, and `MaskResponseBodyProperties` configuration.
- **Case-Insensitive**: Property matching is case-insensitive.
- **Recursive**: Masks properties in nested objects and arrays.
- **Additional Properties**: Allows adding extra properties to mask at runtime.
- **Safe**: If Hubble is not initialized or errors occur, returns unmasked JSON or an error message.

#### Example with Sensitive Data

```csharp
public class UserDto
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public string Email { get; set; }
    public string Password { get; set; }  // Sensitive
    public string Token { get; set; }     // Sensitive
    public string CreditCard { get; set; } // Sensitive
}

var user = new UserDto 
{
    Id = Guid.NewGuid(),
    Name = "John Doe",
    Email = "john@example.com",
    Password = "SecretPass123",
    Token = "abc123xyz",
    CreditCard = "4111111111111111"
};

// Log with masking
_logger.LogInformation("User data: {User}", 
    HubbleMaskingHelper.SerializeMasked(user));

// Output: User data: {"Id":"...","Name":"John Doe","Email":"john@example.com","Password":"*****","Token":"*****","CreditCard":"*****"}
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
    
    // General Settings
    options.ServiceName = "MyService";
    options.BasePath = "/monitoring";
    options.TimeZoneId = "Eastern Standard Time";
    options.EnableDiagnostics = false;
    
    // Data Capture Settings
    options.CaptureLoggerMessages = true;
    options.CaptureHttpRequests = true;
    options.IgnoreStaticFiles = true;
    
    // Paths to ignore
    options.IgnorePaths = new List<string> { "/health", "/metrics" };
    
    // Security Configuration
    options.Security = new SecurityConfiguration
    {
        // Data Masking (Case-Insensitive)
        // Applies to both Request and Response bodies
        MaskBodyProperties = new List<string> { "password", "token", "creditCard" },
        
        // Additional properties to mask ONLY in the Request body
        MaskRequestBodyProperties = new List<string> { "pin", "secretKey" },

        // Additional properties to mask ONLY in the Response body
        MaskResponseBodyProperties = new List<string> { "internalId", "secretData" },
        
        // Headers to mask
        MaskHeaders = new List<string> { "Authorization", "X-Api-Key", "Cookie" },
        
        // IP Filtering (CIDR supported)
        // Leave empty or use "*" to allow all IPs
        AllowedIps = new List<string> { "127.0.0.1", "10.0.0.0/8" }
    };
    
    // Automatic Data Pruning
    options.EnableDataPrune = true;
    options.DataPruneIntervalHours = 24; // Run every 24 hours
    options.MaxLogAgeHours = 72;         // Keep logs for 3 days
    
    // UI Settings
    options.HighlightNewServices = true;
    options.HighlightDurationSeconds = 10;
    
    // Authentication
    options.RequireAuthentication = true;
    options.Username = "admin";
    options.Password = "secure_password";
});
```

### Security Features

#### Data Masking
Hubble helps protect sensitive information in your logs:
- **Case-Insensitive**: Masking is case-insensitive (e.g., "password", "Password", "PASSWORD" will all be masked).
- **Request/Response Body**: `MaskBodyProperties` applies to both request and response bodies.
- **Request Specific**: `MaskRequestBodyProperties` allows you to define fields that should only be masked in the request.
- **Response Specific**: `MaskResponseBodyProperties` allows you to define fields that should only be masked in the response.
- **Headers**: `MaskHeaders` protects sensitive headers like authorization tokens.

#### IP Access Control
You can restrict access to the Hubble dashboard:
- **Empty List**: If `AllowedIps` is empty, access is granted to **all** IPs.
- **Wildcard**: If the list contains `*`, access is granted to **all** IPs.
- **CIDR**: Supports CIDR notation for IP ranges (e.g., `192.168.1.0/24`).
- **Specific IPs**: Supports individual IPs (e.g., `127.0.0.1`).

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## Contributing

Contributions are welcome. Please open an issue or pull request for suggestions or improvements.

---

Developed by Gabonet