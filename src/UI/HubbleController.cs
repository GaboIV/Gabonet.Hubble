namespace Gabonet.Hubble.UI;

using Gabonet.Hubble.Interfaces;
using Gabonet.Hubble.Models;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

/// <summary>
/// Controlador para la interfaz de usuario de Hubble.
/// </summary>
public class HubbleController
{
    private readonly IHubbleService _hubbleService;

    /// <summary>
    /// Constructor del controlador de Hubble.
    /// </summary>
    /// <param name="hubbleService">Servicio de Hubble</param>
    public HubbleController(IHubbleService hubbleService)
    {
        _hubbleService = hubbleService;
    }

    /// <summary>
    /// Obtiene la lista de logs para mostrar en la interfaz de usuario.
    /// </summary>
    /// <param name="method">Método HTTP para filtrar</param>
    /// <param name="url">URL para filtrar</param>
    /// <param name="page">Número de página</param>
    /// <param name="pageSize">Tamaño de página</param>
    /// <returns>HTML con la lista de logs</returns>
    public async Task<string> GetLogsViewAsync(
        string? method = null,
        string? url = null,
        int page = 1,
        int pageSize = 50)
    {
        var logs = await _hubbleService.GetFilteredLogsAsync(method, url, page, pageSize);

        // Generar HTML con diseño moderno
        var html = GenerateHtmlHeader("Hubble - Logs");
        
        html += "<div class='container'>";
        html += "<div class='header'>";
        html += "<h1>Hubble</h1>";
        html += "<p>Hubble for .NET - Monitoreo de aplicaciones</p>";
        html += "</div>";

        // Formulario de filtro
        html += "<div class='filter-form'>";
        html += "<form method='get'>";
        html += $"<input type='text' name='method' placeholder='Método HTTP' value='{method}' class='input-field'>";
        html += $"<input type='text' name='url' placeholder='URL' value='{url}' class='input-field'>";
        html += "<button type='submit' class='btn primary'>Filtrar</button>";
        html += "</form>";
        html += "</div>";

        // Tabla de logs
        html += "<div class='table-container'>";
        html += "<table class='data-table'>";
        html += "<thead><tr>";
        html += "<th>Fecha/Hora</th>";
        html += "<th>Método</th>";
        html += "<th>URL</th>";
        html += "<th>Estado</th>";
        html += "<th>Duración</th>";
        html += "<th>Consultas DB</th>";
        html += "<th>Acciones</th>";
        html += "</tr></thead>";
        html += "<tbody>";

        foreach (var log in logs)
        {
            var statusClass = log.IsError || log.StatusCode >= 400 ? "error" : "success";
            var formattedTime = log.Timestamp.ToString("yyyy-MM-dd HH:mm:ss");
            
            html += $"<tr class='{statusClass}'>";
            html += $"<td>{formattedTime}</td>";
            html += $"<td>{log.Method}</td>";
            html += $"<td class='url-cell'>{log.HttpUrl}</td>";
            html += $"<td>{log.StatusCode}</td>";
            html += $"<td>{log.ExecutionTime} ms</td>";
            html += $"<td>{log.DatabaseQueries.Count}</td>";
            html += $"<td><a href='/hubble/detail/{log.Id}' class='btn small'>Ver</a></td>";
            html += "</tr>";
        }

        html += "</tbody></table>";
        html += "</div>";

        // Paginación
        var totalPages = Math.Max(1, (int)Math.Ceiling((double)logs.Count / pageSize));
        html += "<div class='pagination'>";
        
        if (page > 1)
        {
            html += $"<a href='?page={page - 1}&pageSize={pageSize}&method={method}&url={url}' class='btn secondary'>Anterior</a>";
        }
        
        html += $"<span class='page-info'>Página {page} de {totalPages}</span>";
        
        if (page < totalPages)
        {
            html += $"<a href='?page={page + 1}&pageSize={pageSize}&method={method}&url={url}' class='btn secondary'>Siguiente</a>";
        }
        
        html += "</div>";
        html += "</div>"; // Cierre del container

        html += GenerateHtmlFooter();
        
        return html;
    }

