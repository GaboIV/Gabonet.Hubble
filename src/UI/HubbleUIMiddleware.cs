namespace Gabonet.Hubble.UI;

using Gabonet.Hubble.Middleware;
using Gabonet.Hubble.UI.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;

/// <summary>
/// Middleware para manejar las rutas de la interfaz de usuario de Hubble.
/// </summary>
public class HubbleUIMiddleware
{
    private readonly RequestDelegate _next;
    private const string HtmlContentType = "text/html";

    /// <summary>
    /// Constructor del middleware de la interfaz de usuario de Hubble.
    /// </summary>
    /// <param name="next">Siguiente middleware en la cadena</param>
    public HubbleUIMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    /// <summary>
    /// Método principal del middleware que procesa la solicitud HTTP.
    /// </summary>
    /// <param name="context">Contexto HTTP</param>
    /// <returns>Tarea asíncrona</returns>
    public async Task InvokeAsync(HttpContext context, HubbleOptions options)
    {
        var basePath = options.BasePath.ToLower();
        var path = context.Request.Path.Value?.ToLower() ?? "";
        
        // Solo procesar solicitudes que empiecen con la ruta base de Hubble
        if (!path.StartsWith(basePath))
        {
            await _next(context);
            return;
        }

        // Verificar autenticación si está habilitada
        if (options.RequireAuthentication && !IsAuthenticated(context, options))
        {
            if (path.Equals($"{basePath}/login", StringComparison.OrdinalIgnoreCase) && 
                context.Request.Method.Equals("POST", StringComparison.OrdinalIgnoreCase))
            {
                await HandleLoginAsync(context, options);
            }
            else
            {
                await ShowLoginFormAsync(context);
            }
            return;
        }

        // Manejar diferentes rutas
        if (path.Equals(basePath, StringComparison.OrdinalIgnoreCase) || 
            path.Equals($"{basePath}/", StringComparison.OrdinalIgnoreCase))
        {
            await HandleHubbleHomeAsync(context);
        }
        else if (path.StartsWith($"{basePath}/detail/", StringComparison.OrdinalIgnoreCase))
        {
            await HandleHubbleDetailAsync(context);
        }
        else if (path.Equals($"{basePath}/delete-all", StringComparison.OrdinalIgnoreCase))
        {
            await HandleHubbleDeleteAllAsync(context);
        }
        else if (path.StartsWith($"{basePath}/api/", StringComparison.OrdinalIgnoreCase))
        {
            await HandleHubbleApiAsync(context);
        }
        else if (path.Equals($"{basePath}/logout", StringComparison.OrdinalIgnoreCase))
        {
            await HandleLogoutAsync(context);
        }
        // Nueva ruta para la página de configuración y estadísticas
        else if (path.Equals($"{basePath}/config", StringComparison.OrdinalIgnoreCase))
        {
            await HandleConfigPageAsync(context);
        }
        // Ruta para ejecutar limpieza manual
        else if (path.Equals($"{basePath}/run-prune", StringComparison.OrdinalIgnoreCase))
        {
            await HandleRunPruneAsync(context);
        }
        // Ruta para recalcular estadísticas
        else if (path.Equals($"{basePath}/recalculate-stats", StringComparison.OrdinalIgnoreCase))
        {
            await HandleRecalculateStatsAsync(context);
        }
        // Ruta para guardar configuración de prune
        else if (path.Equals($"{basePath}/save-config", StringComparison.OrdinalIgnoreCase) && 
                 context.Request.Method.Equals("POST", StringComparison.OrdinalIgnoreCase))
        {
            await HandleSavePruneConfigAsync(context);
        }
        // Ruta para guardar configuración de captura
        else if (path.Equals($"{basePath}/save-capture-config", StringComparison.OrdinalIgnoreCase) && 
                 context.Request.Method.Equals("POST", StringComparison.OrdinalIgnoreCase))
        {
            await HandleSaveCaptureConfigAsync(context);
        }
        // Ruta para guardar rutas ignoradas
        else if (path.Equals($"{basePath}/save-ignore-paths", StringComparison.OrdinalIgnoreCase) && 
                 context.Request.Method.Equals("POST", StringComparison.OrdinalIgnoreCase))
        {
            await HandleSaveIgnorePathsAsync(context);
        }
        else
        {
            // Para cualquier otra ruta, continuar con el siguiente middleware
            await _next(context);
        }
    }

