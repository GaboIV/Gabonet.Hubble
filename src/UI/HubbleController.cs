namespace Gabonet.Hubble.UI;

using Gabonet.Hubble.Interfaces;
using Gabonet.Hubble.Models;
using Gabonet.Hubble.UI.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

/// <summary>
/// Controlador para la interfaz de usuario de Hubble.
/// </summary>
public class HubbleController
{
    private readonly IHubbleService _hubbleService;
    private readonly string _version;
    private readonly string _basePath;
    private readonly string _prefixPath;
    private readonly Middleware.HubbleOptions _options;
    private readonly IHubbleStatsService? _statsService;

    /// <summary>
    /// Constructor del controlador de Hubble.
    /// </summary>
    /// <param name="hubbleService">Servicio de Hubble</param>
    /// <param name="options">Opciones de configuración de Hubble</param>
    /// <param name="statsService">Servicio de estadísticas</param>
    public HubbleController(
        IHubbleService hubbleService, 
        Middleware.HubbleOptions options,
        IHubbleStatsService? statsService = null)
    {
        _hubbleService = hubbleService;
        _version = GetAssemblyVersion();
        _basePath = options.BasePath.TrimEnd('/');
        _prefixPath = options.PrefixPath.TrimEnd('/');
        _options = options;
        _statsService = statsService;
    }

    /// <summary>
    /// Obtiene la versión del ensamblado actual
    /// </summary>
    /// <returns>Versión del ensamblado</returns>
    private string GetAssemblyVersion()
    {
        try
        {
            var assembly = Assembly.GetExecutingAssembly();
            var assemblyName = assembly.GetName();
            var version = assemblyName.Version;

            if (version != null)
            {
                return $"v{version}";
            }

            // Intentar obtener versión del ensamblado Gabonet.Hubble si estamos en un ensamblado diferente
            var hubbleAssembly = Assembly.Load("Gabonet.Hubble");
            if (hubbleAssembly != null)
            {
                var hubbleVersion = hubbleAssembly.GetName().Version;
                if (hubbleVersion != null)
                {
                    return $"v{hubbleVersion}";
                }
            }

            return "v0.2.8"; // Versión por defecto como fallback
        }
        catch
        {
            return "v0.2.8"; // En caso de error, devolver versión por defecto
        }
    }

    /// <summary>
    /// Obtiene la versión actual de Hubble
    /// </summary>
    /// <returns>Versión de Hubble</returns>
    public string GetVersion()
    {
        return _version;
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
        html += "<p><span class='app-title'>Hubble for .NET</span> <span class='app-version'>" + _version + "</span></p>";
        html += "</div>";

        // Botón de logout si la autenticación está habilitada
        html += "<div class='header-right'>";
        // if (_options.HighlightNewServices)
        // {
        //     html += "<div class='live-indicator'>Actualización en tiempo real <span id='reload-counter'>3</span>s</div>";
        // }
        html += $"<a href='{_prefixPath}{_basePath}/config' class='btn primary'>Configuración</a>";
        html += $"<a href='{_prefixPath}{_basePath}/logout' class='btn secondary'>Cerrar sesión</a>";
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
        html += $"<button onclick=\"if(confirm('¿Está seguro que desea eliminar todos los logs? Esta acción no se puede deshacer.')) {{ window.location.href='{_prefixPath}{_basePath}/delete-all'; }}\" class='btn danger'>Eliminar todos</button>";
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

            // Detectar y resaltar servicios nuevos basados en su fecha de creación
            var highlightClass = "";
            if (_options.HighlightNewServices)
            {
                try
                {
                    // Los logs ya vienen con la zona horaria configurada
                    // Usamos TimeZoneInfo.Local para obtener la zona horaria local del sistema
                    DateTime now = DateTime.Now;

                    // Calcular cuántos segundos han pasado desde la creación del log
                    var secondsSinceCreation = (now - log.Timestamp).TotalSeconds;

                    // Si el log se creó hace menos de 15 segundos (o el valor configurado), resaltarlo
                    if (Math.Abs(secondsSinceCreation) <= (_options.HighlightDurationSeconds > 0 ? _options.HighlightDurationSeconds : 15))
                    {
                        highlightClass = " new-service";
                    }
                }
                catch (Exception ex)
                {
                    // En caso de error en el cálculo de tiempo, imprimimos el error pero no detenemos la generación de la página
                    Console.WriteLine($"Error al calcular el tiempo para el resaltado: {ex.Message}");
                }
            }

            html += $"<tr class='{statusClass}{highlightClass}'>";
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
                // Mostrar URL y QueryParams en una sola línea
                html += "<td class='url-cell'>";
                html += $"<div class='url-path'>{log.HttpUrl}</div>";
                
                // Mostrar QueryParams si no están vacíos
                if (!string.IsNullOrEmpty(log.QueryParams) && log.QueryParams != "?")
                {
                    html += $"<div class='url-params'>{log.QueryParams}</div>";
                }
                
                html += "</td>";
            }

            html += $"<td>{log.StatusCode}</td>";
            html += $"<td>{log.ExecutionTime} ms</td>";

            // Añadir la etiqueta "NUEVO" para servicios resaltados en la columna de acciones
            if (highlightClass.Contains("new-service") && !string.IsNullOrEmpty(log.ServiceName))
            {
                html += $"<td><a href='{_prefixPath}{_basePath}/detail/{log.Id}' class='btn small'>Ver</a>♾️</td>";
            }
            else
            {
                html += $"<td><a href='{_prefixPath}{_basePath}/detail/{log.Id}' class='btn small'>Ver</a></td>";
            }

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

        // Agregar script para recarga automática si está habilitada la opción de resaltar servicios en tiempo real
        if (_options.HighlightNewServices)
        {
            html += @"
<script>
    // Función para recargar la página preservando los filtros actuales
    function reloadPageWithFilters() {
        // Obtener la URL actual con todos sus parámetros
        var currentUrl = window.location.href;
        // Recargar preservando la posición de scroll
        var scrollPosition = window.scrollY;
        
        // Usar fetch para cargar la página en segundo plano sin perder el estado
        fetch(currentUrl)
            .then(response => response.text())
            .then(html => {
                // Extraer solo el contenido de la tabla
                const parser = new DOMParser();
                const doc = parser.parseFromString(html, 'text/html');
                const newTable = doc.querySelector('.table-container');
                
                if (newTable) {
                    // Reemplazar solo la tabla, manteniendo el resto de la página
                    document.querySelector('.table-container').innerHTML = newTable.innerHTML;
                    
                    // Restaurar la posición de scroll
                    window.scrollTo(0, scrollPosition);
                }
            })
            .catch(error => {
                console.error('Error al recargar la tabla:', error);
            });
    }
    
    // Configurar recarga automática cada 3 segundos para actualizar la tabla de servicios
    setInterval(reloadPageWithFilters, 3000);
    
    // Actualizar el contador de recarga
    setInterval(function() {
        var counter = document.getElementById('reload-counter');
        if (counter) {
            var count = parseInt(counter.textContent);
            if (count > 1) {
                counter.textContent = count - 1;
            } else {
                counter.textContent = 3;
            }
        }
    }, 1000);
</script>";
        }

