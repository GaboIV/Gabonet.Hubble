namespace Gabonet.Hubble.UI;

using Gabonet.Hubble.Middleware;
using Gabonet.Hubble.Security;
using Gabonet.Hubble.UI.Models;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

/// <summary>
/// Middleware para manejar las rutas de la interfaz de usuario de Hubble.
/// </summary>
public class HubbleUIMiddleware
{
    /// <summary>
    /// Tamaño máximo de página permitido en el listado de logs (HTML y API).
    /// </summary>
    public const int MaxPageSize = 200;

    private const string HtmlContentType = "text/html; charset=utf-8";
    private const string TextContentType = "text/plain; charset=utf-8";
    private const string JsonContentType = "application/json; charset=utf-8";

    private const string ContentSecurityPolicy =
        "default-src 'self'; script-src 'self' 'unsafe-inline'; style-src 'self' 'unsafe-inline'; " +
        "img-src 'self' data:; connect-src 'self'; object-src 'none'; base-uri 'none'; " +
        "form-action 'self'; frame-ancestors 'none'";

    private readonly RequestDelegate _next;
    private readonly HubbleLoginThrottle _loginThrottle = new HubbleLoginThrottle();

    private enum AuthResult
    {
        Authenticated,
        Unauthenticated,
        LockedOut
    }

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
    /// <param name="options">Opciones de Hubble</param>
    /// <param name="antiforgery">Servicio antiforgery para proteger los formularios contra CSRF</param>
    /// <param name="dataProtectionProvider">Proveedor de Data Protection para la cookie de sesión</param>
    /// <returns>Tarea asíncrona</returns>
    public async Task InvokeAsync(
        HttpContext context,
        HubbleOptions options,
        IAntiforgery antiforgery,
        IDataProtectionProvider dataProtectionProvider)
    {
        // Solo procesar solicitudes bajo la ruta base de Hubble (comparación por segmentos: "/hubble" no coincide con "/hubblefoo")
        if (!context.Request.Path.StartsWithSegments(NormalizeBasePath(options.BasePath), StringComparison.OrdinalIgnoreCase, out var remaining))
        {
            await _next(context);
            return;
        }

        var subPath = (remaining.Value ?? string.Empty).TrimEnd('/').ToLowerInvariant();
        var isApi = subPath == "/api" || subPath.StartsWith("/api/", StringComparison.Ordinal);
        var isPost = HttpMethods.IsPost(context.Request.Method);

        ApplySecurityHeaders(context.Response);

        // El filtro de IPs solo protege el dashboard y la API de Hubble, nunca el resto de la aplicación
        if (!HubbleIpAllowList.IsAllowed(context.Connection.RemoteIpAddress, options.Security.AllowedIps))
        {
            await WriteErrorAsync(context, isApi, StatusCodes.Status403Forbidden, "Access denied: IP not allowed");
            return;
        }

        if (options.RequireAuthentication)
        {
            var authResult = Authenticate(context, options, dataProtectionProvider);

            if (authResult == AuthResult.LockedOut)
            {
                await WriteErrorAsync(context, isApi, StatusCodes.Status429TooManyRequests, "Too many failed login attempts. Try again later.");
                return;
            }

            if (authResult != AuthResult.Authenticated)
            {
                if (subPath == "/login" && isPost)
                {
                    await HandleLoginAsync(context, options, antiforgery, dataProtectionProvider);
                }
                else if (isApi)
                {
                    context.Response.Headers.WWWAuthenticate = "Basic realm=\"Hubble\", charset=\"UTF-8\"";
                    await WriteErrorAsync(context, true, StatusCodes.Status401Unauthorized, "Authentication required");
                }
                else
                {
                    await ShowLoginFormAsync(context, antiforgery);
                }

                return;
            }

            // Usuario ya autenticado que vuelve a la página de login
            if (subPath == "/login")
            {
                context.Response.Redirect(GetDashboardUrl(options));
                return;
            }
        }

        try
        {
            if (isApi)
            {
                await HandleHubbleApiAsync(context, subPath.Substring("/api".Length));
                return;
            }

            switch (subPath)
            {
                case "":
                    await HandleHubbleHomeAsync(context);
                    return;

                case var detailPath when detailPath.StartsWith("/detail/", StringComparison.Ordinal):
                    await HandleHubbleDetailAsync(context, (remaining.Value ?? string.Empty).TrimEnd('/').Substring("/detail/".Length));
                    return;

                case "/config":
                    await HandleConfigPageAsync(context);
                    return;

                case "/logout":
                    HandleLogout(context, options);
                    return;
            }

            // Acciones que modifican estado: solo POST con token antiforgery válido
            Func<HttpContext, Task>? stateChangingAction = subPath switch
            {
                "/delete-all" => HandleHubbleDeleteAllAsync,
                "/run-prune" => HandleRunPruneAsync,
                "/recalculate-stats" => HandleRecalculateStatsAsync,
                "/save-config" => HandleSavePruneConfigAsync,
                "/save-capture-config" => HandleSaveCaptureConfigAsync,
                "/save-ignore-paths" => HandleSaveIgnorePathsAsync,
                _ => null
            };

            if (stateChangingAction == null)
            {
                // Para cualquier otra ruta, continuar con el siguiente middleware
                await _next(context);
                return;
            }

            if (!isPost)
            {
                context.Response.Headers.Allow = HttpMethods.Post;
                await WriteErrorAsync(context, false, StatusCodes.Status405MethodNotAllowed, "This action only accepts POST requests");
                return;
            }

            if (!await antiforgery.IsRequestValidAsync(context))
            {
                await WriteErrorAsync(context, false, StatusCodes.Status400BadRequest, "Invalid or missing anti-forgery token. Reload the page and try again.");
                return;
            }

            await stateChangingAction(context);
        }
        catch (Exception ex)
        {
            if (context.Response.HasStarted)
            {
                throw;
            }

            await WriteErrorAsync(context, isApi, StatusCodes.Status500InternalServerError, $"Internal server error: {ex.Message}");
        }
    }