    private async Task HandleHubbleHomeAsync(HttpContext context)
    {
        try
        {
            // Obtener parámetros de consulta
            var method = context.Request.Query["method"].ToString();
            var url = context.Request.Query["url"].ToString();
            var statusGroup = context.Request.Query["statusGroup"].ToString();
            var logType = context.Request.Query["logType"].ToString();
            var pageStr = context.Request.Query["page"].ToString();
            var pageSizeStr = context.Request.Query["pageSize"].ToString();

            int page = 1;
            int pageSize = 50;

            if (!string.IsNullOrEmpty(pageStr) && int.TryParse(pageStr, out int parsedPage))
            {
                page = parsedPage;
            }

            if (!string.IsNullOrEmpty(pageSizeStr) && int.TryParse(pageSizeStr, out int parsedPageSize))
            {
                pageSize = parsedPageSize;
            }

            // Obtener el controlador de Hubble
            var hubbleController = context.RequestServices.GetRequiredService<HubbleController>();
            var html = await hubbleController.GetLogsViewAsync(method, url, statusGroup, logType, page, pageSize);
            
            context.Response.ContentType = HtmlContentType;
            await context.Response.WriteAsync(html);
        }
        catch (Exception ex)
        {
            context.Response.StatusCode = 500;
            await context.Response.WriteAsync($"Error: {ex.Message}");
        }
    }

    private async Task HandleHubbleDetailAsync(HttpContext context)
    {
        try
        {
            var pathParts = context.Request.Path.Value?.Split('/') ?? Array.Empty<string>();
            var id = pathParts.Length > 0 ? pathParts[pathParts.Length - 1] : "";

            // Obtener el controlador de Hubble
            var hubbleController = context.RequestServices.GetRequiredService<HubbleController>();
            var html = await hubbleController.GetLogDetailAsync(id ?? "");

            context.Response.ContentType = HtmlContentType;
            await context.Response.WriteAsync(html);
        }
        catch (Exception ex)
        {
            context.Response.StatusCode = 500;
            await context.Response.WriteAsync($"Error: {ex.Message}");
        }
    }

    private async Task HandleHubbleDeleteAllAsync(HttpContext context)
    {
        try
        {
            // Obtener el controlador de Hubble
            var hubbleController = context.RequestServices.GetRequiredService<HubbleController>();
            var html = await hubbleController.DeleteAllLogsAsync();

            context.Response.ContentType = HtmlContentType;
            await context.Response.WriteAsync(html);
        }
        catch (Exception ex)
        {
            context.Response.StatusCode = 500;
            await context.Response.WriteAsync($"Error: {ex.Message}");
        }
    }

    private async Task HandleHubbleApiAsync(HttpContext context)
    {
        try
        {
            var path = context.Request.Path.Value?.ToLower() ?? "";
            var method = context.Request.Method.ToUpper();
            var hubbleController = context.RequestServices.GetRequiredService<HubbleController>();

            context.Response.ContentType = "application/json";

            // Extract the API path after /api/
            var basePath = context.RequestServices.GetRequiredService<HubbleOptions>().BasePath.ToLower();
            var apiPath = path.Substring($"{basePath}/api".Length);

            switch (method)
            {
                case "GET":
                    await HandleGetApiAsync(context, apiPath, hubbleController);
                    break;
                case "POST":
                    await HandlePostApiAsync(context, apiPath, hubbleController);
                    break;
                case "DELETE":
                    await HandleDeleteApiAsync(context, apiPath, hubbleController);
                    break;
                default:
                    context.Response.StatusCode = 405; // Method Not Allowed
                    await context.Response.WriteAsync(JsonConvert.SerializeObject(new ApiResponse
                    {
                        Success = false,
                        Message = $"HTTP method {method} is not supported"
                    }));
                    break;
            }
        }
        catch (Exception ex)
        {
            context.Response.StatusCode = 500;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(JsonConvert.SerializeObject(new ApiResponse
            {
                Success = false,
                Message = $"Internal server error: {ex.Message}"
            }));
        }
    }

