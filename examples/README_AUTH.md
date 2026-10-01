# Configuración de Autenticación en Hubble

Este documento explica cómo configurar la autenticación en Hubble usando `appsettings.json`.

## Configuración

### 1. Agregar configuración en appsettings.json

Crea o actualiza tu archivo `appsettings.json` con la siguiente sección:

```json
{
  "ConnectionStrings": {
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
    "MaxLogAgeHours": 168,
    "TimeZoneId": "America/Mexico_City"
  }
}
```

### 2. Configurar en Program.cs

```csharp
using Gabonet.Hubble.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Configurar servicios
builder.Services.AddControllers();

// Configurar Hubble con autenticación desde appsettings.json
builder.Services.AddHubble(
    builder.Configuration.GetSection("Hubble"),                    // Sección "Hubble" de appsettings.json
    options =>
    {
        options.ConnectionString = builder.Configuration.GetConnectionString("MongoConnection")!;
        options.DatabaseName = "HubbleDB";
    });

// Agregar logging de Hubble
builder.Logging.AddHubbleLogging();

var app = builder.Build();

// Configurar pipeline
app.UseHttpsRedirection();
app.UseHubble(); // Esto incluye la autenticación

app.MapControllers();
app.Run();
```

## Opciones de Configuración

| Propiedad | Tipo | Descripción | Valor por Defecto |
|-----------|------|-------------|-------------------|
| `RequireAuthentication` | bool | Habilita la autenticación | `false` |
| `Username` | string | Nombre de usuario | `""` |
| `Password` | string | Contraseña | `""` |
| `BasePath` | string | Ruta base de Hubble | `"/hubble"` |
| `ServiceName` | string | Nombre del servicio | `"HubbleService"` |
| `EnableDiagnostics` | bool | Habilita diagnósticos | `false` |
| `CaptureLoggerMessages` | bool | Captura logs de ILogger (requiere `AddHubbleLogging()`) | `true` |
| `CaptureHttpRequests` | bool | Captura requests HTTP | `true` |
| `IgnorePaths` | string[] | Rutas a ignorar | `[]` |
| `IgnoreStaticFiles` | bool | Ignora archivos estáticos | `true` |
| `EnableDataPrune` | bool | Retención automática con índice TTL de MongoDB | `false` |
| `MaxLogAgeHours` | int | Edad máxima de logs (horas) | `24` |
| `TimeZoneId` | string | Zona horaria | `""` (UTC) |

## Tipos de Autenticación Soportados

### 1. Autenticación por Cookie (Interfaz Web)

Visita `https://tu-app/hubble` en tu navegador e ingresa las credenciales. Se creará una cookie de sesión cifrada y firmada con ASP.NET Core Data Protection (`HttpOnly`, `SameSite=Strict`), válida por 8 horas. El formulario de login está protegido contra CSRF y, tras 5 intentos fallidos desde la misma IP, el acceso se bloquea 15 minutos.

### 2. Autenticación Básica HTTP (APIs)

Para usar las APIs programáticamente:

```bash
# Con curl
curl -u admin:hubble123 "https://tu-app/hubble/api/logs"

# Con PowerShell
$credentials = [Convert]::ToBase64String([Text.Encoding]::ASCII.GetBytes("admin:hubble123"))
Invoke-RestMethod -Uri "https://tu-app/hubble/api/logs" -Headers @{Authorization = "Basic $credentials"}
```

```csharp
// Con HttpClient en C#
using var client = new HttpClient();
var credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes("admin:hubble123"));
client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);

var response = await client.GetAsync("https://tu-app/hubble/api/logs");
```

## Ejemplos de Uso

### Ejemplo 1: Solo habilitar autenticación

```json
{
  "Hubble": {
    "RequireAuthentication": true,
    "Username": "admin",
    "Password": "mi-password-seguro"
  }
}
```

### Ejemplo 2: Configuración completa de producción

```json
{
  "Hubble": {
    "RequireAuthentication": true,
    "Username": "hubble-admin",
    "Password": "P@ssw0rd123!",
    "BasePath": "/monitoring",
    "ServiceName": "ProductionAPI",
    "CaptureLoggerMessages": true,
    "CaptureHttpRequests": true,
    "IgnorePaths": [
      "/health",
      "/metrics",
      "/swagger",
      "/api/public"
    ],
    "EnableDataPrune": true,
    "MaxLogAgeHours": 72,
    "TimeZoneId": "America/Mexico_City"
  }
}
```

### Ejemplo 3: Desarrollo local sin autenticación

```json
{
  "Hubble": {
    "RequireAuthentication": false,
    "ServiceName": "DevAPI",
    "EnableDiagnostics": true,
    "CaptureLoggerMessages": true,
    "EnableDataPrune": false
  }
}
```

## Seguridad

### Recomendaciones:

1. **Usa contraseñas seguras** en producción
2. **Nunca commits credenciales** en el código fuente
3. **Usa variables de entorno o un gestor de secretos** para valores sensibles. ASP.NET Core **no** expande `${VARIABLE}` dentro de `appsettings.json`; en su lugar, deja la contraseña fuera del archivo y defínela con una variable de entorno que use `__` como separador de sección (sobrescribe automáticamente `Hubble:Password`):

4. **Usa HTTPS** en producción para proteger las credenciales
5. **Limita el acceso** solo a administradores con `Security.AllowedIps` (solo afecta al dashboard, no a tu aplicación)
6. **Varias instancias**: configura un almacén compartido de claves de Data Protection para que la cookie de sesión sea válida en todas

### Variables de Entorno:

```bash
# Linux/macOS
export Hubble__Username="admin"
export Hubble__Password="mi-password-seguro"

# Windows (PowerShell)
$env:Hubble__Username = "admin"
$env:Hubble__Password = "mi-password-seguro"
```

## Troubleshooting

### Problema: No puedo acceder a la interfaz

1. Si recibes `403`, tu IP no está en `Security.AllowedIps` (detrás de un proxy, configura `UseForwardedHeaders()` antes de `UseHubble()`)
2. Si recibes `429`, hubo demasiados intentos fallidos: espera 15 minutos
3. Confirma que las credenciales sean correctas
4. Si `RequireAuthentication` es `true` con usuario o contraseña vacíos, la aplicación no arranca: configura ambas credenciales

### Problema: API devuelve 401

1. Asegúrate de enviar el header `Authorization`
2. Verifica que la codificación Base64 sea correcta
3. Confirma que uses el formato `Basic username:password`

### Problema: Cookie expira muy rápido

La cookie tiene una duración fija de 8 horas. Si necesitas modificar esto, deberás personalizar el middleware.
