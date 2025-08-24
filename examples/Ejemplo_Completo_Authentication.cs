using Gabonet.Hubble.Extensions;
using Microsoft.EntityFrameworkCore;

// ===================================================================
// EJEMPLO COMPLETO: Configuración de Hubble con Autenticación
// ===================================================================

var builder = WebApplication.CreateBuilder(args);

// Agregar servicios
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// ===================================================================
// CONFIGURACIÓN DE HUBBLE CON AUTENTICACIÓN DESDE APPSETTINGS.JSON
// ===================================================================

// Opción recomendada: Cargar configuración desde appsettings.json
builder.Services.AddHubble(
    builder.Configuration,                                              // Configuración de la aplicación
    builder.Configuration.GetConnectionString("MongoConnection")!,     // Conexión a MongoDB
    "HubbleDB"                                                         // Nombre de la base de datos
);

// Agregar logging de Hubble para capturar logs de ILogger
builder.Logging.AddHubbleLogging();

// Configurar Entity Framework con interceptor de Hubble
builder.Services.AddDbContext<ApplicationDbContext>((serviceProvider, options) =>
{
    var httpContextAccessor = serviceProvider.GetRequiredService<IHttpContextAccessor>();
    
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"))
           .AddHubbleInterceptor(httpContextAccessor, "ApplicationDB");
});

var app = builder.Build();

// Configurar pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthentication(); // Si usas autenticación propia de la app
app.UseAuthorization();

// ===================================================================
// MIDDLEWARE DE HUBBLE - INCLUYE AUTENTICACIÓN AUTOMÁTICA
// ===================================================================
app.UseHubble();

app.MapControllers();

// Endpoints de ejemplo
app.MapGet("/api/test", () => "API funcionando correctamente")
   .WithName("TestEndpoint");

app.MapPost("/api/users", (User user) => 
{
    // Simular creación de usuario
    return Results.Created($"/api/users/{user.Id}", user);
});

app.Run();

// ===================================================================
// MODELOS Y CONTEXTO DE EJEMPLO
// ===================================================================

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }
    
    public DbSet<User> Users { get; set; }
    public DbSet<Product> Products { get; set; }
}

public class User
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class Product
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string Category { get; set; } = string.Empty;
}

/* ===================================================================
   ARCHIVO APPSETTINGS.JSON CORRESPONDIENTE:
   ===================================================================

{
  "ConnectionStrings": {
    "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=MyApp;Trusted_Connection=true;",
    "MongoConnection": "mongodb://localhost:27017"
  },
  "Hubble": {
    "RequireAuthentication": true,
    "Username": "admin",
    "Password": "hubble123",
    "BasePath": "/hubble",
    "ServiceName": "MiAplicacion",
    "EnableDiagnostics": false,
    "CaptureLoggerMessages": true,
    "CaptureHttpRequests": true,
    "IgnorePaths": [
      "/health",
      "/metrics",
      "/swagger"
    ],
    "IgnoreStaticFiles": true,
    "EnableDataPrune": true,
    "DataPruneIntervalHours": 24,
    "MaxLogAgeHours": 168,
    "TimeZoneId": "America/Mexico_City",
    "HighlightNewServices": false,
    "HighlightDurationSeconds": 5
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*"
}

   ===================================================================
   URLS DE ACCESO:
   ===================================================================
   
   - Interfaz Web: https://localhost:5001/hubble
   - API Logs: https://localhost:5001/hubble/api/logs
   - API Config: https://localhost:5001/hubble/api/config
   
   ===================================================================
   EJEMPLOS DE AUTENTICACIÓN:
   ===================================================================
   
   # Autenticación con cURL:
   curl -u admin:hubble123 "https://localhost:5001/hubble/api/logs"
   
   # Autenticación con PowerShell:
   $credentials = [Convert]::ToBase64String([Text.Encoding]::ASCII.GetBytes("admin:hubble123"))
   Invoke-RestMethod -Uri "https://localhost:5001/hubble/api/logs" -Headers @{Authorization = "Basic $credentials"}
   
   # Autenticación con HttpClient en C#:
   using var client = new HttpClient();
   var credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes("admin:hubble123"));
   client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);
   var response = await client.GetAsync("https://localhost:5001/hubble/api/logs");
   
   ===================================================================
*/