    private async Task HandleGetApiAsync(HttpContext context, string apiPath, HubbleController controller)
    {
        switch (apiPath)
        {
            case "/logs":
                // GET /api/logs - Get logs with filtering and pagination
                var method = context.Request.Query["method"].ToString();
                var url = context.Request.Query["url"].ToString();
                var statusGroup = context.Request.Query["statusGroup"].ToString();
                var logType = context.Request.Query["logType"].ToString();
                var pageStr = context.Request.Query["page"].ToString();
                var pageSizeStr = context.Request.Query["pageSize"].ToString();

                int page = 1;
                int pageSize = 50;

                if (!string.IsNullOrEmpty(pageStr) && int.TryParse(pageStr, out int parsedPage))
                {
                    page = parsedPage;
                }

                if (!string.IsNullOrEmpty(pageSizeStr) && int.TryParse(pageSizeStr, out int parsedPageSize))
                {
                    pageSize = parsedPageSize;
                }

                var logsResponse = await controller.GetLogsApiAsync(method, url, statusGroup, logType, page, pageSize);
                await context.Response.WriteAsync(JsonConvert.SerializeObject(logsResponse));
                break;

            case var logDetailPath when logDetailPath.StartsWith("/logs/"):
                // GET /api/logs/{id} - Get log details
                var logId = logDetailPath.Substring("/logs/".Length);
                var logDetailResponse = await controller.GetLogDetailApiAsync(logId);
                
                if (!logDetailResponse.Found)
                {
                    context.Response.StatusCode = 404;
                }
                
                await context.Response.WriteAsync(JsonConvert.SerializeObject(logDetailResponse));
                break;

            case "/config":
                // GET /api/config - Get configuration and statistics
                var configResponse = await controller.GetConfigurationApiAsync();
                await context.Response.WriteAsync(JsonConvert.SerializeObject(configResponse));
                break;

            case "/prune":
                // GET /api/prune - Run manual prune operation
                var pruneResponse = await controller.RunManualPruneApiAsync();
                await context.Response.WriteAsync(JsonConvert.SerializeObject(pruneResponse));
                break;

            case "/recalculate-stats":
                // GET /api/recalculate-stats - Recalculate statistics
                var recalcResponse = await controller.RecalculateStatisticsApiAsync();
                await context.Response.WriteAsync(JsonConvert.SerializeObject(recalcResponse));
                break;

            default:
                context.Response.StatusCode = 404;
                await context.Response.WriteAsync(JsonConvert.SerializeObject(new ApiResponse
                {
                    Success = false,
                    Message = $"API endpoint not found: {apiPath}"
                }));
                break;
        }
    }

    private async Task HandlePostApiAsync(HttpContext context, string apiPath, HubbleController controller)
    {
        // Read request body
        string requestBody;
        using (var reader = new StreamReader(context.Request.Body))
        {
            requestBody = await reader.ReadToEndAsync();
        }

        switch (apiPath)
        {
            case "/config/prune":
                // POST /api/config/prune - Save prune configuration
                var pruneRequest = JsonConvert.DeserializeObject<SavePruneConfigRequest>(requestBody);
                if (pruneRequest == null)
                {
                    context.Response.StatusCode = 400;
                    await context.Response.WriteAsync(JsonConvert.SerializeObject(new ApiResponse
                    {
                        Success = false,
                        Message = "Invalid request body"
                    }));
                    return;
                }

                var pruneResponse = await controller.SavePruneConfigApiAsync(pruneRequest);
                await context.Response.WriteAsync(JsonConvert.SerializeObject(pruneResponse));
                break;

            case "/config/capture":
                // POST /api/config/capture - Save capture configuration
                var captureRequest = JsonConvert.DeserializeObject<SaveCaptureConfigRequest>(requestBody);
                if (captureRequest == null)
                {
                    context.Response.StatusCode = 400;
                    await context.Response.WriteAsync(JsonConvert.SerializeObject(new ApiResponse
                    {
                        Success = false,
                        Message = "Invalid request body"
                    }));
                    return;
                }

                var captureResponse = await controller.SaveCaptureConfigApiAsync(captureRequest);
                await context.Response.WriteAsync(JsonConvert.SerializeObject(captureResponse));
                break;

            case "/config/ignore-paths":
                // POST /api/config/ignore-paths - Save ignore paths configuration
                var ignoreRequest = JsonConvert.DeserializeObject<SaveIgnorePathsRequest>(requestBody);
                if (ignoreRequest == null)
                {
                    context.Response.StatusCode = 400;
                    await context.Response.WriteAsync(JsonConvert.SerializeObject(new ApiResponse
                    {
                        Success = false,
                        Message = "Invalid request body"
                    }));
                    return;
                }

                var ignoreResponse = await controller.SaveIgnorePathsApiAsync(ignoreRequest);
                await context.Response.WriteAsync(JsonConvert.SerializeObject(ignoreResponse));
                break;

            default:
                context.Response.StatusCode = 404;
                await context.Response.WriteAsync(JsonConvert.SerializeObject(new ApiResponse
                {
                    Success = false,
                    Message = $"API endpoint not found: {apiPath}"
                }));
                break;
        }
    }

