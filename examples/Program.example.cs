using Gabonet.Hubble.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = WebApplication.CreateBuilder(args);

// Agregar servicios al contenedor
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// === OPCIÓN 1: Configurar Hubble usando appsettings.json (RECOMENDADO) ===
// La configuración se carga automáticamente desde la sección "Hubble" en appsettings.json
builder.Services.AddHubble(
    builder.Configuration,
    builder.Configuration.GetConnectionString("MongoConnection")!,
    "HubbleDB"
);

// === OPCIÓN 2: Configurar Hubble manualmente (alternativa) ===
/*
builder.Services.AddHubble(options =>
{
    options.ConnectionString = builder.Configuration.GetConnectionString("MongoConnection")!;
    options.DatabaseName = "HubbleDB";
    options.ServiceName = "MiAplicacion";
    options.RequireAuthentication = true;
    options.Username = "admin";
    options.Password = "hubble123";
    options.BasePath = "/hubble";
    options.IgnorePaths.Add("/health");
    options.IgnorePaths.Add("/metrics");
    options.IgnoreStaticFiles = true;
    options.EnableDiagnostics = true; // Habilitar en desarrollo
    options.EnableDataPrune = true;
    options.DataPruneIntervalHours = 24;
    options.MaxLogAgeHours = 168;
});
*/

// Configurar Entity Framework con Hubble
builder.Services.AddDbContext<ApplicationDbContext>((serviceProvider, options) =>
{
    var httpContextAccessor = serviceProvider.GetRequiredService<IHttpContextAccessor>();
    
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"))
           .AddHubbleInterceptor(httpContextAccessor, "MiBaseDeDatos");
});

// Agregar logging de Hubble para capturar logs de ILogger
builder.Logging.AddHubbleLogging();

var app = builder.Build();

// Configurar el pipeline de solicitudes HTTP
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();

// Usar el middleware de Hubble
app.UseHubble();

app.MapControllers();

app.Run();

// Ejemplo de DbContext con Hubble
public class ApplicationDbContext : DbContext
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    
    public ApplicationDbContext(
        DbContextOptions<ApplicationDbContext> options,
        IHttpContextAccessor httpContextAccessor) : base(options)
    {
        _httpContextAccessor = httpContextAccessor;
    }
    
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        // Alternativa: configurar el interceptor aquí si no se hizo en la configuración del servicio
        // optionsBuilder.AddHubbleInterceptor(_httpContextAccessor, "MiBaseDeDatos");
        base.OnConfiguring(optionsBuilder);
    }
    
    // Definición de DbSets
    public DbSet<Usuario> Usuarios { get; set; }
    public DbSet<Producto> Productos { get; set; }
}

// Modelos de ejemplo
public class Usuario
{
    public int Id { get; set; }
    public string Nombre { get; set; }
    public string Email { get; set; }
}

public class Producto
{
    public int Id { get; set; }
    public string Nombre { get; set; }
    public decimal Precio { get; set; }
} 