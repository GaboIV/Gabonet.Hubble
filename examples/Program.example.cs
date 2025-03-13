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

// Configurar Telescope
builder.Services.AddTelescope(options =>
{
    options.MongoConnectionString = "mongodb://localhost:27017";
    options.DatabaseName = "TelescopeDB";
    options.ServiceName = "MiAplicacion";
    options.IgnorePaths.Add("/health");
    options.IgnorePaths.Add("/metrics");
    options.IgnoreStaticFiles = true;
    options.EnableDiagnostics = true; // Habilitar en desarrollo
});

// Configurar Entity Framework con Telescope
builder.Services.AddDbContext<ApplicationDbContext>((serviceProvider, options) =>
{
    var httpContextAccessor = serviceProvider.GetRequiredService<IHttpContextAccessor>();
    
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"))
           .AddTelescopeInterceptor(httpContextAccessor, "MiBaseDeDatos");
});

var app = builder.Build();

// Configurar el pipeline de solicitudes HTTP
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();

// Usar el middleware de Telescope
app.UseTelescope();

app.MapControllers();

app.Run();

// Ejemplo de DbContext con Telescope
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
        // optionsBuilder.AddTelescopeInterceptor(_httpContextAccessor, "MiBaseDeDatos");
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