    private async Task HandleDeleteApiAsync(HttpContext context, string apiPath, HubbleController controller)
    {
        switch (apiPath)
        {
            case "/logs":
                // DELETE /api/logs - Delete all logs
                var deleteResponse = await controller.DeleteAllLogsApiAsync();
                await context.Response.WriteAsync(JsonConvert.SerializeObject(deleteResponse));
                break;

            default:
                context.Response.StatusCode = 404;
                await context.Response.WriteAsync(JsonConvert.SerializeObject(new ApiResponse
                {
                    Success = false,
                    Message = $"API endpoint not found: {apiPath}"
                }));
                break;
        }
    }

    private async Task HandleLoginAsync(HttpContext context, HubbleOptions options)
    {
        var basePath = options.BasePath.ToLower();
        
        // Leer el cuerpo de la solicitud para obtener las credenciales
        context.Request.EnableBuffering();
        using var reader = new System.IO.StreamReader(context.Request.Body, Encoding.UTF8, true, 1024, true);
        var body = await reader.ReadToEndAsync();
        context.Request.Body.Position = 0;

        // Procesar los datos del formulario
        var formData = System.Web.HttpUtility.ParseQueryString(body);
        var username = formData["username"];
        var password = formData["password"];

        if (username == options.Username && password == options.Password)
        {
            // Crear una cookie de autenticación
            context.Response.Cookies.Append("HubbleAuth", GenerateAuthToken(username, password), new CookieOptions
            {
                HttpOnly = true,
                Secure = context.Request.IsHttps,
                SameSite = SameSiteMode.Lax,
                Expires = DateTimeOffset.Now.AddHours(8), // La cookie expira después de 8 horas
                Path = basePath // Solo válida para las rutas de Hubble
            });

            // Redirigir al usuario a la página principal de Hubble
            context.Response.Redirect(basePath);
        }
        else
        {
            // Si las credenciales son incorrectas, mostrar el formulario de inicio de sesión con error
            await ShowLoginFormAsync(context, true);
        }
    }

    private async Task HandleLogoutAsync(HttpContext context)
    {
        var options = context.RequestServices.GetRequiredService<HubbleOptions>();
        var basePath = options.BasePath.ToLower();
        
        // Eliminar la cookie de autenticación
        context.Response.Cookies.Delete("HubbleAuth", new CookieOptions
        {
            Path = basePath
        });

        // Redirigir al formulario de inicio de sesión
        context.Response.Redirect(basePath);
    }