    private static PathString NormalizeBasePath(string? basePath)
    {
        var normalized = "/" + (basePath ?? string.Empty).Trim().Trim('/');
        return new PathString(normalized == "/" ? "/hubble" : normalized);
    }

    private static string GetDashboardUrl(HubbleOptions options)
    {
        return options.PrefixPath.TrimEnd('/') + NormalizeBasePath(options.BasePath).Value;
    }

    private static void ApplySecurityHeaders(HttpResponse response)
    {
        var headers = response.Headers;
        headers.XContentTypeOptions = "nosniff";
        headers.XFrameOptions = "DENY";
        headers["Referrer-Policy"] = "no-referrer";
        headers.ContentSecurityPolicy = ContentSecurityPolicy;
        // Los logs contienen datos sensibles: no deben quedar en caché del navegador ni de proxies
        headers.CacheControl = "no-store, no-cache";
        headers.Pragma = "no-cache";
    }

    private static async Task WriteErrorAsync(HttpContext context, bool asJson, int statusCode, string message)
    {
        context.Response.StatusCode = statusCode;

        if (asJson)
        {
            context.Response.ContentType = JsonContentType;
            await context.Response.WriteAsync(JsonConvert.SerializeObject(new ApiResponse
            {
                Success = false,
                Message = message
            }));
        }
        else
        {
            // Texto plano para que un mensaje de error nunca se interprete como HTML
            context.Response.ContentType = TextContentType;
            await context.Response.WriteAsync(message);
        }
    }

    private static (int Page, int PageSize) ReadPagination(HttpContext context)
    {
        var page = int.TryParse(context.Request.Query["page"], out var parsedPage) ? parsedPage : 1;
        var pageSize = int.TryParse(context.Request.Query["pageSize"], out var parsedPageSize) ? parsedPageSize : 50;

        return (Math.Max(1, page), Math.Clamp(pageSize, 1, MaxPageSize));
    }