    /// <summary>
    /// Obtiene los detalles de un log específico.
    /// </summary>
    /// <param name="id">ID del log</param>
    /// <returns>HTML con los detalles del log</returns>
    public async Task<string> GetLogDetailAsync(string id)
    {
        var log = await _hubbleService.GetLogByIdAsync(id);
        
        if (log == null)
        {
            return GenerateErrorPage("Log no encontrado", "El log solicitado no existe o ha sido eliminado.");
        }
        var html = GenerateHtmlHeader("Hubble - Detalle del Log");
        
        html += "<div class='container'>";
        html += "<div class='header'>";
        html += "<h1>Hubble</h1>";
        html += "<a href='/hubble' class='btn secondary'>Volver a la lista</a>";
        html += "</div>";

        html += "<h2 class='page-title'>Detalle del Log</h2>";

        // Información general
        html += "<div class='card'>";
        html += "<h2>Información General</h2>";
        html += "<div class='info-grid'>";
        html += $"<div class='info-item'><span>Fecha/Hora:</span> {log.Timestamp.ToString("yyyy-MM-dd HH:mm:ss")}</div>";
        html += $"<div class='info-item'><span>Método:</span> {log.Method}</div>";
        html += $"<div class='info-item'><span>URL:</span> {log.HttpUrl}</div>";
        html += $"<div class='info-item'><span>Controlador:</span> {log.ControllerName}</div>";
        html += $"<div class='info-item'><span>Acción:</span> {log.ActionName}</div>";
        html += $"<div class='info-item'><span>Estado:</span> <span class='{(log.IsError || log.StatusCode >= 400 ? "error-text" : "success-text")}'>{log.StatusCode}</span></div>";
        html += $"<div class='info-item'><span>Duración:</span> {log.ExecutionTime} ms</div>";
        html += $"<div class='info-item'><span>IP Cliente:</span> {log.IpAddress}</div>";
        html += $"<div class='info-item'><span>Servicio:</span> {log.ServiceName}</div>";
        html += "</div>";
        html += "</div>";

        // Si hay error
        if (log.IsError || !string.IsNullOrEmpty(log.ErrorMessage))
        {
            html += "<div class='card error-card'>";
            html += "<h2>Error</h2>";
            html += $"<div class='code-block'>{log.ErrorMessage}</div>";
            
            if (!string.IsNullOrEmpty(log.StackTrace))
            {
                html += "<h3>Stack Trace</h3>";
                html += $"<div class='code-block'>{log.StackTrace}</div>";
            }
            
            html += "</div>";
        }

        // Cabeceras de la solicitud
        if (!string.IsNullOrEmpty(log.RequestHeaders))
        {
            html += "<div class='card'>";
            html += "<h2>Cabeceras de la Solicitud</h2>";
            html += $"<div class='code-block'>{FormatJson(log.RequestHeaders)}</div>";
            html += "</div>";
        }

        // Datos de la solicitud
        if (!string.IsNullOrEmpty(log.RequestData))
        {
            html += "<div class='card'>";
            html += "<h2>Datos de la Solicitud</h2>";
            html += $"<div class='code-block'>{FormatJson(log.RequestData)}</div>";
            html += "</div>";
        }

        // Datos de la respuesta
        if (!string.IsNullOrEmpty(log.ResponseData))
        {
            html += "<div class='card'>";
            html += "<h2>Datos de la Respuesta</h2>";
            html += $"<div class='code-block'>{FormatJson(log.ResponseData)}</div>";
            html += "</div>";
        }

        // Consultas a bases de datos
        if (log.DatabaseQueries.Count > 0)
        {
            html += "<div class='card'>";
            html += "<h2>Consultas a Bases de Datos</h2>";
            
            foreach (var query in log.DatabaseQueries)
            {
                html += "<div class='query-item'>";
                html += $"<div class='query-header'>";
                html += $"<span class='query-type'>{query.OperationType ?? "QUERY"}</span>";
                html += $"<span class='query-db'>{query.DatabaseType} - {query.DatabaseName}</span>";
                html += $"<span class='query-time'>{query.ExecutionTime} ms</span>";
                html += "</div>";
                
                html += $"<div class='code-block sql'>{query.Query}</div>";
                
                if (!string.IsNullOrEmpty(query.Parameters))
                {
                    html += "<h4>Parámetros</h4>";
                    html += $"<div class='code-block'>{FormatJson(query.Parameters)}</div>";
                }
                
                if (!string.IsNullOrEmpty(query.TableName))
                {
                    html += $"<div class='query-meta'>Tabla: {query.TableName}</div>";
                }
                
                if (!string.IsNullOrEmpty(query.CallerMethod))
                {
                    html += $"<div class='query-meta'>Método: {query.CallerMethod}</div>";
                }
                
                html += "</div>";
            }
            
            html += "</div>";
        }

        html += "</div>"; // Cierre del container
        html += GenerateHtmlFooter();
        
        return html;
    }