    private async Task ShowLoginFormAsync(HttpContext context, bool showError = false)
    {
        var options = context.RequestServices.GetRequiredService<HubbleOptions>();
        var basePath = options.BasePath.ToLower();
        
        // Obtener el controlador para acceder al logo y la versión
        var hubbleController = context.RequestServices.GetRequiredService<HubbleController>();
        var hubbleLogo = hubbleController.GetHubbleLogo();
        var version = hubbleController.GetVersion();
        
        var html = $@"
<!DOCTYPE html>
<html lang='es'>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <title>Hubble - Iniciar sesión</title>
    <style>
        :root {{
            --primary-color: #6200ee;
            --primary-light: #bb86fc;
            --secondary-color: #03dac6;
            --background: #121212;
            --surface: #1e1e1e;
            --error: #cf6679;
            --text-primary: #ffffff;
            --text-secondary: rgba(255, 255, 255, 0.7);
            --border-color: #333333;
        }}
        
        * {{
            box-sizing: border-box;
            margin: 0;
            padding: 0;
        }}
        
        body {{
            font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
            background-color: var(--background);
            color: var(--text-primary);
            line-height: 1.6;
            height: 100vh;
            display: flex;
            justify-content: center;
            align-items: center;
        }}
        
        .login-container {{
            background-color: var(--surface);
            border-radius: 8px;
            box-shadow: 0 4px 6px rgba(0, 0, 0, 0.1);
            padding: 2rem;
            width: 90%;
            max-width: 400px;
        }}
        
        .login-header {{
            text-align: center;
            margin-bottom: 2rem;
        }}
        
        .login-logo {{
            margin-bottom: 1rem;
        }}
        
        .logo-container {{
            display: flex;
            justify-content: center;
            align-items: center;
            margin-bottom: 15px;
        }}
        
        .app-info {{
            text-align: center;
            margin-bottom: 15px;
        }}
        
        .app-title {{
            font-size: 1em;
            color: var(--text-primary);
            font-weight: 500;
        }}
        
        .app-version {{
            color: var(--secondary-color);
            font-size: 0.85em;
            margin-left: 5px;
        }}
        
        .login-subtitle {{
            color: var(--text-secondary);
            font-size: 1rem;
        }}
        
        .login-form {{
            display: flex;
            flex-direction: column;
        }}
        
        .form-group {{
            margin-bottom: 1.5rem;
        }}
        
        .password-group {{
            position: relative;
        }}
        
        label {{
            display: block;
            margin-bottom: 0.5rem;
            color: var(--text-secondary);
        }}
        
        input {{
            width: 100%;
            padding: 0.75rem;
            border: 1px solid var(--border-color);
            border-radius: 4px;
            background-color: rgba(255, 255, 255, 0.1);
            color: var(--text-primary);
            font-size: 1rem;
        }}
        
        input:focus {{
            outline: none;
            border-color: var(--primary-light);
            box-shadow: 0 0 0 2px rgba(187, 134, 252, 0.25);
        }}
        
        .password-input {{
            padding-right: 2.5rem;
        }}
        
        .password-toggle {{
            position: absolute;
            right: 1rem;
            top: 2.75rem;
            background: none;
            border: none;
            color: var(--text-secondary);
            cursor: pointer;
            padding: 0;
            width: 1.5rem;
            height: 1.5rem;
            display: flex;
            align-items: center;
            justify-content: center;
            transition: color 0.2s;
        }}
        
        .password-toggle:hover {{
            color: var(--text-primary);
        }}
        
        .password-toggle svg {{
            width: 1.2rem;
            height: 1.2rem;
        }}
        
        button {{
            display: inline-block;
            width: 100%;
            padding: 0.75rem;
            border: none;
            border-radius: 4px;
            background-color: var(--primary-color);
            color: white;
            font-size: 1rem;
            font-weight: 600;
            cursor: pointer;
            transition: background-color 0.2s;
        }}
        
        button:hover {{
            background-color: var(--primary-light);
        }}
        
        .error-message {{
            background-color: rgba(207, 102, 121, 0.1);
            border-left: 4px solid var(--error);
            color: var(--error);
            padding: 0.75rem;
            margin-bottom: 1.5rem;
            border-radius: 0 4px 4px 0;
        }}
    </style>
</head>
<body>
    <div class='login-container'>
        <div class='login-header'>
            <div class='login-logo'>
                {hubbleLogo}
            </div>
            <div class='app-info'>
                <p><span class='app-title'>Hubble for .NET</span> <span class='app-version'>{version}</span></p>
            </div>
            <p class='login-subtitle'>Inicia sesión para continuar</p>
        </div>
        
        {(showError ? @"<div class='error-message'>Nombre de usuario o contraseña incorrectos</div>" : "")}
        
        <form class='login-form' method='post' action='{basePath}/login'>
            <div class='form-group'>
                <label for='username'>Nombre de usuario</label>
                <input type='text' id='username' name='username' required autofocus />
            </div>
            
            <div class='form-group password-group'>
                <label for='password'>Contraseña</label>
                <input type='password' id='password' name='password' class='password-input' required />
                <button type='button' class='password-toggle' onclick='togglePassword()' title='Mostrar/ocultar contraseña'>
                    <svg id='eye-icon' viewBox='0 0 24 24' fill='currentColor'>
                        <path d='M12 4.5C7 4.5 2.73 7.61 1 12c1.73 4.39 6 7.5 11 7.5s9.27-3.11 11-7.5c-1.73-4.39-6-7.5-11-7.5zM12 17c-2.76 0-5-2.24-5-5s2.24-5 5-5 5 2.24 5 5-2.24 5-5 5zm0-8c-1.66 0-3 1.34-3 3s1.34 3 3 3 3-1.34 3-3-1.34-3-3-3z'/>
                    </svg>
                </button>
            </div>
            
            <button type='submit'>Iniciar sesión</button>
        </form>
    </div>

    <script>
        function togglePassword() {{
            const passwordInput = document.getElementById('password');
            const eyeIcon = document.getElementById('eye-icon');
            const isPassword = passwordInput.type === 'password';
            
            passwordInput.type = isPassword ? 'text' : 'password';
            
            // Cambiar el icono
            if (isPassword) {{
                // Icono de ojo con tachado (ocultar)
                eyeIcon.innerHTML = '<path d=""M2 4.27l2.28 2.28.46.46C3.08 8.3 1.78 10.02 1 12c1.73 4.39 6 7.5 11 7.5 1.55 0 3.03-.3 4.38-.84l.42.42L19.73 22 21 20.73 3.27 3 2 4.27zM7.53 9.8l1.55 1.55c-.05.21-.08.43-.08.65 0 1.66 1.34 3 3 3 .22 0 .44-.03.65-.08l1.55 1.55c-.67.33-1.41.53-2.2.53-2.76 0-5-2.24-5-5 0-.79.2-1.53.53-2.2zm4.31-.78l3.15 3.15.02-.16c0-1.66-1.34-3-3-3l-.17.01z""/><path d=""M14.12 9.88c.09.46.04.87-.12 1.27l4.26 4.26c.94-.85 1.74-1.8 2.36-2.79-1.73-4.39-6-7.5-11-7.5-1.4 0-2.74.25-3.98.7l2.66 2.66c.4-.16.81-.21 1.27-.12l4.55 4.55z""/>';
            }} else {{
                // Icono de ojo normal (mostrar)
                eyeIcon.innerHTML = '<path d=""M12 4.5C7 4.5 2.73 7.61 1 12c1.73 4.39 6 7.5 11 7.5s9.27-3.11 11-7.5c-1.73-4.39-6-7.5-11-7.5zM12 17c-2.76 0-5-2.24-5-5s2.24-5 5-5 5 2.24 5 5-2.24 5-5 5zm0-8c-1.66 0-3 1.34-3 3s1.34 3 3 3 3-1.34 3-3-1.34-3-3-3z""/>';
            }}
        }}
    </script>
</body>
</html>";

        context.Response.ContentType = "text/html";
        await context.Response.WriteAsync(html);
    }