        html += "</body></html>";
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
        html += "<div class='action-buttons'>";
        html += $"<a href='{_prefixPath}{_basePath}' class='btn primary'>Volver a la lista</a>";
        html += "</div>";
        html += "</div>";

        // Botón de logout si la autenticación está habilitada
        html += "<div class='header-right'>";
        html += $"<a href='{_prefixPath}{_basePath}/logout' class='btn secondary'>Cerrar sesión</a>";
        html += "</div>";
        html += "</div>";

        html += "<h2 class='page-title'>Detalle del Log</h2>";

        // Estilos para acordeón
        html += @"
        <style>
            .card {
                margin-bottom: 15px;
                border-radius: 8px;
                overflow: hidden;
            }
            .card-header {
                background-color: var(--surface);
                padding: 15px;
                cursor: pointer;
                display: flex;
                justify-content: space-between;
                align-items: center;
                transition: background-color 0.3s;
            }
            .card-header:hover {
                background-color: #2a2a2a;
            }
            .card-header h2 {
                margin: 0;
                font-size: 1.2rem;
            }
            .card-header::after {
                content: '▼';
                font-size: 12px;
                transition: transform 0.3s;
            }
            .card-header.collapsed::after {
                transform: rotate(-90deg);
            }
            .card-content {
                background-color: var(--surface);
                overflow: hidden;
            }
            .card-header.collapsed + .card-content {
                display: none;
            }
            .card-content-inner {
                padding: 15px;
            }
            .error-card .card-header {
                background-color: rgba(207, 102, 121, 0.2);
            }
            .error-card .card-content {
                background-color: rgba(207, 102, 121, 0.1);
            }
        </style>";

        // Script para funcionalidad de acordeón
        html += @"
        <script>
            document.addEventListener('DOMContentLoaded', function() {
                // Funcionalidad de acordeón para las tarjetas
                const cardHeaders = document.querySelectorAll('.card-header');
                cardHeaders.forEach(header => {
                    header.addEventListener('click', function() {
                        this.classList.toggle('collapsed');
                    });
                });
                
                // Expandir la primera tarjeta por defecto
                if (cardHeaders.length > 0) {
                    cardHeaders[0].classList.remove('collapsed');
                }

                // Colapsar el resto de tarjetas
                for (let i = 1; i < cardHeaders.length; i++) {
                    cardHeaders[i].classList.add('collapsed');
                }
            });
        </script>";

        // Información general
        html += "<div class='card'>";
        html += "<div class='card-header'><h2>Información General</h2></div>";
        html += "<div class='card-content'><div class='card-content-inner'>";
        html += "<div class='info-grid'>";
        html += $"<div class='info-item'><span>Fecha/Hora:</span> {log.Timestamp.ToString("yyyy-MM-dd HH:mm:ss")}</div>";
        html += $"<div class='info-item'><span>Método:</span> {log.Method}</div>";
        html += $"<div class='info-item'><span>Controlador:</span> {log.ControllerName}</div>";
        html += $"<div class='info-item'><span>Acción:</span> {log.ActionName}</div>";
        html += $"<div class='info-item'><span>Estado:</span> <span class='{(log.IsError || log.StatusCode >= 400 ? "error-text" : "success-text")}'>{log.StatusCode}</span></div>";
        html += $"<div class='info-item'><span>Duración:</span> {log.ExecutionTime} ms</div>";
        html += "</div>";
        
        // URL con QueryParams en una línea completa
        html += "<div class='url-item'>";
        html += "<div class='url-label'>URL:</div>";
        html += $"<div class='url-value'>{log.HttpUrl}</div>";
        
        // Mostrar QueryParams si no están vacíos
        if (!string.IsNullOrEmpty(log.QueryParams) && log.QueryParams != "?")
        {
            html += $"<div class='url-params-line'>{log.QueryParams}</div>";
        }
        
        html += "</div>";
        
        html += "</div></div>";
        html += "</div>";

        // Si hay error
        if (log.IsError || !string.IsNullOrEmpty(log.ErrorMessage))
        {
            html += "<div class='card error-card'>";
            html += "<div class='card-header collapsed'><h2>Error</h2></div>";
            html += "<div class='card-content'><div class='card-content-inner'>";
            html += $"<div class='code-block'>{log.ErrorMessage}</div>";

            if (!string.IsNullOrEmpty(log.StackTrace))
            {
                html += "<h3>Stack Trace</h3>";
                html += $"<div class='code-block'>{log.StackTrace}</div>";
            }

            html += "</div></div>";
            html += "</div>";
        }

        // Cabeceras de la solicitud
        if (!string.IsNullOrEmpty(log.RequestHeaders))
        {
            html += "<div class='card'>";
            html += "<div class='card-header collapsed'><h2>Cabeceras de la Solicitud</h2></div>";
            html += "<div class='card-content'><div class='card-content-inner'>";
            html += $"<div class='code-block'>{FormatJson(log.RequestHeaders)}</div>";
            html += "</div></div>";
            html += "</div>";
        }

        // Datos de la solicitud
        if (!string.IsNullOrEmpty(log.RequestData))
        {
            html += "<div class='card'>";
            html += "<div class='card-header collapsed'><h2>Datos de la Solicitud</h2></div>";
            html += "<div class='card-content'><div class='card-content-inner'>";
            html += $"<div class='code-block'>{FormatJson(log.RequestData)}</div>";
            html += "</div></div>";
            html += "</div>";
        }

        // Datos de la respuesta
        if (!string.IsNullOrEmpty(log.ResponseData))
        {
            html += "<div class='card'>";
            html += "<div class='card-header collapsed'><h2>Datos de la Respuesta</h2></div>";
            html += "<div class='card-content'><div class='card-content-inner'>";
            html += $"<div class='code-block'>{FormatJson(log.ResponseData)}</div>";
            html += "</div></div>";
            html += "</div>";
        }

        // Consultas a bases de datos
        if (log.DatabaseQueries.Count > 0)
        {
            html += "<div class='card'>";
            html += "<div class='card-header collapsed'><h2>Consultas a Bases de Datos</h2></div>";
            html += "<div class='card-content'><div class='card-content-inner'>";

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

            html += "</div></div>";
            html += "</div>";
        }