    private async Task HandleHubbleHomeAsync(HttpContext context)
    {
        // Obtener parámetros de consulta
        var method = context.Request.Query["method"].ToString();
        var url = context.Request.Query["url"].ToString();
        var statusGroup = context.Request.Query["statusGroup"].ToString();
        var logType = context.Request.Query["logType"].ToString();
        var (page, pageSize) = ReadPagination(context);

        // Obtener el controlador de Hubble
        var hubbleController = context.RequestServices.GetRequiredService<HubbleController>();
        var html = await hubbleController.GetLogsViewAsync(method, url, statusGroup, logType, page, pageSize);

        context.Response.ContentType = HtmlContentType;
        await context.Response.WriteAsync(html);
    }

    private async Task HandleHubbleDetailAsync(HttpContext context, string id)
    {
        // Obtener el controlador de Hubble
        var hubbleController = context.RequestServices.GetRequiredService<HubbleController>();
        var html = await hubbleController.GetLogDetailAsync(id);

        context.Response.ContentType = HtmlContentType;
        await context.Response.WriteAsync(html);
    }

    private async Task HandleHubbleDeleteAllAsync(HttpContext context)
    {
        // Check if delete all is allowed
        var options = context.RequestServices.GetRequiredService<HubbleOptions>();
        if (!options.AllowDeleteAll)
        {
            await WriteErrorAsync(context, false, StatusCodes.Status403Forbidden, "Delete all operation is not allowed");
            return;
        }

        // Obtener el controlador de Hubble
        var hubbleController = context.RequestServices.GetRequiredService<HubbleController>();
        var html = await hubbleController.DeleteAllLogsAsync();

        context.Response.ContentType = HtmlContentType;
        await context.Response.WriteAsync(html);
    }