    private bool IsAuthenticated(HttpContext context, HubbleOptions options)
    {
        // Si no se requiere autenticación, considerar siempre autenticado
        if (!options.RequireAuthentication)
        {
            return true;
        }

        // Verificar autenticación básica HTTP primero
        if (IsBasicAuthValid(context, options))
        {
            return true;
        }

        // Verificar si existe la cookie de autenticación
        if (context.Request.Cookies.TryGetValue("HubbleAuth", out string? authToken))
        {
            // Verificar que el token sea válido
            return ValidateAuthToken(authToken, options.Username, options.Password);
        }

        return false;
    }

    private bool IsBasicAuthValid(HttpContext context, HubbleOptions options)
    {
        try
        {
            // Verificar si hay encabezado de autorización
            if (!context.Request.Headers.ContainsKey("Authorization"))
            {
                return false;
            }

            var authHeader = context.Request.Headers["Authorization"].ToString();
            
            // Verificar que sea Basic Auth
            if (!authHeader.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            // Decodificar las credenciales
            var encodedCredentials = authHeader.Substring("Basic ".Length).Trim();
            var decodedBytes = Convert.FromBase64String(encodedCredentials);
            var credentials = Encoding.UTF8.GetString(decodedBytes);
            
            // Separar usuario y contraseña
            var parts = credentials.Split(':', 2);
            if (parts.Length != 2)
            {
                return false;
            }

            var username = parts[0];
            var password = parts[1];

            // Verificar las credenciales
            return username == options.Username && password == options.Password;
        }
        catch
        {
            return false;
        }
    }

    private static string GenerateAuthToken(string username, string password)
    {
        // Crear un token simple basado en username
        // En un entorno de producción, se recomendaría usar algo más seguro como JWT
        var tokenData = $"{username}:{DateTime.UtcNow.Ticks}";
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(tokenData));
    }