        // Logs relacionados (logs de ILogger asociados a esta solicitud)
        var relatedLogs = await _hubbleService.GetRelatedLogsAsync(log.Id ?? string.Empty);
        if (relatedLogs.Count > 0)
        {
            html += "<div class='card'>";
            html += "<div class='card-header collapsed'><h2>Loggers</h2></div>";
            html += "<div class='card-content'><div class='card-content-inner'>";

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

            html += "</div></div>";
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
        html += $"<a href='{_prefixPath}{_basePath}/logout' class='btn secondary'>Cerrar sesión</a>";
        html += "</div>";
        html += "</div>";

        html += "<div class='card success-card'>";
        html += "<h2>Operación exitosa</h2>";
        html += "<p>Todos los logs han sido eliminados correctamente.</p><br>";
        html += $"<a href='{_prefixPath}{_basePath}' class='btn primary'>Volver a la lista</a>";
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
        html += "<div class='action-buttons'>";
        html += $"<a href='{_prefixPath}{_basePath}' class='btn primary'>Volver a la lista</a>";
        html += "</div>";
        html += "</div>";

        // Botón de logout si la autenticación está habilitada
        html += "<div class='header-right'>";
        html += $"<a href='{_prefixPath}{_basePath}/logout' class='btn secondary'>Cerrar sesión</a>";
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
    public string GetHubbleLogo()
    {
        return $@"<div class='logo-container'>
            <a href='{_prefixPath}{_basePath}' title='Recargar Hubble Dashboard' class='logo-link'>
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
        
        /* Estilos para el título y versión de la aplicación */
        .app-title {{
            font-size: 1em;
            font-weight: 500;
            color: var(--primary-light);
        }}
        
        .app-version {{
            font-size: 0.7em;
            color: var(--secondary-color);
            opacity: 0.8;
            margin-left: 5px;
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
        
        /* Estilo para nuevos servicios detectados en tiempo real */
        .data-table tr.new-service {{
            background-color: rgba(255, 193, 7, 0.3);
            animation: highlight-new 3s ease-out infinite alternate;
            border-left: 4px solid #ffc107;
            position: relative;
        }}
        
        .data-table tr.new-service:hover {{
            background-color: rgba(255, 193, 7, 0.5);
        }}
        
        .data-table tr.error.new-service {{
            background-color: rgba(207, 102, 121, 0.15);
            border-left: 4px solid #ffc107;
        }}
        
        .new-service-label {{
            display: inline-block;
            background-color: #ffc107;
            color: #000;
            font-size: 0.6em;
            padding: 2px 5px;
            border-radius: 3px;
            margin-left: 5px;
            font-weight: bold;
            animation: pulse 1s infinite;
            vertical-align: middle;
        }}
        
        @keyframes pulse {{
            0% {{ opacity: 0.7; }}
            50% {{ opacity: 1; }}
            100% {{ opacity: 0.7; }}
        }}
        
        @keyframes highlight-new {{
            0% {{ background-color: rgba(255, 193, 7, 0.3); }}
            100% {{ background-color: rgba(255, 193, 7, 0.15); }}
        }}
        
        .url-cell {{
            max-width: 300px;
            padding: 8px 12px !important;
        }}
        
        .url-path {{
            white-space: nowrap;
            overflow: hidden;
            text-overflow: ellipsis;
            max-width: 100%;
            display: block;
        }}
        
        .url-params {{
            color: var(--primary-light);
            font-size: 0.85em;
            margin-top: 3px;
            padding: 2px 5px;
            background-color: rgba(187, 134, 252, 0.1);
            border-radius: 3px;
            white-space: nowrap;
            overflow: hidden;
            text-overflow: ellipsis;
            max-width: 100%;
            display: block;
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
            font-size: 1.2em;
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
        
        /* Estilos para URL con QueryParams en la página de detalle */
        .url-item {{
            margin-top: 15px;
            padding: 15px;
            background-color: #292929;
            border-radius: 4px;
            border-left: 4px solid var(--primary-light);
        }}
        
        .url-label {{
            display: block;
            color: var(--text-secondary);
            margin-bottom: 10px;
            font-weight: 500;
        }}
        
        .url-value {{
            display: block;
            word-break: break-all;
            color: var(--text-primary);
            font-family: 'Consolas', 'Monaco', monospace;
        }}
        
        .url-params-line {{
            display: block;
            margin-top: 10px;
            padding: 8px 12px;
            background-color: rgba(187, 134, 252, 0.1);
            border-radius: 4px;
            color: var(--primary-light);
            font-family: 'Consolas', 'Monaco', monospace;
            word-break: break-all;
        }}
        
        .url-detail {{
            display: inline-flex;
            flex-direction: column;
            max-width: 100%;
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
        
        /* Estilos para el indicador de actualización en tiempo real */
        .live-indicator {{
            display: inline-flex;
            align-items: center;
            margin-right: 15px;
            background-color: rgba(255, 193, 7, 0.2);
            color: #ffc107;
            font-size: 0.85em;
            padding: 5px 10px;
            border-radius: 20px;
            border: 1px solid rgba(255, 193, 7, 0.5);
        }}
        
        #reload-counter {{
            font-weight: bold;
            margin: 0 3px;
            animation: pulse 1s infinite;
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
        return $@"
    <footer style='text-align: center; margin-top: 40px; padding: 20px; color: var(--text-secondary);'>
        <p><span class='app-title'>Hubble for .NET</span> <span class='app-version'>{_version}</span></p>
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

    /// <summary>
    /// Muestra la página de configuración y estadísticas de Hubble
    /// </summary>
    /// <returns>HTML con la configuración y estadísticas</returns>
    public async Task<string> GetConfigurationPageAsync()
    {
        if (_statsService == null)
        {
            return GenerateErrorPage("Error", "El servicio de estadísticas no está disponible");
        }
        
        HubbleStatistics stats = null;
        HubbleSystemConfiguration config = null;
        
        try
        {
            stats = await _statsService.GetStatisticsAsync();
            config = await _statsService.GetSystemConfigurationAsync();
        }
        catch (Exception ex)
        {
            return GenerateErrorPage("Error", $"No se pudo obtener las estadísticas o configuración: {ex.Message}");
        }
        
        if (stats == null || config == null)
        {
            return GenerateErrorPage("Error", "No se pudieron cargar las estadísticas o la configuración");
        }
        
        // Generar HTML (el resto del método queda igual)
        var html = GenerateHtmlHeader("Hubble - Configuración", true);
        
        html += "<div class='container'>";
        html += "<div class='header'>";
        html += "<div class='header-left'>";
        html += GetHubbleLogo();
        html += "<p><span class='app-title'>Hubble for .NET</span> <span class='app-version'>" + _version + "</span></p>";
        html += "</div>";
        
        // Botones de navegación
        html += "<div class='header-right'>";
        html += $"<a href='{_prefixPath}{_basePath}' class='btn secondary'>Volver a logs</a>";
        html += $"<a href='{_prefixPath}{_basePath}/logout' class='btn secondary'>Cerrar sesión</a>";
        html += "</div>";
        html += "</div>";
        
        // Contenido principal con dos columnas: estadísticas y configuración
        html += "<div class='config-container'>";
        
        // Primera columna: Estadísticas
        html += "<div class='config-column stats-column'>";
        html += "<h2>Estadísticas</h2>";
        
        html += "<div class='stats-card'>";
        html += "<h3>Resumen de logs</h3>";
        html += "<div class='stats-grid'>";
        html += $"<div class='stat-item'><span class='stat-value'>{stats.TotalLogs}</span><span class='stat-label'>Total de logs</span></div>";
        html += $"<div class='stat-item'><span class='stat-value'>{stats.SuccessfulLogs}</span><span class='stat-label'>Logs exitosos (2xx)</span></div>";
        html += $"<div class='stat-item'><span class='stat-value'>{stats.FailedLogs}</span><span class='stat-label'>Logs fallidos (4xx/5xx)</span></div>";
        html += $"<div class='stat-item'><span class='stat-value'>{stats.LoggerLogs}</span><span class='stat-label'>Logs de ILogger</span></div>";
        html += "</div>";
        html += "</div>";
        
        // Información sobre el último prune
        html += "<div class='stats-card'>";
        html += "<h3>Limpieza de datos</h3>";
        
        if (stats.LastPrune.LastPruneDate.HasValue)
        {
            var lastPruneDate = stats.LastPrune.LastPruneDate.Value.ToLocalTime();
            var timeAgo = DateTime.Now - lastPruneDate;
            var timeAgoText = FormatTimeAgo(timeAgo);
            
            html += "<div class='stats-grid'>";
            html += $"<div class='stat-item'><span class='stat-value'>{lastPruneDate:dd/MM/yyyy}</span><span class='stat-label'>Fecha</span></div>";
            html += $"<div class='stat-item'><span class='stat-value'>{lastPruneDate:HH:mm:ss}</span><span class='stat-label'>Hora</span></div>";
            html += $"<div class='stat-item'><span class='stat-value'>{stats.LastPrune.LogsDeleted}</span><span class='stat-label'>Logs eliminados</span></div>";
            html += $"<div class='stat-item time-ago'><span class='time-ago-text'>Última limpieza: {timeAgoText}</span></div>";
            html += "</div>";
            
            // Reemplazar botón con mensaje informativo
            html += "<div class='info-message' style='margin-top: 15px;'>";
            html += "<p>La limpieza automática está habilitada con un intervalo de " + config.DataPruneIntervalHours + " horas.</p>";
            html += "</div>";
        }
        else
        {
            html += "<p>No se ha realizado ninguna limpieza automática de datos.</p>";
            
            if (config.EnableDataPrune)
            {
                html += "<div class='info-message'>";
                html += "<p>La limpieza automática está habilitada y se ejecutará cada " + config.DataPruneIntervalHours + " horas.</p>";
                html += "</div>";
            }
            else
            {
                html += "<div class='info-message'>";
                html += "<p>La limpieza automática está deshabilitada.</p>";
                html += "</div>";
            }
        }
        html += "</div>";
        
        // Información de estadística sin botón de recalcular
        html += "<div class='info-message stats-info'>";
        html += "<p>Las estadísticas se actualizan automáticamente cada vez que se accede a esta página.</p>";
        html += "</div>";
        
        html += "</div>"; // Fin de la primera columna
        
        // Segunda columna: Configuración del sistema
        html += "<div class='config-column config-settings-column'>";
        html += "<h2>Configuración del sistema</h2>";
        
        // Información del sistema
        html += "<div class='stats-card'>";
        html += "<h3>Información del sistema</h3>";
        html += "<div class='config-form'>";
        html += "<div class='config-group'>";
        html += "<label>Servicio:</label>";
        html += $"<div class='config-value'>{config.ServiceName}</div>";
        html += "</div>";
        html += "<div class='config-group'>";
        html += "<label>Versión:</label>";
        html += $"<div class='config-value'>{config.SystemInfo.Version}</div>";
        html += "</div>";
        html += "<div class='config-group'>";
        html += "<label>Base de datos:</label>";
        html += $"<div class='config-value'>{config.SystemInfo.DatabaseName}</div>";
        html += "</div>";
        html += "<div class='config-group'>";
        html += "<label>MongoDB:</label>";
        html += $"<div class='config-value'>Driver {config.SystemInfo.MongoDBVersion}</div>";
        html += "</div>";
        html += "<div class='config-group'>";
        html += "<label>Zona horaria:</label>";
        html += $"<div class='config-value'>{(string.IsNullOrEmpty(config.TimeZoneId) ? "UTC" : config.TimeZoneId)}</div>";
        html += "</div>";
        html += "<div class='config-group'>";
        html += "<label>Diagnósticos:</label>";
        html += $"<div class='config-value'>Activado</div>";
        html += "</div>";
        html += "<div class='config-group'>";
        html += "<label>Ruta base:</label>";
        html += $"<div class='config-value'>{_prefixPath}{_basePath}</div>";
        html += "</div>";
        html += "<div class='config-group'>";
        html += "<label>Autenticación requerida:</label>";
        html += $"<div class='config-value'>{(_options.RequireAuthentication ? "Sí" : "No")}</div>";
        html += "</div>";
        html += "<div class='config-group'>";
        html += "<label>Resaltar nuevos servicios:</label>";
        html += $"<div class='config-value'>{(_options.HighlightNewServices ? "Sí" : "No")}</div>";
        html += "</div>";
        html += "<div class='config-group'>";
        html += "<label>Duración de resaltado (segundos):</label>";
        html += $"<div class='config-value'>{_options.HighlightDurationSeconds}</div>";
        html += "</div>";
        html += "<div class='config-group'>";
        html += "<label>Inicio del servicio:</label>";
        
        var startTime = config.SystemInfo.StartTime.ToLocalTime();
        var uptime = DateTime.Now - startTime;
        var uptimeText = FormatTimeAgo(uptime);
        
        html += $"<div class='config-value'>{startTime:dd/MM/yyyy HH:mm:ss} <span class='uptime'>({uptimeText})</span></div>";
        html += "</div>";
        html += "</div>";
        html += "</div>";
        
        // Configuración de limpieza
        html += "<div class='stats-card'>";
        html += "<h3>Configuración de limpieza</h3>";
        html += "<div class='config-form'>";
        html += "<div class='config-group'>";
        html += "<label>Habilitar limpieza automática:</label>";
        html += $"<div class='config-value'>{(config.EnableDataPrune ? "Activado" : "Desactivado")}</div>";
        html += "</div>";
        html += "<div class='config-group'>";
        html += "<label>Intervalo de limpieza (horas):</label>";
        html += $"<div class='config-value'>{config.DataPruneIntervalHours}</div>";
        html += "</div>";
        html += "<div class='config-group'>";
        html += "<label>Edad máxima de los logs (horas):</label>";
        html += $"<div class='config-value'>{config.MaxLogAgeHours}</div>";
        html += "</div>";
        html += "</div>";
        html += "<div class='info-message'>";
        html += "<p>La configuración no puede ser modificada. Consulte al administrador del sistema para realizar cambios.</p>";
        html += "</div>";
        html += "</div>";
        
        // Configuración de captura de datos
        html += "<div class='stats-card'>";
        html += "<h3>Configuración de captura</h3>";
        html += "<div class='config-form'>";
        html += "<div class='config-group'>";
        html += "<label>Capturar solicitudes HTTP (HUBBLE_ENABLE_DIAGNOSTICS):</label>";
        
        // Obtener el valor directamente de la variable de entorno
        var enableDiagnostics = Environment.GetEnvironmentVariable("HUBBLE_ENABLE_DIAGNOSTICS");
        var isEnabledStr = !string.IsNullOrEmpty(enableDiagnostics) && 
                          (enableDiagnostics.ToLower() == "true" || enableDiagnostics == "1") 
                          ? "Activado" : "Desactivado";
        
        html += $"<div class='config-value'>{isEnabledStr}</div>";
        html += "</div>";
        html += "<div class='config-group'>";
        html += "<label>Capturar mensajes de ILogger (HUBBLE_CAPTURE_LOGGER_MESSAGES):</label>";
        html += $"<div class='config-value'>Activado</div>";
        html += "</div>";
        html += "<div class='config-group'>";
        html += "<label>Nivel mínimo de log:</label>";
        html += $"<div class='config-value'>{config.MinimumLogLevel}</div>";
        html += "</div>";
        html += "</div>";
        html += "<div class='info-message'>";
        html += "<p>La configuración no puede ser modificada. Consulte al administrador del sistema para realizar cambios.</p>";
        html += "</div>";
        html += "</div>";
        
        // Rutas ignoradas
        html += "<div class='stats-card'>";
        html += "<h3>Rutas ignoradas</h3>";
        html += "<div class='config-form'>";
        html += "<div class='config-group ignored-paths'>";
        
        // Obtener las rutas ignoradas desde el servicio o mostrar el placeholder
        var hubbleIgnorePaths = Environment.GetEnvironmentVariable("HUBBLE_IGNORE_PATHS");
        if (!string.IsNullOrEmpty(hubbleIgnorePaths))
        {
            var ignorePaths = hubbleIgnorePaths.Split(',').Select(p => p.Trim()).ToList();
            
            if (ignorePaths.Count > 0)
            {
                html += "<div class='config-value'>HUBBLE_IGNORE_PATHS:</div>";
                html += "<ul class='ignored-paths-list'>";
                foreach (var path in ignorePaths)
                {
                    html += $"<li>{path}</li>";
                }
                html += "</ul>";
            }
            else
            {
                html += "<div class='config-value'>No hay rutas ignoradas configuradas (HUBBLE_IGNORE_PATHS vacío).</div>";
            }
        }
        else
        {
            html += "<div class='config-value'>No hay rutas ignoradas configuradas (HUBBLE_IGNORE_PATHS no definido).</div>";
        }
        
        html += "</div>";
        html += "</div>";
        html += "<div class='info-message'>";
        html += "<p>La configuración no puede ser modificada. Consulte al administrador del sistema para realizar cambios.</p>";
        html += "</div>";
        html += "</div>";
        
        html += "</div>"; // Fin de la segunda columna
        html += "</div>"; // Fin del contenedor de configuración
        
        html += "</div>"; // Fin del contenedor principal
        
        // Agregar estilos CSS específicos para la página de configuración
        html += "<style>";
        html += ".config-container { display: flex; flex-wrap: wrap; gap: 20px; margin-top: 20px; }";
        html += ".config-column { flex: 1; min-width: 300px; }";
        html += ".stats-column, .config-settings-column { display: flex; flex-direction: column; gap: 20px; }";
        html += ".stats-card { background: #1e1e1e; border-radius: 8px; padding: 20px; box-shadow: 0 2px 10px rgba(0,0,0,0.2); color: #ffffff; }";
        html += ".stats-card h3 { margin-top: 0; color: #bb86fc; font-size: 18px; margin-bottom: 15px; }";
        html += ".stats-grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(120px, 1fr)); gap: 15px; }";
        html += ".stat-item { display: flex; flex-direction: column; }";
        html += ".stat-value { font-size: 24px; font-weight: 600; color: #bb86fc; }";
        html += ".stat-label { font-size: 14px; color: rgba(255, 255, 255, 0.7); margin-top: 5px; }";
        html += ".action-buttons { margin-top: 20px; display: flex; gap: 10px; justify-content: flex-end; }";
        html += ".config-form { display: flex; flex-direction: column; gap: 15px; }";
        html += ".config-group { display: flex; justify-content: space-between; align-items: center; }";
        html += ".config-value { font-family: 'Courier New', monospace; color: #03dac6; }";
        html += ".system-info { word-break: break-all; max-width: 280px; }";
        html += ".uptime { color: rgba(255, 255, 255, 0.6); font-style: italic; }";
        html += ".switch { position: relative; display: inline-block; width: 60px; height: 30px; }";
        html += ".switch input { opacity: 0; width: 0; height: 0; }";
        html += ".slider { position: absolute; cursor: pointer; top: 0; left: 0; right: 0; bottom: 0; background-color: #333; transition: .4s; border-radius: 30px; }";
        html += ".slider:before { position: absolute; content: ''; height: 22px; width: 22px; left: 4px; bottom: 4px; background-color: white; transition: .4s; border-radius: 50%; }";
        html += "input:checked + .slider { background-color: #6200ee; }";
        html += "input:focus + .slider { box-shadow: 0 0 1px #6200ee; }";
        html += "input:checked + .slider:before { transform: translateX(30px); }";
        html += ".textarea-field { width: 100%; min-height: 100px; border: 1px solid #333; border-radius: 4px; padding: 8px; font-family: 'Courier New', monospace; background-color: #121212; color: white; }";
        html += ".input-field { background-color: #121212; color: white; border: 1px solid #333; padding: 8px; border-radius: 4px; width: 100px; }";
        html += ".select-field { background-color: #121212; color: white; border: 1px solid #333; padding: 8px; border-radius: 4px; min-width: 150px; }";
        html += "h2 { color: #bb86fc; margin-bottom: 15px; font-size: 22px; }";
        html += "p { color: rgba(255, 255, 255, 0.8); }";
        html += "label { color: rgba(255, 255, 255, 0.8); }";
        html += ".btn.primary { background-color: #6200ee; color: white; border: none; padding: 8px 16px; border-radius: 4px; cursor: pointer; font-weight: 500; }";
        html += ".btn.secondary { background-color: transparent; color: #bb86fc; border: 1px solid #bb86fc; padding: 8px 16px; border-radius: 4px; cursor: pointer; font-weight: 500; }";
        html += ".btn.primary:hover { background-color: #7a36f9; }";
        html += ".btn.secondary:hover { background-color: rgba(187, 134, 252, 0.1); }";
        html += ".info-message { background-color: rgba(98, 0, 238, 0.1); border-left: 3px solid #6200ee; padding: 10px; margin-top: 15px; border-radius: 0 4px 4px 0; }";
        html += ".info-message p { color: rgba(255, 255, 255, 0.9); margin: 0; font-style: italic; }";
        html += ".stats-info { background-color: rgba(3, 218, 198, 0.1); border-left: 3px solid #03dac6; margin: 20px auto; max-width: 800px; }";
        html += ".ignored-paths { flex-direction: column; align-items: flex-start !important; }";
        html += ".ignored-paths-list { list-style-type: none; padding: 0; margin: 0; width: 100%; }";
        html += ".ignored-paths-list li { padding: 5px 0; border-bottom: 1px solid #333; color: #03dac6; font-family: 'Courier New', monospace; }";
        html += ".ignored-paths-list li:last-child { border-bottom: none; }";
        html += ".time-ago-text { color: rgba(255, 255, 255, 0.7); font-style: italic; grid-column: span 2; }";
        html += ".time-ago { grid-column: span 2; margin-top: 5px; }";
        html += "</style>";
        
        html += GenerateHtmlFooter();
        
        return html;
    }
    
    /// <summary>
    /// Ejecuta una limpieza manual de datos antiguos
    /// </summary>
    /// <returns>Redirección a la página de configuración</returns>
    public async Task<string> RunManualPruneAsync()
    {
        if (_statsService == null)
        {
            return GenerateErrorPage("Error", "El servicio de estadísticas no está disponible");
        }
        
        try
        {
            var config = await _statsService.GetSystemConfigurationAsync();
            var maxAgeHours = config.MaxLogAgeHours > 0 ? config.MaxLogAgeHours : 24;
            var cutoffDate = DateTime.UtcNow.AddHours(-maxAgeHours);
            
            var logsDeleted = await _hubbleService.DeleteLogsOlderThanAsync(cutoffDate);
            await _statsService.UpdatePruneStatisticsAsync(DateTime.UtcNow, logsDeleted);
            
            return $"<script>alert('Limpieza manual completada. Se eliminaron {logsDeleted} logs.'); window.location.href='{_prefixPath}{_basePath}/config';</script>";
        }
        catch (Exception ex)
        {
            return GenerateErrorPage("Error", $"Error al ejecutar la limpieza manual: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Recalcula las estadísticas del sistema
    /// </summary>
    /// <returns>Redirección a la página de configuración</returns>
    public async Task<string> RecalculateStatisticsAsync()
    {
        if (_statsService == null)
        {
            return GenerateErrorPage("Error", "El servicio de estadísticas no está disponible");
        }
        
        try
        {
            await _statsService.RecalculateStatisticsAsync();
            return $"<script>alert('Estadísticas recalculadas correctamente.'); window.location.href='{_prefixPath}{_basePath}/config';</script>";
        }
        catch (Exception ex)
        {
            return GenerateErrorPage("Error", $"Error al recalcular las estadísticas: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Guarda la configuración de limpieza de datos
    /// </summary>
    /// <param name="enableDataPrune">Indica si se debe habilitar la limpieza automática</param>
    /// <param name="dataPruneIntervalHours">Intervalo de limpieza en horas</param>
    /// <param name="maxLogAgeHours">Edad máxima de los logs en horas</param>
    /// <returns>Redirección a la página de configuración</returns>
    public async Task<string> SavePruneConfigAsync(bool enableDataPrune, int dataPruneIntervalHours, int maxLogAgeHours)
    {
        if (_statsService == null)
        {
            return GenerateErrorPage("Error", "El servicio de estadísticas no está disponible");
        }
        
        try
        {
            var config = await _statsService.GetSystemConfigurationAsync();
            
            config.EnableDataPrune = enableDataPrune;
            config.DataPruneIntervalHours = Math.Max(1, Math.Min(168, dataPruneIntervalHours)); // Entre 1 y 168 horas
            config.MaxLogAgeHours = Math.Max(1, Math.Min(8760, maxLogAgeHours)); // Entre 1 hora y 1 año
            
            await _statsService.SaveSystemConfigurationAsync(config);
            
            return $"<script>alert('Configuración guardada correctamente.'); window.location.href='{_prefixPath}{_basePath}/config';</script>";
        }
        catch (Exception ex)
        {
            return GenerateErrorPage("Error", $"Error al guardar la configuración: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Guarda la configuración de captura de datos
    /// </summary>
    /// <param name="captureHttpRequests">Indica si se deben capturar solicitudes HTTP</param>
    /// <param name="captureLoggerMessages">Indica si se deben capturar mensajes de ILogger</param>
    /// <param name="minimumLogLevel">Nivel mínimo de log a capturar</param>
    /// <returns>Redirección a la página de configuración</returns>
    public async Task<string> SaveCaptureConfigAsync(bool captureHttpRequests, bool captureLoggerMessages, string minimumLogLevel)
    {
        if (_statsService == null)
        {
            return GenerateErrorPage("Error", "El servicio de estadísticas no está disponible");
        }
        
        try
        {
            var config = await _statsService.GetSystemConfigurationAsync();
            
            config.CaptureHttpRequests = captureHttpRequests;
            config.CaptureLoggerMessages = captureLoggerMessages;
            config.MinimumLogLevel = minimumLogLevel;
            
            await _statsService.SaveSystemConfigurationAsync(config);
            
            return $"<script>alert('Configuración de captura guardada correctamente.'); window.location.href='{_prefixPath}{_basePath}/config';</script>";
        }
        catch (Exception ex)
        {
            return GenerateErrorPage("Error", $"Error al guardar la configuración de captura: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Guarda la configuración de rutas ignoradas
    /// </summary>
    /// <param name="ignorePaths">Rutas a ignorar (una por línea)</param>
    /// <returns>Redirección a la página de configuración</returns>
    public async Task<string> SaveIgnorePathsAsync(string ignorePaths)
    {
        if (_statsService == null)
        {
            return GenerateErrorPage("Error", "El servicio de estadísticas no está disponible");
        }
        
        try
        {
            var config = await _statsService.GetSystemConfigurationAsync();
            
            // Convertir el texto a una lista de rutas (eliminar líneas vacías)
            var paths = ignorePaths.Split('\n')
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Select(p => p.Trim())
                .ToList();
            
            config.IgnorePaths = paths;
            
            await _statsService.SaveSystemConfigurationAsync(config);
            
            return $"<script>alert('Configuración de rutas ignoradas guardada correctamente.'); window.location.href='{_prefixPath}{_basePath}/config';</script>";
        }
        catch (Exception ex)
        {
            return GenerateErrorPage("Error", $"Error al guardar las rutas ignoradas: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Formatea un intervalo de tiempo en formato legible
    /// </summary>
    /// <param name="timeSpan">Intervalo de tiempo</param>
    /// <returns>Texto formateado</returns>
    private string FormatTimeAgo(TimeSpan timeSpan)
    {
        if (timeSpan.TotalDays > 365)
        {
            var years = (int)(timeSpan.TotalDays / 365);
            return years == 1 ? "hace 1 año" : $"hace {years} años";
        }
        
        if (timeSpan.TotalDays > 30)
        {
            var months = (int)(timeSpan.TotalDays / 30);
            return months == 1 ? "hace 1 mes" : $"hace {months} meses";
        }
        
        if (timeSpan.TotalDays >= 1)
        {
            var days = (int)timeSpan.TotalDays;
            return days == 1 ? "hace 1 día" : $"hace {days} días";
        }
        
        if (timeSpan.TotalHours >= 1)
        {
            var hours = (int)timeSpan.TotalHours;
            return hours == 1 ? "hace 1 hora" : $"hace {hours} horas";
        }
        
        if (timeSpan.TotalMinutes >= 1)
        {
            var minutes = (int)timeSpan.TotalMinutes;
            return minutes == 1 ? "hace 1 minuto" : $"hace {minutes} minutos";
        }
        
        return "hace unos segundos";
    }

    #region API JSON Methods

    /// <summary>
    /// Gets logs in JSON format with filtering and pagination
    /// </summary>
    /// <param name="method">HTTP method filter</param>
    /// <param name="url">URL filter</param>
    /// <param name="statusGroup">Status code group filter (200, 400, 500)</param>
    /// <param name="logType">Log type filter (ApplicationLogger, HTTP)</param>
    /// <param name="page">Page number</param>
    /// <param name="pageSize">Page size</param>
    /// <returns>JSON response with logs and pagination info</returns>
    public async Task<LogsApiResponse> GetLogsApiAsync(
        string? method = null,
        string? url = null,
        string? statusGroup = null,
        string? logType = null,
        int page = 1,
        int pageSize = 50)
    {
        try
        {
            // Default to exclude related logs unless explicitly requesting ApplicationLogger logs
            bool excludeRelatedLogs = string.IsNullOrEmpty(logType) || logType != "ApplicationLogger";

            // Get total count before applying pagination
            var totalCount = await _hubbleService.GetTotalLogsCountAsync(method, url, excludeRelatedLogs);

            // Get logs for current page
            var logs = await _hubbleService.GetFilteredLogsWithRelatedAsync(method, url, excludeRelatedLogs, page, pageSize);

            // Filter by status code group if specified
            if (!string.IsNullOrEmpty(statusGroup) && int.TryParse(statusGroup, out int statusBase))
            {
                logs = logs.Where(log => log.StatusCode >= statusBase && log.StatusCode < statusBase + 100).ToList();
            }

            // Filter by log type if explicitly specified
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

            var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);

            return new LogsApiResponse
            {
                Logs = logs,
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount,
                TotalPages = totalPages,
                HasNextPage = page < totalPages,
                HasPreviousPage = page > 1
            };
        }
        catch (Exception)
        {
            return new LogsApiResponse
            {
                Logs = new List<GeneralLog>(),
                Page = page,
                PageSize = pageSize,
                TotalCount = 0,
                TotalPages = 0,
                HasNextPage = false,
                HasPreviousPage = false
            };
        }
    }

    /// <summary>
    /// Gets a specific log by ID in JSON format
    /// </summary>
    /// <param name="id">Log ID</param>
    /// <returns>JSON response with log details and related logs</returns>
    public async Task<LogDetailApiResponse> GetLogDetailApiAsync(string id)
    {
        try
        {
            var log = await _hubbleService.GetLogByIdAsync(id);
            
            if (log == null)
            {
                return new LogDetailApiResponse
                {
                    Found = false,
                    Log = null,
                    RelatedLogs = new List<GeneralLog>()
                };
            }

            var relatedLogs = new List<GeneralLog>();

            // Get related logs if this is an HTTP request log
            if (!string.IsNullOrEmpty(log.RelatedRequestId) && log.ControllerName != "ApplicationLogger")
            {
                relatedLogs = await _hubbleService.GetRelatedLogsAsync(log.RelatedRequestId);
                relatedLogs = relatedLogs.Where(rl => rl.Id != log.Id).ToList();
            }

            return new LogDetailApiResponse
            {
                Found = true,
                Log = log,
                RelatedLogs = relatedLogs
            };
        }
        catch (Exception)
        {
            return new LogDetailApiResponse
            {
                Found = false,
                Log = null,
                RelatedLogs = new List<GeneralLog>()
            };
        }
    }

    /// <summary>
    /// Deletes all logs and returns JSON response
    /// </summary>
    /// <returns>JSON response indicating success</returns>
    public async Task<DeleteApiResponse> DeleteAllLogsApiAsync()
    {
        try
        {
            // Get count before deleting
            var deletedCount = await _hubbleService.GetTotalLogsCountAsync();
            await _hubbleService.DeleteAllLogsAsync();
            
            return new DeleteApiResponse
            {
                Success = true,
                Message = "All logs have been deleted successfully",
                DeletedCount = deletedCount
            };
        }
        catch (Exception ex)
        {
            return new DeleteApiResponse
            {
                Success = false,
                Message = $"Error deleting logs: {ex.Message}",
                DeletedCount = 0
            };
        }
    }

    /// <summary>
    /// Gets configuration and statistics in JSON format
    /// </summary>
    /// <returns>JSON response with configuration and statistics</returns>
    public async Task<ConfigApiResponse> GetConfigurationApiAsync()
    {
        try
        {
            var response = new ConfigApiResponse
            {
                Options = new HubbleOptionsDto
                {
                    BasePath = _basePath,
                    PrefixPath = _prefixPath,
                    ServiceName = _options.ServiceName,
                    CaptureHttpRequests = _options.CaptureHttpRequests,
                    CaptureLoggerMessages = _options.CaptureLoggerMessages,
                    IgnorePaths = _options.IgnorePaths?.ToList() ?? new List<string>(),
                    Version = _version
                }
            };

            if (_statsService != null)
            {
                response.Statistics = await _statsService.GetStatisticsAsync();
                response.Configuration = await _statsService.GetSystemConfigurationAsync();
            }

            return response;
        }
        catch (Exception)
        {
            return new ConfigApiResponse
            {
                Statistics = null,
                Configuration = null,
                Options = new HubbleOptionsDto
                {
                    BasePath = _basePath,
                    PrefixPath = _prefixPath,
                    ServiceName = _options.ServiceName,
                    CaptureHttpRequests = _options.CaptureHttpRequests,
                    CaptureLoggerMessages = _options.CaptureLoggerMessages,
                    IgnorePaths = _options.IgnorePaths?.ToList() ?? new List<string>(),
                    Version = _version
                }
            };
        }
    }

    /// <summary>
    /// Runs manual prune operation and returns JSON response
    /// </summary>
    /// <returns>JSON response with prune results</returns>
    public async Task<PruneApiResponse> RunManualPruneApiAsync()
    {
        try
        {
            if (_statsService == null)
            {
                return new PruneApiResponse
                {
                    Success = false,
                    Message = "Statistics service is not available",
                    PrunedCount = 0,
                    CutoffDate = DateTime.UtcNow
                };
            }

            var config = await _statsService.GetSystemConfigurationAsync();
            var maxAgeHours = config.MaxLogAgeHours > 0 ? config.MaxLogAgeHours : 24;
            var cutoffDate = DateTime.UtcNow.AddHours(-maxAgeHours);

            var logsDeleted = await _hubbleService.DeleteLogsOlderThanAsync(cutoffDate);
            await _statsService.UpdatePruneStatisticsAsync(DateTime.UtcNow, logsDeleted);

            return new PruneApiResponse
            {
                Success = true,
                Message = $"Manual prune completed successfully. {logsDeleted} logs were deleted.",
                PrunedCount = logsDeleted,
                CutoffDate = cutoffDate
            };
        }
        catch (Exception ex)
        {
            return new PruneApiResponse
            {
                Success = false,
                Message = $"Error running manual prune: {ex.Message}",
                PrunedCount = 0,
                CutoffDate = DateTime.UtcNow
            };
        }
    }

    /// <summary>
    /// Recalculates statistics and returns JSON response
    /// </summary>
    /// <returns>JSON response indicating success</returns>
    public async Task<ApiResponse> RecalculateStatisticsApiAsync()
    {
        try
        {
            if (_statsService == null)
            {
                return new ApiResponse
                {
                    Success = false,
                    Message = "Statistics service is not available"
                };
            }

            await _statsService.RecalculateStatisticsAsync();

            return new ApiResponse
            {
                Success = true,
                Message = "Statistics recalculated successfully"
            };
        }
        catch (Exception ex)
        {
            return new ApiResponse
            {
                Success = false,
                Message = $"Error recalculating statistics: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Saves prune configuration and returns JSON response
    /// </summary>
    /// <param name="request">Prune configuration request</param>
    /// <returns>JSON response indicating success</returns>
    public async Task<ApiResponse> SavePruneConfigApiAsync(SavePruneConfigRequest request)
    {
        try
        {
            if (_statsService == null)
            {
                return new ApiResponse
                {
                    Success = false,
                    Message = "Statistics service is not available"
                };
            }

            var config = await _statsService.GetSystemConfigurationAsync();

            config.EnableDataPrune = request.EnableDataPrune;
            config.DataPruneIntervalHours = Math.Max(1, Math.Min(168, request.DataPruneIntervalHours)); // Between 1 and 168 hours
            config.MaxLogAgeHours = Math.Max(1, Math.Min(8760, request.MaxLogAgeHours)); // Between 1 hour and 1 year

            await _statsService.SaveSystemConfigurationAsync(config);

            return new ApiResponse
            {
                Success = true,
                Message = "Prune configuration saved successfully"
            };
        }
        catch (Exception ex)
        {
            return new ApiResponse
            {
                Success = false,
                Message = $"Error saving prune configuration: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Saves capture configuration and returns JSON response
    /// </summary>
    /// <param name="request">Capture configuration request</param>
    /// <returns>JSON response indicating success</returns>
    public async Task<ApiResponse> SaveCaptureConfigApiAsync(SaveCaptureConfigRequest request)
    {
        try
        {
            if (_statsService == null)
            {
                return new ApiResponse
                {
                    Success = false,
                    Message = "Statistics service is not available"
                };
            }

            var config = await _statsService.GetSystemConfigurationAsync();

            config.CaptureHttpRequests = request.CaptureHttpRequests;
            config.CaptureLoggerMessages = request.CaptureLoggerMessages;

            await _statsService.SaveSystemConfigurationAsync(config);

            return new ApiResponse
            {
                Success = true,
                Message = "Capture configuration saved successfully"
            };
        }
        catch (Exception ex)
        {
            return new ApiResponse
            {
                Success = false,
                Message = $"Error saving capture configuration: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Saves ignore paths configuration and returns JSON response
    /// </summary>
    /// <param name="request">Ignore paths request</param>
    /// <returns>JSON response indicating success</returns>
    public async Task<ApiResponse> SaveIgnorePathsApiAsync(SaveIgnorePathsRequest request)
    {
        try
        {
            if (_statsService == null)
            {
                return new ApiResponse
                {
                    Success = false,
                    Message = "Statistics service is not available"
                };
            }

            var config = await _statsService.GetSystemConfigurationAsync();
            config.IgnorePaths = request.IgnorePaths;

            await _statsService.SaveSystemConfigurationAsync(config);

            return new ApiResponse
            {
                Success = true,
                Message = "Ignore paths configuration saved successfully"
            };
        }
        catch (Exception ex)
        {
            return new ApiResponse
            {
                Success = false,
                Message = $"Error saving ignore paths configuration: {ex.Message}"
            };
        }
    }

    #endregion
}