    /// <summary>
    /// Genera una página de error.
    /// </summary>
    /// <param name="title">Título del error</param>
    /// <param name="message">Mensaje de error</param>
    /// <returns>HTML con la página de error</returns>
    private string GenerateErrorPage(string title, string message)
    {
        var logoPath = "/img/gabonet.hubble.png";
        var html = GenerateHtmlHeader($"Hubble - {title}");
        
        html += "<div class='container'>";
        html += "<div class='header'>";
        html += $"<div class='logo-container'><img src='{logoPath}' alt='Hubble Logo' class='logo'></div>";
        html += "<a href='/hubble' class='btn secondary'>Volver a la lista</a>";
        html += "</div>";
        
        html += $"<h2 class='page-title'>{title}</h2>";
        
        html += "<div class='card error-card'>";
        html += $"<p>{message}</p>";
        html += "</div>";
        
        html += "</div>";
        html += GenerateHtmlFooter();
        
        return html;
    }

    /// <summary>
    /// Genera el encabezado HTML con estilos modernos.
    /// </summary>
    /// <param name="title">Título de la página</param>
    /// <returns>HTML del encabezado</returns>
    private string GenerateHtmlHeader(string title)
    {
        return $@"<!DOCTYPE html>
<html lang='es'>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <title>{title}</title>
    <style>
        :root {{
            --primary-color: #6200ee;
            --primary-light: #bb86fc;
            --secondary-color: #03dac6;
            --background: #121212;
            --surface: #1e1e1e;
            --error: #cf6679;
            --success: #4caf50;
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
        }}
        
        .container {{
            max-width: 1200px;
            margin: 0 auto;
            padding: 20px;
        }}
        
        .header {{
            margin-bottom: 30px;
            display: flex;
            justify-content: space-between;
            align-items: center;
        }}
        
        .logo-container {{
            display: flex;
            align-items: center;
        }}
        
        .logo {{
            max-height: 60px;
            max-width: 100%;
        }}
        
        h1 {{
            color: var(--primary-light);
            margin-bottom: 10px;
        }}
        
        h2 {{
            color: var(--primary-light);
            margin-bottom: 15px;
        }}
        
        h3 {{
            color: var(--text-secondary);
            margin: 15px 0 10px;
        }}
        
        .filter-form {{
            background-color: var(--surface);
            padding: 20px;
            border-radius: 8px;
            margin-bottom: 20px;
            display: flex;
            justify-content: center;
        }}
        
        .filter-form form {{
            display: flex;
            gap: 10px;
            width: 100%;
            max-width: 800px;
        }}
        
        .input-field {{
            background-color: rgba(255, 255, 255, 0.1);
            border: 1px solid var(--border-color);
            color: var(--text-primary);
            padding: 10px 15px;
            border-radius: 4px;
            flex-grow: 1;
        }}
        
        .btn {{
            background-color: var(--primary-color);
            color: white;
            border: none;
            padding: 10px 20px;
            border-radius: 4px;
            cursor: pointer;
            text-decoration: none;
            display: inline-block;
            font-weight: 500;
            transition: background-color 0.3s;
        }}
        
        .btn:hover {{
            background-color: var(--primary-light);
        }}
        
        .btn.secondary {{
            background-color: transparent;
            border: 1px solid var(--primary-light);
            color: var(--primary-light);
        }}
        
        .btn.secondary:hover {{
            background-color: rgba(187, 134, 252, 0.1);
        }}
        
        .btn.small {{
            padding: 5px 10px;
            font-size: 0.9em;
        }}
        
        .table-container {{
            overflow-x: auto;
            margin-bottom: 20px;
        }}
        
        .data-table {{
            width: 100%;
            border-collapse: collapse;
            background-color: var(--surface);
            border-radius: 8px;
            overflow: hidden;
        }}
        
        .data-table th {{
            background-color: rgba(187, 134, 252, 0.1);
            color: var(--primary-light);
            text-align: left;
            padding: 15px;
            font-weight: 500;
        }}
        
        .data-table td {{
            padding: 12px 15px;
            border-top: 1px solid var(--border-color);
        }}
        
        .data-table tr:hover {{
            background-color: rgba(255, 255, 255, 0.05);
        }}
        
        .data-table tr.error {{
            background-color: rgba(207, 102, 121, 0.1);
        }}
        
        .data-table tr.error:hover {{
            background-color: rgba(207, 102, 121, 0.2);
        }}
        
        .url-cell {{
            max-width: 300px;
            overflow: hidden;
            text-overflow: ellipsis;
            white-space: nowrap;
        }}
        
        .pagination {{
            display: flex;
            justify-content: center;
            align-items: center;
            gap: 15px;
            margin-top: 20px;
        }}
        
        .page-info {{
            color: var(--text-secondary);
        }}
        
        .card {{
            background-color: var(--surface);
            border-radius: 8px;
            padding: 20px;
            margin-bottom: 20px;
        }}
        
        .error-card {{
            background-color: rgba(207, 102, 121, 0.1);
            border-left: 4px solid var(--error);
        }}
        
        .info-grid {{
            display: grid;
            grid-template-columns: repeat(auto-fill, minmax(300px, 1fr));
            gap: 15px;
        }}
        
        .info-item {{
            padding: 10px;
            background-color: rgba(255, 255, 255, 0.05);
            border-radius: 4px;
        }}
        
        .info-item span:first-child {{
            color: var(--text-secondary);
            margin-right: 5px;
        }}
        
        .error-text {{
            color: var(--error);
        }}
        
        .success-text {{
            color: var(--success);
        }}
        
        .code-block {{
            background-color: rgba(0, 0, 0, 0.3);
            padding: 15px;
            border-radius: 4px;
            overflow-x: auto;
            white-space: pre-wrap;
            font-family: 'Consolas', 'Monaco', monospace;
            font-size: 0.9em;
            margin-bottom: 15px;
        }}
        
        .code-block.sql {{
            color: #9cdcfe;
        }}
        
        .query-item {{
            background-color: rgba(0, 0, 0, 0.2);
            border-radius: 4px;
            padding: 15px;
            margin-bottom: 15px;
        }}
        
        .query-header {{
            display: flex;
            justify-content: space-between;
            margin-bottom: 10px;
            padding-bottom: 10px;
            border-bottom: 1px solid var(--border-color);
        }}
        
        .query-type {{
            color: var(--primary-light);
            font-weight: bold;
        }}
        
        .query-db {{
            color: var(--text-secondary);
        }}
        
        .query-time {{
            color: var(--secondary-color);
        }}
        
        .query-meta {{
            color: var(--text-secondary);
            font-size: 0.9em;
            margin-top: 5px;
        }}
        
        .page-title {{
            color: var(--primary-light);
            margin-bottom: 20px;
            text-align: center;
        }}
        
        @media (max-width: 768px) {{
            .info-grid {{
                grid-template-columns: 1fr;
            }}
            
            .filter-form form {{
                flex-direction: column;
            }}
        }}
    </style>
</head>
<body>
";
    }

    /// <summary>
    /// Genera el pie de página HTML.
    /// </summary>
    /// <returns>HTML del pie de página</returns>
    private string GenerateHtmlFooter()
    {
        return @"
    <footer style='text-align: center; margin-top: 40px; padding: 20px; color: var(--text-secondary);'>
        <p>Hubble for .NET - Monitoreo de aplicaciones</p>
    </footer>
</body>
</html>";
    }

    /// <summary>
    /// Formatea una cadena JSON para mostrarla con formato.
    /// </summary>
    /// <param name="json">Cadena JSON</param>
    /// <returns>JSON formateado</returns>
    private string FormatJson(string json)
    {
        if (string.IsNullOrEmpty(json))
        {
            return string.Empty;
        }

        try
        {
            var obj = JsonConvert.DeserializeObject(json);
            return JsonConvert.SerializeObject(obj, Formatting.Indented);
        }
        catch
        {
            return json;
        }
    }
} 