    private async Task HandleHubbleApiAsync(HttpContext context, string apiPath)
    {
        var hubbleController = context.RequestServices.GetRequiredService<HubbleController>();
        var method = context.Request.Method;

        // Las peticiones que modifican estado deben ser JSON: un formulario de otro sitio no puede enviar
        // application/json sin una petición CORS preflight, lo que bloquea ataques CSRF contra la API.
        if (HttpMethods.IsPost(method) && !context.Request.HasJsonContentType())
        {
            await WriteErrorAsync(context, true, StatusCodes.Status415UnsupportedMediaType, "Content-Type must be application/json");
            return;
        }

        context.Response.ContentType = JsonContentType;

        switch (method.ToUpperInvariant())
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
                context.Response.Headers.Allow = "GET, POST, DELETE";
                await WriteErrorAsync(context, true, StatusCodes.Status405MethodNotAllowed, $"HTTP method {method} is not supported");
                break;
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
                var (page, pageSize) = ReadPagination(context);

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
            case "/recalculate-stats":
                // Estas operaciones modifican datos: ya no se aceptan por GET
                context.Response.Headers.Allow = HttpMethods.Post;
                await WriteErrorAsync(context, true, StatusCodes.Status405MethodNotAllowed, $"Use POST {apiPath} with Content-Type: application/json");
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
            case "/prune":
                // POST /api/prune - Run manual prune operation
                var manualPruneResponse = await controller.RunManualPruneApiAsync();
                await context.Response.WriteAsync(JsonConvert.SerializeObject(manualPruneResponse));
                break;

            case "/recalculate-stats":
                // POST /api/recalculate-stats - Recalculate statistics
                var recalcResponse = await controller.RecalculateStatisticsApiAsync();
                await context.Response.WriteAsync(JsonConvert.SerializeObject(recalcResponse));
                break;

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
        // Check if delete all is allowed
        var options = context.RequestServices.GetRequiredService<HubbleOptions>();
        
        switch (apiPath)
        {
            case "/logs":
                // DELETE /api/logs - Delete all logs
                if (!options.AllowDeleteAll)
                {
                    context.Response.StatusCode = 403;
                    await context.Response.WriteAsync(JsonConvert.SerializeObject(new ApiResponse
                    {
                        Success = false,
                        Message = "Delete all operation is not allowed"
                    }));
                    return;
                }
                
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

    private async Task HandleLoginAsync(
        HttpContext context,
        HubbleOptions options,
        IAntiforgery antiforgery,
        IDataProtectionProvider dataProtectionProvider)
    {
        var clientKey = GetClientKey(context);

        if (!context.Request.HasFormContentType || !await antiforgery.IsRequestValidAsync(context))
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await ShowLoginFormAsync(context, antiforgery, "La sesión del formulario expiró. Inténtalo de nuevo.");
            return;
        }

        // Procesar los datos del formulario
        var form = await context.Request.ReadFormAsync();
        var username = form["username"].ToString();
        var password = form["password"].ToString();

        if (HubbleAuthTokens.CredentialsMatch(username, password, options.Username, options.Password))
        {
            _loginThrottle.RegisterSuccess(clientKey);

            var dashboardUrl = GetDashboardUrl(options);

            // Cookie de sesión cifrada y firmada con Data Protection (no se puede falsificar ni modificar)
            context.Response.Cookies.Append(
                HubbleAuthTokens.CookieName,
                HubbleAuthTokens.CreateSessionToken(dataProtectionProvider, options.Username, options.Password),
                new CookieOptions
                {
                    HttpOnly = true,
                    Secure = context.Request.IsHttps,
                    SameSite = SameSiteMode.Strict,
                    Expires = DateTimeOffset.UtcNow.Add(HubbleAuthTokens.SessionLifetime),
                    Path = dashboardUrl // Solo válida para las rutas de Hubble
                });

            // Redirigir al usuario a la página principal de Hubble
            context.Response.Redirect(dashboardUrl);
            return;
        }

        _loginThrottle.RegisterFailure(clientKey);

        if (_loginThrottle.IsLockedOut(clientKey))
        {
            await WriteErrorAsync(context, false, StatusCodes.Status429TooManyRequests, "Too many failed login attempts. Try again later.");
            return;
        }

        // Si las credenciales son incorrectas, mostrar el formulario de inicio de sesión con error
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        await ShowLoginFormAsync(context, antiforgery, "Nombre de usuario o contraseña incorrectos");
    }

    private static void HandleLogout(HttpContext context, HubbleOptions options)
    {
        var dashboardUrl = GetDashboardUrl(options);

        // Eliminar la cookie de autenticación
        context.Response.Cookies.Delete(HubbleAuthTokens.CookieName, new CookieOptions
        {
            Path = dashboardUrl
        });

        // Redirigir al formulario de inicio de sesión
        context.Response.Redirect(dashboardUrl);
    }

    private async Task ShowLoginFormAsync(HttpContext context, IAntiforgery antiforgery, string? errorMessage = null)
    {
        var options = context.RequestServices.GetRequiredService<HubbleOptions>();
        var loginUrl = HubbleHtml.Encode(GetDashboardUrl(options) + "/login");
        var tokens = antiforgery.GetAndStoreTokens(context);
        var antiforgeryField = $"<input type='hidden' name='{HubbleHtml.Encode(tokens.FormFieldName)}' value='{HubbleHtml.Encode(tokens.RequestToken)}' />";

        // Obtener el controlador para acceder al logo y la versión
        var hubbleController = context.RequestServices.GetRequiredService<HubbleController>();
        var hubbleLogo = hubbleController.GetHubbleLogo();
        var version = HubbleHtml.Encode(hubbleController.GetVersion());

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
        
        {(errorMessage != null ? $"<div class='error-message'>{HubbleHtml.Encode(errorMessage)}</div>" : "")}

        <form class='login-form' method='post' action='{loginUrl}'>
            {antiforgeryField}
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

        context.Response.ContentType = HtmlContentType;
        await context.Response.WriteAsync(html);
    }

    private AuthResult Authenticate(HttpContext context, HubbleOptions options, IDataProtectionProvider dataProtectionProvider)
    {
        // Autenticación básica HTTP (pensada para clientes de la API)
        if (TryReadBasicCredentials(context, out var username, out var password))
        {
            var clientKey = GetClientKey(context);
            if (_loginThrottle.IsLockedOut(clientKey))
            {
                return AuthResult.LockedOut;
            }

            if (HubbleAuthTokens.CredentialsMatch(username, password, options.Username, options.Password))
            {
                _loginThrottle.RegisterSuccess(clientKey);
                return AuthResult.Authenticated;
            }

            _loginThrottle.RegisterFailure(clientKey);
            return _loginThrottle.IsLockedOut(clientKey) ? AuthResult.LockedOut : AuthResult.Unauthenticated;
        }

        // Cookie de sesión emitida por el formulario de login
        if (context.Request.Cookies.TryGetValue(HubbleAuthTokens.CookieName, out var authToken) &&
            HubbleAuthTokens.ValidateSessionToken(dataProtectionProvider, authToken, options.Username, options.Password))
        {
            return AuthResult.Authenticated;
        }

        return AuthResult.Unauthenticated;
    }

    private static bool TryReadBasicCredentials(HttpContext context, out string username, out string password)
    {
        username = string.Empty;
        password = string.Empty;

        var authHeader = context.Request.Headers.Authorization.ToString();
        if (!authHeader.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        try
        {
            var credentials = Encoding.UTF8.GetString(Convert.FromBase64String(authHeader.Substring("Basic ".Length).Trim()));
            var separatorIndex = credentials.IndexOf(':');
            if (separatorIndex < 0)
            {
                // Cabecera mal formada: se trata como un intento fallido
                return true;
            }

            username = credentials.Substring(0, separatorIndex);
            password = credentials.Substring(separatorIndex + 1);
            return true;
        }
        catch (FormatException)
        {
            return true;
        }
    }

    private static string GetClientKey(HttpContext context)
    {
        return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }

    private async Task HandleConfigPageAsync(HttpContext context)
    {
        var hubbleController = context.RequestServices.GetRequiredService<HubbleController>();
        var html = await hubbleController.GetConfigurationPageAsync();
        
        context.Response.ContentType = HtmlContentType;
        await context.Response.WriteAsync(html);
    }
    
    private async Task HandleRunPruneAsync(HttpContext context)
    {
        var hubbleController = context.RequestServices.GetRequiredService<HubbleController>();
        var html = await hubbleController.RunManualPruneAsync();
        
        context.Response.ContentType = HtmlContentType;
        await context.Response.WriteAsync(html);
    }
    
    private async Task HandleRecalculateStatsAsync(HttpContext context)
    {
        var hubbleController = context.RequestServices.GetRequiredService<HubbleController>();
        var html = await hubbleController.RecalculateStatisticsAsync();
        
        context.Response.ContentType = HtmlContentType;
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
        
        context.Response.ContentType = HtmlContentType;
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
        
        context.Response.ContentType = HtmlContentType;
        await context.Response.WriteAsync(html);
    }
    
    private async Task HandleSaveIgnorePathsAsync(HttpContext context)
    {
        // Obtener los valores del formulario
        var form = await context.Request.ReadFormAsync();
        string ignorePaths = form["ignorePaths"].ToString() ?? string.Empty;
        
        var hubbleController = context.RequestServices.GetRequiredService<HubbleController>();
        var html = await hubbleController.SaveIgnorePathsAsync(ignorePaths);
        
        context.Response.ContentType = HtmlContentType;
        await context.Response.WriteAsync(html);
    }
} 