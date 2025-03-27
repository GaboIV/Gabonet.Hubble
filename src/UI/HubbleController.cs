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
    /// <param name="statusGroup">Grupo de estado HTTP para filtrar (200, 400, 500)</param>
    /// <param name="logType">Tipo de log para filtrar (ApplicationLogger, HTTP)</param>
    /// <param name="page">Número de página</param>
    /// <param name="pageSize">Tamaño de página</param>
    /// <returns>HTML con la lista de logs</returns>
    public async Task<string> GetLogsViewAsync(
        string? method = null,
        string? url = null,
        string? statusGroup = null,
        string? logType = null,
        int page = 1,
        int pageSize = 50)
    {
        // Por defecto, excluir logs relacionados a menos que explícitamente se soliciten logs de tipo ApplicationLogger
        bool excludeRelatedLogs = string.IsNullOrEmpty(logType) || logType != "ApplicationLogger";
        
        // Obtener el conteo total de logs antes de aplicar la paginación
        var totalCount = await _hubbleService.GetTotalLogsCountAsync(method, url, excludeRelatedLogs);
        
        // Obtener los logs para la página actual
        var logs = await _hubbleService.GetFilteredLogsWithRelatedAsync(method, url, excludeRelatedLogs, page, pageSize);

        // Filtrar por grupo de códigos de estado si se especifica
        if (!string.IsNullOrEmpty(statusGroup))
        {
            int statusBase = int.Parse(statusGroup);
            logs = logs.Where(log => log.StatusCode >= statusBase && log.StatusCode < statusBase + 100).ToList();
        }

        // Filtrar por tipo de log si se especifica explícitamente
        if (!string.IsNullOrEmpty(logType))
        {
            if (logType == "ApplicationLogger")
            {
                logs = logs.Where(log => log.ControllerName == "ApplicationLogger").ToList();
            }
            else if (logType == "HTTP")
            {
                logs = logs.Where(log => log.ControllerName != "ApplicationLogger").ToList();
            }
        }

        // Generar HTML con diseño moderno
        var html = GenerateHtmlHeader("Hubble - Logs", true);
        
        html += "<div class='container'>";
        html += "<div class='header'>";
        html += "<div class='header-left'>";
        html += GetHubbleLogo();
        html += "<p>Hubble for .NET - Monitoreo de aplicaciones</p>";
        html += "</div>";
        
        // Botón de logout si la autenticación está habilitada
        html += "<div class='header-right'>";
        html += "<a href='/hubble/logout' class='btn secondary'>Cerrar sesión</a>";
        html += "</div>";
        html += "</div>";

        // Formulario de filtro
        html += "<div class='filter-form'>";
        html += "<form method='get'>";
        
        // Selector de método HTTP
        html += "<div class='select-wrapper'>";
        html += "<select name='method' class='select-field'>";
        html += "<option value=''>Todos los métodos</option>";
        
        var httpMethods = new[] { "GET", "POST", "PUT", "DELETE", "PATCH", "OPTIONS", "HEAD" };
        foreach (var httpMethod in httpMethods)
        {
            var selected = method == httpMethod ? "selected" : "";
            html += $"<option value='{httpMethod}' {selected}>{httpMethod}</option>";
        }
        
        html += "</select>";
        html += "</div>";
        
        // Selector de tipo de log
        html += "<div class='select-wrapper'>";
        html += "<select name='logType' class='select-field'>";
        html += "<option value=''>Todos los tipos</option>";
        html += "<option value='ApplicationLogger'>Logs (ILogger)</option>";
        html += "<option value='HTTP'>HTTP</option>";
        html += "</select>";
        html += "</div>";
        
        // Filtro de URL
        html += $"<input type='text' name='url' placeholder='URL' value='{url}' class='input-field'>";
        
        // Selector de grupos de estado
        html += "<div class='select-wrapper'>";
        html += "<select name='statusGroup' class='select-field'>";
        html += "<option value=''>Todos los estados</option>";
        
        var statusGroups = new Dictionary<string, string>
        {
            { "200", "2xx - Éxito" },
            { "300", "3xx - Redirección" },
            { "400", "4xx - Error cliente" },
            { "500", "5xx - Error servidor" }
        };
        
        foreach (var group in statusGroups)
        {
            var selected = statusGroup == group.Key ? "selected" : "";
            html += $"<option value='{group.Key}' {selected}>{group.Value}</option>";
        }
        
        html += "</select>";
        html += "</div>";
        
        html += "<button type='submit' class='btn primary'>Filtrar</button>";
        html += "</form>";
        html += "<button onclick=\"if(confirm('¿Está seguro que desea eliminar todos los logs? Esta acción no se puede deshacer.')) { window.location.href='/hubble/delete-all'; }\" class='btn danger'>Eliminar todos</button>";
        html += "</div>";

        // Tabla de logs
        html += "<div class='table-container'>";
        html += "<table class='data-table'>";
        html += "<thead><tr>";
        html += "<th>Fecha/Hora</th>";
        html += "<th>Tipo</th>";
        html += "<th>Método</th>";
        html += "<th>URL/Categoría</th>";
        html += "<th>Estado</th>";
        html += "<th>Duración</th>";
        html += "<th>Acciones</th>";
        html += "</tr></thead>";
        html += "<tbody>";

        foreach (var log in logs)
        {
            var statusClass = log.IsError || log.StatusCode >= 400 ? "error" : "success";
            var formattedTime = log.Timestamp.ToString("yyyy-MM-dd HH:mm:ss");
            var isILoggerEntry = log.ControllerName == "ApplicationLogger";
            
            html += $"<tr class='{statusClass}'>";
            html += $"<td>{formattedTime}</td>";
            
            // Mostrar tipo de log
            if (isILoggerEntry)
            {
                // Para logs de ILogger, mostrar el nivel de log (ActionName contiene el nivel)
                html += $"<td><span class='log-level {log.ActionName.ToLower()}'>{log.ActionName}</span></td>";
            }
            else
            {
                // Para logs HTTP normales
                html += $"<td>HTTP</td>";
            }
            
            html += $"<td>{log.Method}</td>";
            
            // Mostrar URL para HTTP o categoría para logs
            if (isILoggerEntry)
            {
                // RequestData contiene la categoría del logger
                html += $"<td class='url-cell'>{log.RequestData}</td>";
            }
            else
            {
                html += $"<td class='url-cell'>{log.HttpUrl}</td>";
            }
            
            html += $"<td>{log.StatusCode}</td>";
            html += $"<td>{log.ExecutionTime} ms</td>";
            html += $"<td><a href='/hubble/detail/{log.Id}' class='btn small'>Ver</a></td>";
            html += "</tr>";
        }

        html += "</tbody></table>";
        html += "</div>";

        // Paginación
        var totalPages = Math.Max(1, (int)Math.Ceiling((double)totalCount / pageSize));
        
        // Mostrar información de total de elementos y paginación
        html += "<div class='pagination-info'>";
        html += $"<span>Mostrando {logs.Count} de {totalCount} registros</span>";
        html += $"<span>Página {page} de {totalPages}</span>";
        html += "</div>";
        
        html += "<div class='pagination'>";
        
        // Botón de primera página
        if (page > 1)
        {
            html += $"<a href='?page=1&pageSize={pageSize}&method={method}&url={url}&statusGroup={statusGroup}&logType={logType}' class='btn pagination-btn' title='Primera página'><span class='pagination-icon'>«</span></a>";
        }
        else
        {
            html += $"<span class='btn pagination-btn disabled' title='Primera página'><span class='pagination-icon'>«</span></span>";
        }
        
        // Botón página anterior
        if (page > 1)
        {
            html += $"<a href='?page={page - 1}&pageSize={pageSize}&method={method}&url={url}&statusGroup={statusGroup}&logType={logType}' class='btn pagination-btn' title='Página anterior'><span class='pagination-icon'>‹</span></a>";
        }
        else
        {
            html += $"<span class='btn pagination-btn disabled' title='Página anterior'><span class='pagination-icon'>‹</span></span>";
        }
        
        // Información de página con navegador de páginas
        html += "<div class='page-navigator'>";
        
        // Lógica mejorada para mostrar las páginas
        int pagesToShow = 5;
        int halfPagesToShow = pagesToShow / 2;
        
        int startPage = Math.Max(1, page - halfPagesToShow);
        int endPage = Math.Min(totalPages, startPage + pagesToShow - 1);
        
        // Ajustar startPage si estamos cerca del final
        if (endPage == totalPages)
        {
            startPage = Math.Max(1, endPage - pagesToShow + 1);
        }
        
        // Mostrar elipsis al inicio si es necesario
        if (startPage > 1)
        {
            if (startPage > 2)
            {
                html += $"<a href='?page=1&pageSize={pageSize}&method={method}&url={url}&statusGroup={statusGroup}&logType={logType}' class='page-number'>1</a>";
                html += "<span class='page-ellipsis'>...</span>";
            }
            else if (startPage == 2)
            {
                html += $"<a href='?page=1&pageSize={pageSize}&method={method}&url={url}&statusGroup={statusGroup}&logType={logType}' class='page-number'>1</a>";
            }
        }
        
        // Mostrar páginas numeradas
        for (int i = startPage; i <= endPage; i++)
        {
            if (i == page)
            {
                html += $"<span class='page-number current'>{i}</span>";
            }
            else
            {
                html += $"<a href='?page={i}&pageSize={pageSize}&method={method}&url={url}&statusGroup={statusGroup}&logType={logType}' class='page-number'>{i}</a>";
            }
        }
        
        // Mostrar elipsis al final si es necesario
        if (endPage < totalPages)
        {
            if (endPage < totalPages - 1)
            {
                html += "<span class='page-ellipsis'>...</span>";
                html += $"<a href='?page={totalPages}&pageSize={pageSize}&method={method}&url={url}&statusGroup={statusGroup}&logType={logType}' class='page-number'>{totalPages}</a>";
            }
            else if (endPage == totalPages - 1)
            {
                html += $"<a href='?page={totalPages}&pageSize={pageSize}&method={method}&url={url}&statusGroup={statusGroup}&logType={logType}' class='page-number'>{totalPages}</a>";
            }
        }
        
        html += "</div>";
        
        // Botón página siguiente
        if (page < totalPages)
        {
            html += $"<a href='?page={page + 1}&pageSize={pageSize}&method={method}&url={url}&statusGroup={statusGroup}&logType={logType}' class='btn pagination-btn' title='Página siguiente'><span class='pagination-icon'>›</span></a>";
        }
        else
        {
            html += $"<span class='btn pagination-btn disabled' title='Página siguiente'><span class='pagination-icon'>›</span></span>";
        }
        
        // Botón de última página
        if (page < totalPages)
        {
            html += $"<a href='?page={totalPages}&pageSize={pageSize}&method={method}&url={url}&statusGroup={statusGroup}&logType={logType}' class='btn pagination-btn' title='Última página'><span class='pagination-icon'>»</span></a>";
        }
        else
        {
            html += $"<span class='btn pagination-btn disabled' title='Última página'><span class='pagination-icon'>»</span></span>";
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
        var html = GenerateHtmlHeader("Hubble - Detalle del Log", true);
        
        html += "<div class='container'>";
        html += "<div class='header'>";
        html += "<div class='header-left'>";
        html += GetHubbleLogo();
        html += "<a href='/hubble' class='btn secondary'>Volver a la lista</a>";
        html += "</div>";
        
        // Botón de logout si la autenticación está habilitada
        html += "<div class='header-right'>";
        html += "<a href='/hubble/logout' class='btn secondary'>Cerrar sesión</a>";
        html += "</div>";
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
        
        // Logs relacionados (logs de ILogger asociados a esta solicitud)
        var relatedLogs = await _hubbleService.GetRelatedLogsAsync(log.Id);
        if (relatedLogs.Count > 0)
        {
            html += "<div class='card'>";
            html += "<h2>Loggers</h2>";
            
            // Agrupar logs por categoría (RequestData contiene el nombre de la categoría)
            var logsByCategory = relatedLogs
                .GroupBy(l => l.RequestData)
                .OrderBy(g => g.Key)
                .ToList();
                
            foreach (var categoryGroup in logsByCategory)
            {
                html += $"<div class='category-group'>";
                html += $"<h3 class='category-title'>{categoryGroup.Key}</h3>";
                
                // Ordenar logs por timestamp dentro de cada categoría
                var orderedLogs = categoryGroup.OrderBy(l => l.Timestamp).ToList();
                
                foreach (var relatedLog in orderedLogs)
                {
                    var logClass = relatedLog.ActionName.ToLower();
                    html += "<div class='related-log-item'>";
                    html += $"<div class='related-log-header'>";
                    html += $"<span class='log-type'><span class='log-level {logClass}'>{relatedLog.ActionName}</span></span>";
                    html += $"<span class='log-time'>{relatedLog.Timestamp.ToString("HH:mm:ss.fff")}</span>";
                    html += "</div>";
                    
                    html += $"<div class='code-block log'>{relatedLog.ResponseData}</div>";
                    
                    if (!string.IsNullOrEmpty(relatedLog.ErrorMessage))
                    {
                        html += "<h4>Error</h4>";
                        html += $"<div class='code-block error'>{relatedLog.ErrorMessage}</div>";
                        
                        if (!string.IsNullOrEmpty(relatedLog.StackTrace))
                        {
                            html += "<h4>Stack Trace</h4>";
                            html += $"<div class='code-block'>{relatedLog.StackTrace}</div>";
                        }
                    }
                    
                    html += "</div>";
                }
                
                html += "</div>"; // Cierre de category-group
            }
            
            html += "</div>";
        }

        html += "</div>"; // Cierre del container
        html += GenerateHtmlFooter();
        
        return html;
    }

    /// <summary>
    /// Elimina todos los logs y redirige a la página principal.
    /// </summary>
    /// <returns>HTML con mensaje de redirección</returns>
    public async Task<string> DeleteAllLogsAsync()
    {
        await _hubbleService.DeleteAllLogsAsync();
        
        var html = GenerateHtmlHeader("Hubble - Logs eliminados", true);
        
        html += "<div class='container'>";
        html += "<div class='header'>";
        html += "<div class='header-left'>";
        html += GetHubbleLogo();
        html += "</div>";
        
        // Botón de logout si la autenticación está habilitada
        html += "<div class='header-right'>";
        html += "<a href='/hubble/logout' class='btn secondary'>Cerrar sesión</a>";
        html += "</div>";
        html += "</div>";

        html += "<div class='card success-card'>";
        html += "<h2>Operación exitosa</h2>";
        html += "<p>Todos los logs han sido eliminados correctamente.</p><br>";
        html += "<a href='/hubble' class='btn primary'>Volver a la lista</a>";
        html += "</div>";
        
        html += "</div>";
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
        var html = GenerateHtmlHeader($"Hubble - {title}", true);
        
        html += "<div class='container'>";
        html += "<div class='header'>";
        html += "<div class='header-left'>";
        html += GetHubbleLogo();
        html += "<a href='/hubble' class='btn secondary'>Volver a la lista</a>";
        html += "</div>";
        
        // Botón de logout si la autenticación está habilitada
        html += "<div class='header-right'>";
        html += "<a href='/hubble/logout' class='btn secondary'>Cerrar sesión</a>";
        html += "</div>";
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
    /// Genera el logo de Hubble en formato SVG.
    /// </summary>
    /// <returns>HTML con el logo SVG</returns>
    private string GetHubbleLogo()
    {
        return @"<div class='logo-container'>
            <a href='/hubble' title='Recargar Hubble Dashboard' class='logo-link'>
                <svg width='200' height='60' viewBox='0 0 200 60' fill='none' xmlns='http://www.w3.org/2000/svg' class='hubble-logo'>
                    <!-- Fondo circular principal -->
                    <circle cx='30' cy='30' r='28' fill='#121212' stroke='#6200EE' stroke-width='3'></circle>
                    
                    <!-- Anillos del telescopio -->
                    <circle cx='30' cy='30' r='22' fill='none' stroke='#03DAC6' stroke-width='2' stroke-dasharray='3 3'></circle>
                    <circle cx='30' cy='30' r='16' fill='none' stroke='rgba(187, 134, 252, 0.4)' stroke-width='2'></circle>
                    
                    <!-- Estrellas -->
                    <circle cx='20' cy='22' r='3' fill='#BB86FC'>
                        <animate attributeName='opacity' values='0.7;1;0.7' dur='3s' repeatCount='indefinite'></animate>
                    </circle>
                    <circle cx='40' cy='32' r='4' fill='#03DAC6'>
                        <animate attributeName='opacity' values='0.8;1;0.8' dur='2.5s' repeatCount='indefinite'></animate>
                    </circle>
                    <circle cx='30' cy='16' r='2' fill='#FFFFFF'>
                        <animate attributeName='opacity' values='0.6;1;0.6' dur='2s' repeatCount='indefinite'></animate>
                    </circle>
                    <circle cx='35' cy='42' r='1.5' fill='#BB86FC'>
                        <animate attributeName='opacity' values='0.7;1;0.7' dur='2.7s' repeatCount='indefinite'></animate>
                    </circle>
                    <circle cx='15' cy='38' r='1.8' fill='#FFFFFF'>
                        <animate attributeName='opacity' values='0.6;0.9;0.6' dur='3.2s' repeatCount='indefinite'></animate>
                    </circle>
                    
                    <!-- Texto HUBBLE más grande -->
                    <text x='70' y='40' font-family='Segoe UI, sans-serif' font-size='28' fill='white' font-weight='bold'>HUBBLE</text>
                    
                    <!-- Línea decorativa -->
                    <path d='M130 30C130 22 140 18 150 18' stroke='#03DAC6' stroke-width='2' stroke-dasharray='2 2'></path>
                    
                    <!-- Texto 'by Gabonet' -->
                    <text x='100' y='55' font-family='Segoe UI, sans-serif' font-size='10' fill='#03DAC6' font-weight='500'>by Gabonet</text>
                </svg>
            </a>
        </div>";
    }

    /// <summary>
    /// Genera el encabezado HTML con estilos modernos.
    /// </summary>
    /// <param name="title">Título de la página</param>
    /// <param name="showLogout">Indica si se debe mostrar el botón de logout</param>
    /// <returns>HTML del encabezado</returns>
    private string GenerateHtmlHeader(string title, bool showLogout = false)
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
            --danger: #f44336;
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
        
        .header-left {{
            display: flex;
            align-items: center;
        }}
        
        .header-right {{
            display: flex;
            align-items: center;
            gap: 10px;
        }}
        
        .logo-container {{
            display: flex;
            align-items: center;
            margin-right: 15px;
        }}
        
        .logo-link {{
            display: block;
            transition: transform 0.3s ease;
        }}
        
        .logo-link:hover {{
            transform: scale(1.05);
        }}
        
        .hubble-logo {{
            max-height: 40px;
            max-width: 120px;
        }}
        
        /* Estilos especiales para las estrellas del logo */
        .hubble-logo circle {{
            transition: fill 0.3s ease, r 0.3s ease;
        }}
        
        .logo-link:hover .hubble-logo circle[fill='#BB86FC'],
        .logo-link:hover .hubble-logo circle[fill='#03DAC6'],
        .logo-link:hover .hubble-logo circle[fill='#FFFFFF'] {{
            filter: brightness(1.2);
        }}
        
        .logo-link:hover .hubble-logo text {{
            fill: var(--primary-light);
            transition: fill 0.3s ease;
        }}
        
        /* Estilos para el texto del creador */
        .creator-text {{
            opacity: 0.8;
            transition: opacity 0.3s ease;
        }}
        
        .logo-link:hover .creator-text {{
            opacity: 1;
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
            justify-content: space-between;
            align-items: center;
            gap: 15px;
            flex-wrap: wrap;
        }}
        
        .filter-form form {{
            display: flex;
            gap: 10px;
            flex: 1;
            flex-wrap: wrap;
        }}
        
        .input-field {{
            background-color: rgba(255, 255, 255, 0.1);
            border: 1px solid var(--border-color);
            color: var(--text-primary);
            padding: 10px 15px;
            border-radius: 4px;
            flex-grow: 1;
            min-width: 150px;
        }}
        
        /* Solución para todos los navegadores */
        .select-wrapper {{
            position: relative;
            min-width: 150px;
            flex-grow: 1;
        }}
        
        .select-field {{
            -webkit-appearance: none;
            -moz-appearance: none;
            appearance: none;
            background-color: var(--surface) !important;
            border: 1px solid var(--border-color);
            color: var(--text-primary) !important;
            padding: 10px 15px;
            border-radius: 4px;
            width: 100%;
            cursor: pointer;
        }}
        
        /* Forzar tema oscuro en todas las opciones */
        select, option, select.select-field, option.select-option {{
            background-color: var(--surface) !important;
            color: var(--text-primary) !important;
        }}
        
        /* Estilos globales para los select nativos */
        select {{
            background-color: var(--surface) !important;
            color: var(--text-primary) !important;
        }}
        
        select option {{
            background-color: var(--surface) !important;
            color: var(--text-primary) !important;
            padding: 8px !important;
        }}
        
        .select-wrapper::after {{
            content: '';
            position: absolute;
            top: 50%;
            right: 15px;
            transform: translateY(-50%);
            width: 0;
            height: 0;
            border-left: 5px solid transparent;
            border-right: 5px solid transparent;
            border-top: 5px solid var(--text-primary);
            pointer-events: none;
        }}
        
        .trash-icon {{
            display: inline-block;
            margin-right: 5px;
            font-style: normal;
        }}
        
        .filter-icon {{
            display: inline-block;
            margin-right: 5px;
            font-style: normal;
        }}
        
        .eye-icon {{
            display: inline-block;
            margin-right: 3px;
            font-style: normal;
        }}
        
        .back-icon {{
            display: inline-block;
            margin-right: 5px;
            font-style: normal;
        }}
        
        .icon {{
            display: inline-block;
            margin-right: 5px;
            font-style: normal;
            vertical-align: middle;
        }}
        
        .table-actions {{
            display: flex;
            justify-content: flex-end;
            margin-bottom: 20px;
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
        
        .btn.danger {{
            background-color: var(--danger);
            color: white;
        }}
        
        .btn.danger:hover {{
            background-color: #d32f2f;
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
        
        .pagination-info {{
            display: flex;
            justify-content: space-between;
            align-items: center;
            background-color: var(--surface);
            padding: 10px 15px;
            border-radius: 4px;
            margin-bottom: 15px;
            color: var(--text-secondary);
            font-size: 0.9em;
        }}
        
        .page-info {{
            color: var(--text-secondary);
        }}
        
        .pagination-btn {{
            display: inline-flex;
            justify-content: center;
            align-items: center;
            min-width: 40px;
            height: 40px;
            padding: 0 10px;
            border-radius: 4px;
            background-color: rgba(255, 255, 255, 0.05);
            color: var(--primary-light);
            text-decoration: none;
            transition: all 0.2s ease;
        }}
        
        .pagination-btn:hover {{
            background-color: rgba(255, 255, 255, 0.1);
        }}
        
        .pagination-btn.disabled {{
            opacity: 0.5;
            cursor: not-allowed;
            pointer-events: none;
        }}
        
        .pagination-icon {{
            font-size: 1.5em;
            line-height: 1;
        }}
        
        .page-navigator {{
            display: flex;
            align-items: center;
            gap: 5px;
        }}
        
        .page-number {{
            display: inline-flex;
            justify-content: center;
            align-items: center;
            min-width: 40px;
            height: 40px;
            border-radius: 4px;
            text-decoration: none;
            color: var(--text-primary);
            background-color: rgba(255, 255, 255, 0.05);
            transition: all 0.2s ease;
        }}
        
        .page-number:hover {{
            background-color: rgba(255, 255, 255, 0.1);
        }}
        
        .page-number.current {{
            background-color: var(--primary-color);
            color: white;
            font-weight: bold;
        }}
        
        .page-ellipsis {{
            color: var(--text-secondary);
            padding: 0 5px;
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
        
        .success-card {{
            background-color: rgba(76, 175, 80, 0.1);
            border-left: 4px solid var(--success);
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
        
        /* Estilos para los niveles de log de ILogger */
        .log-level {{
            display: inline-block;
            padding: 3px 8px;
            border-radius: 4px;
            font-size: 0.85em;
            font-weight: 500;
        }}
        
        .log-level.information {{
            background-color: rgba(3, 218, 198, 0.2);
            color: var(--secondary-color);
        }}
        
        .log-level.warning {{
            background-color: rgba(255, 193, 7, 0.2);
            color: #ffc107;
        }}
        
        .log-level.error, .log-level.critical {{
            background-color: rgba(207, 102, 121, 0.2);
            color: var(--error);
        }}
        
        .log-level.debug, .log-level.trace {{
            background-color: rgba(255, 255, 255, 0.1);
            color: var(--text-secondary);
        }}
        
        /* Estilos para los logs relacionados */
        .related-log-item {{
            background-color: rgba(0, 0, 0, 0.2);
            border-radius: 4px;
            padding: 15px;
            margin-bottom: 15px;
        }}
        
        .related-log-header {{
            display: flex;
            justify-content: space-between;
            margin-bottom: 10px;
            padding-bottom: 10px;
            border-bottom: 1px solid var(--border-color);
        }}
        
        .log-type {{
            font-weight: bold;
        }}
        
        .log-category {{
            color: var(--text-secondary);
            max-width: 60%;
            overflow: hidden;
            text-overflow: ellipsis;
            white-space: nowrap;
        }}
        
        .log-time {{
            color: var(--secondary-color);
            font-family: monospace;
        }}
        
        .code-block.log {{
            background-color: rgba(0, 0, 0, 0.3);
            border-left: 3px solid var(--secondary-color);
        }}
        
        .code-block.error {{
            background-color: rgba(0, 0, 0, 0.3);
            border-left: 3px solid var(--error);
        }}
        
        /* Estilo para agrupar logs por categoría */
        .category-group {{
            margin-bottom: 25px;
            border-left: 3px solid var(--primary-light);
            padding-left: 15px;
        }}
        
        .category-title {{
            color: var(--primary-light);
            font-size: 1.1em;
            margin-bottom: 15px;
            padding-bottom: 5px;
            border-bottom: 1px dashed var(--border-color);
        }}
        
        @media (max-width: 768px) {{
            .info-grid {{
                grid-template-columns: 1fr;
            }}
            
            .filter-form form {{
                flex-direction: column;
            }}
            
            .related-log-header {{
                flex-direction: column;
                gap: 5px;
            }}
            
            .log-category {{
                max-width: 100%;
            }}
        }}
        
        .title-link {{
            color: var(--primary-light);
            text-decoration: none;
            transition: color 0.3s;
        }}
        
        .title-link:hover {{
            color: var(--secondary-color);
        }}
    </style>
    <!-- Hack para forzar estilos en selects para todos los navegadores -->
    <style id=""fix-selects"">
        /* Esto aplicará estilos a los selects en cualquier navegador */
        html select, html option {{
            background-color: #1e1e1e !important;
            color: #ffffff !important;
        }}
    </style>
</head>
<body>
    <!-- Script para asegurar que los selects tengan tema oscuro -->
    <script>
        // Este script se ejecuta inmediatamente y fuerza el tema oscuro en los selects
        document.addEventListener('DOMContentLoaded', function() {{
            // Una función que aplica un fondo oscuro a todos los selects y opciones
            function applyDarkStyles() {{
                var selects = document.querySelectorAll('select');
                for (var i = 0; i < selects.length; i++) {{
                    selects[i].style.backgroundColor = '#1e1e1e';
                    selects[i].style.color = '#ffffff';
                    
                    var options = selects[i].querySelectorAll('option');
                    for (var j = 0; j < options.length; j++) {{
                        options[j].style.backgroundColor = '#1e1e1e';
                        options[j].style.color = '#ffffff';
                    }}
                }}
            }}
            
            // Aplicar inmediatamente
            applyDarkStyles();
            
            // También aplicar cuando cambie el select (algunos navegadores resetean estilos)
            var selects = document.querySelectorAll('select');
            for (var i = 0; i < selects.length; i++) {{
                selects[i].addEventListener('change', applyDarkStyles);
                selects[i].addEventListener('focus', applyDarkStyles);
                selects[i].addEventListener('blur', applyDarkStyles);
            }}
        }});
    </script>
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