    private static bool ValidateAuthToken(string token, string username, string password)
    {
        try
        {
            // Decodificar el token
            var tokenData = Encoding.UTF8.GetString(Convert.FromBase64String(token));
            var parts = tokenData.Split(':');
            
            // Verificar que el token tenga el formato correcto
            if (parts.Length != 2)
            {
                return false;
            }

            // Verificar que el username en el token coincida con el configurado
            var tokenUsername = parts[0];
            if (tokenUsername != username)
            {
                return false;
            }

            // Verificar que el token no haya expirado (8 horas)
            if (long.TryParse(parts[1], out long timestamp))
            {
                var tokenTime = new DateTime(timestamp, DateTimeKind.Utc);
                if (DateTime.UtcNow.Subtract(tokenTime).TotalHours > 8)
                {
                    return false;
                }
            }
            else
            {
                return false;
            }

            return true;
        }
        catch
        {
            return false;
        }
    }
    
    private async Task HandleConfigPageAsync(HttpContext context)
    {
        var hubbleController = context.RequestServices.GetRequiredService<HubbleController>();
        var html = await hubbleController.GetConfigurationPageAsync();
        
        context.Response.ContentType = "text/html";
        await context.Response.WriteAsync(html);
    }
    
    private async Task HandleRunPruneAsync(HttpContext context)
    {
        var hubbleController = context.RequestServices.GetRequiredService<HubbleController>();
        var html = await hubbleController.RunManualPruneAsync();
        
        context.Response.ContentType = "text/html";
        await context.Response.WriteAsync(html);
    }
    
    private async Task HandleRecalculateStatsAsync(HttpContext context)
    {
        var hubbleController = context.RequestServices.GetRequiredService<HubbleController>();
        var html = await hubbleController.RecalculateStatisticsAsync();
        
        context.Response.ContentType = "text/html";
        await context.Response.WriteAsync(html);
    }
    
    private async Task HandleSavePruneConfigAsync(HttpContext context)
    {
        // Obtener los valores del formulario
        var form = await context.Request.ReadFormAsync();
        bool enableDataPrune = form.ContainsKey("enableDataPrune");
        
        // Intentar parsear los valores numéricos
        if (!int.TryParse(form["dataPruneIntervalHours"], out int dataPruneIntervalHours))
        {
            dataPruneIntervalHours = 1; // Valor por defecto
        }
        
        if (!int.TryParse(form["maxLogAgeHours"], out int maxLogAgeHours))
        {
            maxLogAgeHours = 24; // Valor por defecto
        }
        
        var hubbleController = context.RequestServices.GetRequiredService<HubbleController>();
        var html = await hubbleController.SavePruneConfigAsync(enableDataPrune, dataPruneIntervalHours, maxLogAgeHours);
        
        context.Response.ContentType = "text/html";
        await context.Response.WriteAsync(html);
    }
    
    private async Task HandleSaveCaptureConfigAsync(HttpContext context)
    {
        // Obtener los valores del formulario
        var form = await context.Request.ReadFormAsync();
        bool captureHttpRequests = form.ContainsKey("captureHttpRequests");
        bool captureLoggerMessages = form.ContainsKey("captureLoggerMessages");
        string minimumLogLevel = form["minimumLogLevel"].ToString() ?? "Information";
        
        var hubbleController = context.RequestServices.GetRequiredService<HubbleController>();
        var html = await hubbleController.SaveCaptureConfigAsync(captureHttpRequests, captureLoggerMessages, minimumLogLevel);
        
        context.Response.ContentType = "text/html";
        await context.Response.WriteAsync(html);
    }
    
    private async Task HandleSaveIgnorePathsAsync(HttpContext context)
    {
        // Obtener los valores del formulario
        var form = await context.Request.ReadFormAsync();
        string ignorePaths = form["ignorePaths"].ToString() ?? string.Empty;
        
        var hubbleController = context.RequestServices.GetRequiredService<HubbleController>();
        var html = await hubbleController.SaveIgnorePathsAsync(ignorePaths);
        
        context.Response.ContentType = "text/html";
        await context.Response.WriteAsync(html);
    }
} 