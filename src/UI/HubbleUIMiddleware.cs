namespace Gabonet.Hubble.UI;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading.Tasks;
using System.Linq;

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
    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value?.ToLower();

        if (string.IsNullOrEmpty(path))
        {
            await _next(context);
            return;
        }

        // Verificar si la ruta es para la interfaz de usuario de Hubble
        if (path == "/hubble")
        {
            await HandleHubbleHomeAsync(context);
            return;
        }
        else if (path.StartsWith("/hubble/detail/"))
        {
            await HandleHubbleDetailAsync(context);
            return;
        }
        else if (path.StartsWith("/hubble/api/"))
        {
            await HandleHubbleApiAsync(context);
            return;
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
            var html = await hubbleController.GetLogsViewAsync(method, url, page, pageSize);
            
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
            var html = await hubbleController.GetLogDetailAsync(id);

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
} 