namespace Gabonet.Hubble.UI;

using Gabonet.Hubble.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using System;
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
        var path = context.Request.Path.Value?.ToLower();

        if (string.IsNullOrEmpty(path))
        {
            await _next(context);
            return;
        }

        // Verificar si la ruta es para la interfaz de usuario de Hubble
        if (path.StartsWith("/hubble"))
        {
            // Si se requiere autenticación, verificar que las credenciales sean correctas
            if (options.RequireAuthentication && !IsAuthenticated(context, options))
            {
                // Si es una solicitud de inicio de sesión, verificar las credenciales
                if (path == "/hubble/login" && context.Request.Method == "POST")
                {
                    await HandleLoginAsync(context, options);
                    return;
                }
                
                // Si la solicitud no está autenticada, mostrar el formulario de inicio de sesión
                await ShowLoginFormAsync(context);
                return;
            }

            // Si está autenticado o no se requiere autenticación, procesar la solicitud
            if (path == "/hubble" || path == "/hubble/")
            {
                await HandleHubbleHomeAsync(context);
                return;
            }
            else if (path.StartsWith("/hubble/detail/"))
            {
                await HandleHubbleDetailAsync(context);
                return;
            }
            else if (path == "/hubble/delete-all")
            {
                await HandleHubbleDeleteAllAsync(context);
                return;
            }
            else if (path.StartsWith("/hubble/api/"))
            {
                await HandleHubbleApiAsync(context);
                return;
            }
            else if (path == "/hubble/logout" && options.RequireAuthentication)
            {
                await HandleLogoutAsync(context);
                return;
            }
        }

        // Si no es una ruta de Hubble, continuar con el siguiente middleware
        await _next(context);
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
            
            context.Response.ContentType = "text/html";
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
            var id = context.Request.Path.Value?.Split('/').Last();

            // Obtener el controlador de Hubble
            var hubbleController = context.RequestServices.GetRequiredService<HubbleController>();
            var html = await hubbleController.GetLogDetailAsync(id ?? "");

            context.Response.ContentType = "text/html";
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

            context.Response.ContentType = "text/html";
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
        // Aquí se pueden implementar endpoints API para Hubble si es necesario
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync("{\"message\": \"API de Hubble no implementada\"}");
    }

    private async Task HandleLoginAsync(HttpContext context, HubbleOptions options)
    {
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
                Path = "/hubble" // Solo válida para las rutas de Hubble
            });

            // Redirigir al usuario a la página principal de Hubble
            context.Response.Redirect("/hubble");
        }
        else
        {
            // Si las credenciales son incorrectas, mostrar el formulario de inicio de sesión con error
            await ShowLoginFormAsync(context, true);
        }
    }

    private async Task HandleLogoutAsync(HttpContext context)
    {
        // Eliminar la cookie de autenticación
        context.Response.Cookies.Delete("HubbleAuth", new CookieOptions
        {
            Path = "/hubble"
        });

        // Redirigir al formulario de inicio de sesión
        context.Response.Redirect("/hubble");
    }

    private async Task ShowLoginFormAsync(HttpContext context, bool showError = false)
    {
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
            background-color: var(--background);
            color: var(--text-primary);
            font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
            line-height: 1.6;
            height: 100vh;
            display: flex;
            align-items: center;
            justify-content: center;
        }}
        
        .login-container {{
            background-color: var(--surface);
            border-radius: 8px;
            padding: 30px;
            width: 100%;
            max-width: 400px;
            box-shadow: 0 4px 6px rgba(0, 0, 0, 0.1);
        }}
        
        .login-header {{
            text-align: center;
            margin-bottom: 30px;
        }}
        
        .login-header h1 {{
            color: var(--primary-light);
            margin-bottom: 10px;
        }}
        
        .login-form {{
            display: flex;
            flex-direction: column;
        }}
        
        .form-group {{
            margin-bottom: 20px;
        }}
        
        .form-group label {{
            display: block;
            margin-bottom: 5px;
            color: var(--text-secondary);
        }}
        
        .form-group input {{
            width: 100%;
            padding: 10px 15px;
            border: 1px solid var(--border-color);
            border-radius: 4px;
            background-color: rgba(255, 255, 255, 0.1);
            color: var(--text-primary);
            font-size: 16px;
        }}
        
        .form-group input:focus {{
            outline: none;
            border-color: var(--primary-light);
        }}
        
        .btn {{
            background-color: var(--primary-color);
            color: white;
            border: none;
            padding: 12px 20px;
            border-radius: 4px;
            cursor: pointer;
            text-decoration: none;
            font-weight: 500;
            transition: background-color 0.3s;
            font-size: 16px;
        }}
        
        .btn:hover {{
            background-color: var(--primary-light);
        }}
        
        .error-message {{
            color: var(--error);
            margin-bottom: 20px;
            text-align: center;
            display: {(showError ? "block" : "none")};
        }}
    </style>
</head>
<body>
    <div class='login-container'>
        <div class='login-header'>
            <h1>Hubble</h1>
            <p>Iniciar sesión para acceder al panel</p>
        </div>
        
        <div class='error-message'>Nombre de usuario o contraseña incorrectos</div>
        
        <form class='login-form' method='post' action='/hubble/login'>
            <div class='form-group'>
                <label for='username'>Nombre de usuario</label>
                <input type='text' id='username' name='username' required autofocus>
            </div>
            
            <div class='form-group'>
                <label for='password'>Contraseña</label>
                <input type='password' id='password' name='password' required>
            </div>
            
            <button type='submit' class='btn'>Iniciar sesión</button>
        </form>
    </div>
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

        // Verificar si existe la cookie de autenticación
        if (context.Request.Cookies.TryGetValue("HubbleAuth", out string? authToken))
        {
            // Verificar que el token sea válido
            return ValidateAuthToken(authToken, options.Username, options.Password);
        }

        return false;
    }

    private string GenerateAuthToken(string username, string password)
    {
        // Crear un token simple basado en username y password
        // En un entorno de producción, se recomendaría usar algo más seguro como JWT
        var tokenData = $"{username}:{DateTime.UtcNow.Ticks}";
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(tokenData));
    }

    private bool ValidateAuthToken(string token, string username, string password)
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
                var tokenTime = new DateTime(timestamp);
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
} 