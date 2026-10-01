namespace Gabonet.Hubble.UI;

using Gabonet.Hubble.Interfaces;
using Gabonet.Hubble.Models;
using Gabonet.Hubble.Security;
using Gabonet.Hubble.Services;
using Gabonet.Hubble.UI.Models;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
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
    private readonly IAntiforgery _antiforgery;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly HubbleStorageStatus? _storageStatus;
    private readonly TimeZoneInfo _timeZone;

    /// <summary>
    /// Constructor del controlador de Hubble.
    /// </summary>
    /// <param name="hubbleService">Servicio de Hubble</param>
    /// <param name="options">Opciones de configuración de Hubble</param>
    /// <param name="antiforgery">Servicio antiforgery para los formularios que modifican datos</param>
    /// <param name="httpContextAccessor">Acceso al contexto HTTP actual</param>
    /// <param name="statsService">Servicio de estadísticas</param>
    /// <param name="storageStatus">Estado de los índices y la retención en MongoDB</param>
    public HubbleController(
        IHubbleService hubbleService,
        Middleware.HubbleOptions options,
        IAntiforgery antiforgery,
        IHttpContextAccessor httpContextAccessor,
        IHubbleStatsService? statsService = null,
        HubbleStorageStatus? storageStatus = null)
    {
        _storageStatus = storageStatus;
        _hubbleService = hubbleService;
        _antiforgery = antiforgery;
        _httpContextAccessor = httpContextAccessor;
        _version = GetAssemblyVersion();
        _basePath = options.BasePath.TrimEnd('/');
        _prefixPath = options.PrefixPath.TrimEnd('/');
        _options = options;
        _statsService = statsService;
        _timeZone = ResolveTimeZone(options.TimeZoneId);
    }

    /// <summary>
    /// Zona horaria configurada para mostrar las fechas (UTC si no se indica o no existe).
    /// </summary>
    private static TimeZoneInfo ResolveTimeZone(string? timeZoneId)
    {
        if (string.IsNullOrEmpty(timeZoneId))
        {
            return TimeZoneInfo.Utc;
        }

        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }
        catch (Exception)
        {
            return TimeZoneInfo.Utc;
        }
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
        if (!string.IsNullOrEmpty(statusGroup) && int.TryParse(statusGroup, out int statusBase))
        {
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

        var root = $"{_prefixPath}{_basePath}";
        var totalPages = Math.Max(1, (int)Math.Ceiling((double)totalCount / pageSize));
        var hasFilters = !string.IsNullOrEmpty(method) || !string.IsNullOrEmpty(url) || !string.IsNullOrEmpty(statusGroup) || !string.IsNullOrEmpty(logType);

        var html = new StringBuilder(GenerateHtmlHeader("Hubble - Logs", true));
        html.Append(TopBar("logs"));
        html.Append($"<main class='container' id='logs-page' data-live-default='{(_options.HighlightNewServices ? "1" : "0")}' data-live-interval='{(_options.HighlightNewServices ? 3000 : 5000)}'>");

        // Encabezado de la página
        html.Append("<div class='page-head'><div><h1 class='page-h1'>Registros</h1>");
        html.Append("<p class='page-sub'>Solicitudes HTTP y mensajes de ILogger capturados por Hubble</p></div>");
        html.Append("<div class='page-head-actions'>");
        html.Append("<button type='button' class='btn small' id='live-toggle' aria-pressed='false' title='Actualizar la lista automáticamente (L)'><span class='live-dot'></span>En vivo</button>");
        html.Append($"<button type='button' class='btn small ghost' id='density-toggle' title='Alternar vista compacta'>{Icon("rows")}Compacta</button>");
        html.Append("</div></div>");

        html.Append(RenderListStats(logs, totalCount, pageSize, method, url, statusGroup, logType));
        html.Append(RenderFilters(root, method, url, statusGroup, logType, pageSize, hasFilters));

        // Resultados
        html.Append("<section class='panel results'>");
        html.Append("<div class='results-head'>");
        html.Append($"<div class='results-title' id='results-count'>Mostrando <b>{logs.Count}</b> de <b>{totalCount}</b> registros · página {page} de {totalPages}</div>");
        html.Append("<div class='results-actions'>");
        html.Append($"<button type='button' class='btn small ghost' id='export-csv' title='Exportar a CSV las filas visibles'>{Icon("download")}Exportar CSV</button>");

        // Mostrar el botón de eliminar todo solo si está permitido
        if (_options.AllowDeleteAll)
        {
            html.Append($"<form method='post' action='{root}/delete-all' onsubmit=\"return confirm('¿Está seguro que desea eliminar todos los logs? Esta acción no se puede deshacer.');\">");
            html.Append(AntiforgeryField());
            html.Append($"<button type='submit' class='btn small danger'>{Icon("trash")}Eliminar todos</button>");
            html.Append("</form>");
        }

        html.Append("</div></div>");

        html.Append("<div class='table-wrap'><table class='logs'>");
        html.Append("<thead><tr>");
        html.Append("<th class='sortable' data-sort='time'>Fecha<span class='sort-ind'></span></th>");
        html.Append("<th class='c-type'>Tipo</th>");
        html.Append("<th class='sortable h-method' data-sort='method'>Método<span class='sort-ind'></span></th>");
        html.Append("<th class='sortable' data-sort='url'>URL / Categoría<span class='sort-ind'></span></th>");
        html.Append("<th class='sortable' data-sort='status'>Estado<span class='sort-ind'></span></th>");
        html.Append("<th class='sortable' data-sort='ms'>Duración<span class='sort-ind'></span></th>");
        html.Append("<th class='ta-r'>Acciones</th>");
        html.Append("</tr></thead>");
        html.Append("<tbody id='logs-tbody'>");

        var maxDuration = Math.Max(1, logs.Where(l => !IsLoggerEntry(l)).Select(l => l.ExecutionTime).DefaultIfEmpty(0).Max());
        var highlightSeconds = _options.HighlightDurationSeconds > 0 ? _options.HighlightDurationSeconds : 15;
        var index = 0;

        foreach (var log in logs)
        {
            html.Append(RenderLogRow(log, root, index++, maxDuration, highlightSeconds));
        }

        if (logs.Count == 0)
        {
            html.Append("<tr><td colspan='7'><div class='empty'>");
            html.Append(Icon("inbox"));
            html.Append("<h3>No hay registros</h3>");
            html.Append(hasFilters
                ? $"<p>Ningún registro coincide con los filtros aplicados.</p><a class='btn small soft' href='{root}'>{Icon("x")}Limpiar filtros</a>"
                : "<p>Cuando tu aplicación reciba solicitudes aparecerán aquí.</p>");
            html.Append("</div></td></tr>");
        }

        html.Append($"<tr id='refine-empty' hidden><td colspan='7'><div class='empty'>{Icon("filter")}<h3>Sin coincidencias en esta página</h3><p>Prueba con otro criterio o quita el refinado.</p></div></td></tr>");
        html.Append("</tbody></table></div>");

        html.Append(RenderPager(page, totalPages, pageSize, totalCount, method, url, statusGroup, logType));
        html.Append("</section>");
        html.Append("</main>");

        html.Append(GenerateHtmlFooter());
        return html.ToString();
    }

    /// <summary>
    /// Tarjetas de resumen de la lista: total que coincide con el filtro y métricas de la página actual.
    /// </summary>
    private string RenderListStats(List<GeneralLog> logs, long totalCount, int pageSize, string? method, string? url, string? statusGroup, string? logType)
    {
        var httpLogs = logs.Where(l => !IsLoggerEntry(l)).ToList();
        var ok = httpLogs.Count(l => l.StatusCode >= 200 && l.StatusCode < 400);
        var clientErrors = httpLogs.Count(l => l.StatusCode >= 400 && l.StatusCode < 500);
        var serverErrors = httpLogs.Count(l => l.StatusCode >= 500);
        var durations = httpLogs.Select(l => l.ExecutionTime).OrderBy(d => d).ToList();
        var average = durations.Count > 0 ? (long)Math.Round(durations.Average()) : 0;
        var p95 = durations.Count > 0 ? durations[Math.Max(0, (int)Math.Ceiling(durations.Count * 0.95) - 1)] : 0;
        var slowest = httpLogs.OrderByDescending(l => l.ExecutionTime).FirstOrDefault();
        int Percent(int n) => httpLogs.Count == 0 ? 0 : (int)Math.Round(100.0 * n / httpLogs.Count);

        string StatusCard(string group, string icon, string color, string label, int count, string caption)
        {
            var active = statusGroup == group;
            var href = PageLink(1, pageSize, method, url, active ? null : group, logType);
            var title = active ? "Quitar el filtro de estado" : "Filtrar por este estado";
            return $"<a class='stat{(active ? " active" : "")}' style='--c:var(--{color})' href='{href}' title='{title}'>" +
                   $"<div class='stat-label'>{Icon(icon)}{label}</div>" +
                   $"<div class='stat-value'>{count}<small>{Percent(count)}%</small></div>" +
                   $"<div class='stat-sub'>{caption}</div>" +
                   $"<div class='meter'><span style='width:{Percent(count)}%'></span></div></a>";
        }

        var html = new StringBuilder("<section class='stats' id='stats'>");

        html.Append("<div class='stat' style='--c:var(--primary-2)'>");
        html.Append($"<div class='stat-label'>{Icon("layers")}Registros</div>");
        html.Append($"<div class='stat-value'>{totalCount}</div>");
        html.Append($"<div class='stat-sub'>{logs.Count} en esta página · {httpLogs.Count} HTTP · {logs.Count - httpLogs.Count} ILogger</div>");
        html.Append("</div>");

        html.Append(StatusCard("200", "check-circle", "green", "Correctas", ok, "2xx / 3xx en esta página"));
        html.Append(StatusCard("400", "alert", "amber", "Errores cliente", clientErrors, "4xx en esta página"));
        html.Append(StatusCard("500", "x-circle", "red", "Errores servidor", serverErrors, "5xx en esta página"));

        html.Append("<div class='stat' style='--c:var(--accent)'>");
        html.Append($"<div class='stat-label'>{Icon("clock")}Duración media</div>");
        html.Append($"<div class='stat-value'>{average}<small>ms</small></div>");
        html.Append($"<div class='stat-sub'>p95 · {p95} ms</div>");
        html.Append("</div>");

        if (slowest != null)
        {
            html.Append($"<a class='stat' style='--c:var(--pink)' href='{_prefixPath}{_basePath}/detail/{Uri.EscapeDataString(slowest.Id ?? string.Empty)}' title='Ver la solicitud más lenta'>");
            html.Append($"<div class='stat-label'>{Icon("zap")}Más lenta</div>");
            html.Append($"<div class='stat-value'>{slowest.ExecutionTime}<small>ms</small></div>");
            html.Append($"<div class='stat-sub mono'>{E(slowest.Method)} {E(slowest.HttpUrl)}</div>");
            html.Append("</a>");
        }
        else
        {
            html.Append($"<div class='stat' style='--c:var(--pink)'><div class='stat-label'>{Icon("zap")}Más lenta</div><div class='stat-value'>—</div><div class='stat-sub'>Sin solicitudes HTTP</div></div>");
        }

        html.Append("</section>");
        return html.ToString();
    }

    /// <summary>
    /// Panel de filtros: búsqueda en servidor, filtros rápidos, refinado en cliente y búsquedas guardadas.
    /// </summary>
    private static string RenderFilters(string root, string? method, string? url, string? statusGroup, string? logType, int pageSize, bool hasFilters)
    {
        var html = new StringBuilder("<section class='panel filters'>");
        html.Append($"<form method='get' action='{root}' id='filter-form' class='filter-form'>");

        // Búsqueda por URL, tipo y tamaño de página
        html.Append("<div class='filter-row'>");
        html.Append($"<label class='field grow'>{Icon("search")}<input class='input' type='search' name='url' placeholder='Buscar por URL…  ej: /api/orders' value='{E(url)}' data-search-focus autocomplete='off' spellcheck='false' aria-label='Buscar por URL'><kbd>/</kbd></label>");

        html.Append("<select name='logType' class='select' data-autosubmit aria-label='Tipo de registro'>");
        foreach (var (value, label) in new[] { ("", "Todos los tipos"), ("HTTP", "Solo HTTP"), ("ApplicationLogger", "Solo ILogger") })
        {
            html.Append($"<option value='{value}'{(logType == value || (value == "" && string.IsNullOrEmpty(logType)) ? " selected" : "")}>{label}</option>");
        }
        html.Append("</select>");

        html.Append("<select name='pageSize' class='select' data-autosubmit aria-label='Registros por página'>");
        foreach (var size in new[] { 25, 50, 100, 200 })
        {
            html.Append($"<option value='{size}'{(pageSize == size ? " selected" : "")}>{size} por página</option>");
        }
        if (!new[] { 25, 50, 100, 200 }.Contains(pageSize))
        {
            html.Append($"<option value='{pageSize}' selected>{pageSize} por página</option>");
        }
        html.Append("</select>");

        html.Append($"<button type='submit' class='btn primary'>{Icon("search")}Buscar</button>");
        if (hasFilters)
        {
            html.Append($"<a class='btn ghost' href='{root}'>{Icon("x")}Limpiar</a>");
        }
        html.Append("</div>");

        // Filtros rápidos por método y estado
        html.Append("<div class='filter-row'>");
        html.Append("<span class='filter-label'>Método</span><div class='chips'>");
        html.Append(RadioChip("method", "", "Todos", "all", string.IsNullOrEmpty(method)));
        foreach (var httpMethod in new[] { "GET", "POST", "PUT", "PATCH", "DELETE", "OPTIONS", "HEAD" })
        {
            html.Append(RadioChip("method", httpMethod, httpMethod, MethodClass(httpMethod), method == httpMethod));
        }
        html.Append("</div><span class='vsep'></span>");
        html.Append("<span class='filter-label'>Estado</span><div class='chips'>");
        html.Append(RadioChip("statusGroup", "", "Todos", "all", string.IsNullOrEmpty(statusGroup)));
        foreach (var (group, label) in new[] { ("200", "2xx Éxito"), ("300", "3xx Redirección"), ("400", "4xx Cliente"), ("500", "5xx Servidor") })
        {
            html.Append(RadioChip("statusGroup", group, label, $"s-{group[0]}", statusGroup == group));
        }
        html.Append("</div></div>");
        html.Append("</form>");

        // Búsqueda avanzada sobre los resultados de la página actual (en el navegador)
        html.Append("<div class='filter-row bordered'>");
        html.Append($"<label class='field grow'>{Icon("filter")}<input class='input mono' id='refine' type='text' placeholder='Refinar esta página…  status:5xx  ms>500  -method:OPTIONS  \"texto exacto\"' autocomplete='off' spellcheck='false' aria-label='Refinar resultados'><kbd>F</kbd></label>");
        html.Append("<span class='refine-count' id='refine-count'></span>");
        html.Append("<div class='chips'>");
        foreach (var (token, label, css) in new[]
                 {
                     ("is:error", "Con error", "s-5"),
                     ("ms>1000", "Lentas > 1 s", "s-4"),
                     ("type:http", "HTTP", "m-get"),
                     ("type:log", "ILogger", "l-information"),
                     ("is:new", "Nuevas", "m-patch")
                 })
        {
            html.Append($"<button type='button' class='chip {css}' data-token='{token}'><span class='dot'></span>{label}</button>");
        }
        html.Append("</div>");
        html.Append($"<button type='button' class='btn small ghost' id='syntax-toggle' title='Ver la sintaxis de búsqueda'>{Icon("info")}Sintaxis</button>");
        html.Append($"<button type='button' class='btn small ghost icon' id='refine-clear' title='Quitar el refinado'>{Icon("x")}</button>");
        html.Append("</div>");

        html.Append("<div class='syntax-help' id='syntax-help' hidden>");
        foreach (var (example, description) in new[]
                 {
                     ("status:5xx", "familia de estado (o exacto: status:404)"),
                     ("status>=400", "comparaciones de estado"),
                     ("method:POST", "método HTTP"),
                     ("ms>500", "duración en ms (>, >=, <, <=)"),
                     ("type:log", "tipo: http o log (ILogger)"),
                     ("level:warning", "nivel del log de ILogger"),
                     ("url:/api/orders", "la URL contiene el texto"),
                     ("is:error", "también is:ok, is:slow, is:new"),
                     ("-health", "excluir lo que contenga el término"),
                     ("\"texto exacto\"", "frase literal en cualquier columna")
                 })
        {
            html.Append($"<div><code>{E(example)}</code> {description}</div>");
        }
        html.Append("</div>");

        // Búsquedas guardadas (en el navegador)
        html.Append("<div class='filter-row bordered'>");
        html.Append($"<span class='filter-label'>{Icon("bookmark")}Guardadas</span>");
        html.Append("<div class='chips' id='saved-list'></div>");
        html.Append($"<button type='button' class='btn small soft' id='save-search'>{Icon("plus")}Guardar búsqueda</button>");
        html.Append($"<span class='save-form' id='save-form' hidden><input class='input sm' id='save-name' maxlength='80' placeholder='Nombre de la búsqueda' aria-label='Nombre de la búsqueda'><button type='button' class='btn small primary' id='save-confirm'>Guardar</button><button type='button' class='btn small ghost icon' id='save-cancel' title='Cancelar'>{Icon("x")}</button></span>");
        html.Append("</div>");

        html.Append("</section>");
        return html.ToString();
    }

    /// <summary>
    /// Chip de selección única que envía el formulario de filtros al cambiar.
    /// </summary>
    private static string RadioChip(string name, string value, string label, string css, bool active)
    {
        return $"<label class='chip {css}{(active ? " active" : "")}'><input type='radio' name='{name}' value='{E(value)}'{(active ? " checked" : "")} data-autosubmit><span class='dot'></span>{E(label)}</label>";
    }

    /// <summary>
    /// Fila de la tabla de logs. Los atributos data-* alimentan la búsqueda avanzada, la ordenación y la exportación.
    /// </summary>
    private string RenderLogRow(GeneralLog log, string root, int index, long maxDuration, int highlightSeconds)
    {
        var isLogger = IsLoggerEntry(log);
        var detailUrl = $"{root}/detail/{Uri.EscapeDataString(log.Id ?? string.Empty)}";
        var age = AgeSeconds(log.Timestamp);
        var isNew = _options.HighlightNewServices && Math.Abs(age) <= highlightSeconds;
        var (level, _) = isLogger ? ParseLoggerAction(log.ActionName) : (string.Empty, string.Empty);
        var isError = log.IsError || (!isLogger && log.StatusCode >= 400);
        var rowFamily = isLogger ? (isError ? 5 : level == "Warning" ? 4 : 0) : StatusFamily(log.StatusCode);
        var fullUrl = log.HttpUrl + (HasQuery(log.QueryParams) ? log.QueryParams : string.Empty);
        var time = log.Timestamp.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

        var html = new StringBuilder();
        html.Append($"<tr class='row st-{rowFamily}{(isNew ? " new-service" : "")}' data-href='{E(detailUrl)}' data-id='{E(log.Id)}' data-idx='{index}' ");
        html.Append($"data-ticks='{log.Timestamp.Ticks}' data-ts='{time}' data-type='{(isLogger ? "log" : "http")}' data-level='{E(level)}' ");
        html.Append($"data-method='{E(log.Method)}' data-status='{log.StatusCode}' data-ms='{log.ExecutionTime}' data-url='{E(fullUrl)}' data-error='{(isError ? 1 : 0)}'>");

        html.Append($"<td class='c-time'><div class='t-rel' title='{time}'><span data-age='{age.ToString("0", CultureInfo.InvariantCulture)}'>{FormatAgo(age)}</span>{(isNew ? "<span class='new-tag'>NUEVO</span>" : "")}</div><div class='t-abs'>{time}</div></td>");
        html.Append($"<td class='c-type'>{(isLogger ? LevelBadge(level) : "<span class='badge plain m-other'>HTTP</span>")}</td>");
        html.Append($"<td class='c-method'>{MethodBadge(log.Method)}</td>");

        html.Append("<td class='c-url'>");
        if (isLogger)
        {
            // RequestData contiene la categoría del logger y ResponseData el mensaje
            html.Append($"<div class='u-cat' title='{E(log.RequestData)}'>{E(log.RequestData)}</div>");
            html.Append($"<div class='u-msg' title='{E(Truncate(log.ResponseData, 600))}'>{E(Truncate(log.ResponseData, 240))}</div>");
        }
        else
        {
            html.Append($"<div class='u-path' title='{E(log.HttpUrl)}'>{E(log.HttpUrl)}</div>");
            if (HasQuery(log.QueryParams))
            {
                html.Append($"<span class='u-q' title='{E(log.QueryParams)}'>{E(log.QueryParams)}</span>");
            }
        }
        html.Append("</td>");

        html.Append($"<td class='c-status'>{(isLogger ? "<span class='muted'>—</span>" : StatusPill(log.StatusCode))}</td>");

        if (isLogger)
        {
            html.Append("<td class='c-dur'><span class='muted'>—</span></td>");
        }
        else
        {
            var width = Math.Max(2, (int)Math.Round(100.0 * log.ExecutionTime / maxDuration));
            html.Append($"<td class='c-dur'><div class='dur {DurationClass(log.ExecutionTime)}'><span class='dur-v'>{log.ExecutionTime} ms</span><span class='dur-bar'><i style='width:{width}%'></i></span></div></td>");
        }

        html.Append("<td class='c-act'><div class='row-actions'>");
        html.Append(CopyButton(fullUrl, "Copiar URL", "ghost reveal"));
        html.Append($"<a class='btn icon small ghost reveal' href='{E(detailUrl)}' target='_blank' rel='noopener' title='Abrir en una pestaña nueva'>{Icon("external")}</a>");
        html.Append($"<a class='btn small soft' href='{E(detailUrl)}'>Ver{Icon("chevron-right")}</a>");
        html.Append("</div></td></tr>");

        return html.ToString();
    }

    /// <summary>
    /// Paginación con accesos a primera/última página y salto directo.
    /// </summary>
    private static string RenderPager(int page, int totalPages, int pageSize, long totalCount, string? method, string? url, string? statusGroup, string? logType)
    {
        string Link(int target, string content, string title, string extra = "") =>
            $"<a href='{PageLink(target, pageSize, method, url, statusGroup, logType)}' class='page-btn' title='{title}'{extra}>{content}</a>";
        string Disabled(string content, string title) => $"<span class='page-btn disabled' title='{title}'>{content}</span>";

        var html = new StringBuilder("<div class='pager' id='pager'>");
        html.Append($"<div class='pager-info'>Página <b>{page}</b> de <b>{totalPages}</b> · {totalCount} registros en total</div>");
        html.Append("<div class='pager-nav'>");

        html.Append(page > 1 ? Link(1, Icon("chevrons-left"), "Primera página") : Disabled(Icon("chevrons-left"), "Primera página"));
        html.Append(page > 1 ? Link(page - 1, Icon("chevron-left"), "Página anterior (←)", " data-page-prev") : Disabled(Icon("chevron-left"), "Página anterior"));

        const int pagesToShow = 5;
        var startPage = Math.Max(1, page - pagesToShow / 2);
        var endPage = Math.Min(totalPages, startPage + pagesToShow - 1);
        if (endPage == totalPages)
        {
            startPage = Math.Max(1, endPage - pagesToShow + 1);
        }

        if (startPage > 1)
        {
            html.Append(Link(1, "1", "Página 1"));
            if (startPage > 2)
            {
                html.Append("<span class='page-ellipsis'>…</span>");
            }
        }

        for (var i = startPage; i <= endPage; i++)
        {
            html.Append(i == page ? $"<span class='page-btn current'>{i}</span>" : Link(i, i.ToString(CultureInfo.InvariantCulture), $"Página {i}"));
        }

        if (endPage < totalPages)
        {
            if (endPage < totalPages - 1)
            {
                html.Append("<span class='page-ellipsis'>…</span>");
            }
            html.Append(Link(totalPages, totalPages.ToString(CultureInfo.InvariantCulture), $"Página {totalPages}"));
        }

        html.Append(page < totalPages ? Link(page + 1, Icon("chevron-right"), "Página siguiente (→)", " data-page-next") : Disabled(Icon("chevron-right"), "Página siguiente"));
        html.Append(page < totalPages ? Link(totalPages, Icon("chevrons-right"), "Última página") : Disabled(Icon("chevrons-right"), "Última página"));

        if (totalPages > 1)
        {
            html.Append("<form method='get' class='page-jump'>");
            foreach (var (name, value) in new[] { ("method", method), ("url", url), ("statusGroup", statusGroup), ("logType", logType) })
            {
                if (!string.IsNullOrEmpty(value))
                {
                    html.Append($"<input type='hidden' name='{name}' value='{E(value)}'>");
                }
            }
            html.Append($"<input type='hidden' name='pageSize' value='{pageSize}'>");
            html.Append($"<span>Ir a</span><input class='input sm' type='number' name='page' min='1' max='{totalPages}' value='{page}' aria-label='Ir a la página'>");
            html.Append("</form>");
        }

        html.Append("</div></div>");
        return html.ToString();
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

        var isLogger = IsLoggerEntry(log);

        // Logs relacionados (logs de ILogger asociados a esta solicitud)
        var relatedLogs = isLogger
            ? new List<GeneralLog>()
            : await _hubbleService.GetRelatedLogsAsync(log.Id ?? string.Empty);

        var root = $"{_prefixPath}{_basePath}";
        var timestamp = ToDisplayTime(log.Timestamp);
        var age = AgeSeconds(log.Timestamp);
        var hasQuery = HasQuery(log.QueryParams);
        var fullUrl = log.HttpUrl + (hasQuery ? log.QueryParams : string.Empty);
        var hasError = log.IsError || !string.IsNullOrEmpty(log.ErrorMessage) || !string.IsNullOrEmpty(log.StackTrace);
        var family = StatusFamily(log.StatusCode);
        var (level, source) = isLogger ? ParseLoggerAction(log.ActionName) : (string.Empty, string.Empty);
        var headers = ParseHeaders(log.RequestHeaders);
        var apiUrl = $"{root}/api/logs/{Uri.EscapeDataString(log.Id ?? string.Empty)}";

        var html = new StringBuilder(GenerateHtmlHeader(isLogger ? "Hubble - Log de aplicación" : $"Hubble - {log.Method} {log.HttpUrl}", true));
        html.Append(TopBar("logs"));
        html.Append($"<main class='container' id='detail-page' data-id='{E(log.Id)}'>");

        html.Append($"<nav class='crumbs'><a href='{root}' data-back title='Volver a la lista (B)'>{Icon("arrow-left")}Registros</a><span>/</span><span>{(isLogger ? "Log de aplicación" : "Solicitud HTTP")}</span><span class='id'>#{E(log.Id)}</span></nav>");

        // Cabecera con lo esencial del log
        var heroClass = hasError || family == 5 ? " is-error" : family == 4 || level == "Warning" ? " is-warn" : string.Empty;
        html.Append($"<section class='hero{heroClass}'>");
        html.Append("<div class='hero-top'><div class='hero-badges'>");
        if (isLogger)
        {
            html.Append(LevelBadge(level));
            html.Append("<span class='badge pill plain m-other'>ILogger</span>");
        }
        else
        {
            html.Append(MethodBadge(log.Method, true));
            html.Append(StatusPill(log.StatusCode, true));
            html.Append($"<span class='badge pill lg {DurationColor(log.ExecutionTime)}'>{Icon("clock")}{log.ExecutionTime} ms</span>");
        }
        if (hasError)
        {
            html.Append($"<span class='badge pill lg plain s-5'>{Icon("alert")}Error</span>");
        }
        html.Append($"<span class='badge pill lg plain s-0' title='{timestamp:yyyy-MM-dd HH:mm:ss}'>{Icon("calendar")}<span data-age='{age.ToString("0", CultureInfo.InvariantCulture)}'>{FormatAgo(age)}</span></span>");
        html.Append("</div>");

        html.Append("<div class='hero-actions'>");
        if (!isLogger)
        {
            html.Append($"<button type='button' class='btn small' data-copy='{E(fullUrl)}' title='Copiar la URL con sus parámetros'>{Icon("copy")}{Icon("check")}Copiar URL</button>");
            html.Append($"<button type='button' class='btn small' data-copy-target='#curl-src' title='Copiar la solicitud como comando cURL'>{Icon("terminal")}{Icon("check")}cURL</button>");
        }
        else
        {
            html.Append($"<button type='button' class='btn small' data-copy='{E(log.ResponseData)}'>{Icon("copy")}{Icon("check")}Copiar mensaje</button>");
        }
        html.Append($"<button type='button' class='btn small' id='download-json' data-api='{E(apiUrl)}' title='Descargar el log completo en JSON'>{Icon("download")}JSON</button>");
        html.Append($"<a class='btn small ghost' href='{E(apiUrl)}' target='_blank' rel='noopener' title='Abrir el log en la API de Hubble'>{Icon("code")}API</a>");
        html.Append("</div></div>");

        if (isLogger)
        {
            html.Append($"<div class='hero-msg'>{E(log.ResponseData)}</div>");
        }
        else
        {
            html.Append("<div class='hero-url'>");
            html.Append($"<div class='hero-url-text'>{E(log.HttpUrl)}{(hasQuery ? $"<span class='q'>{E(log.QueryParams)}</span>" : string.Empty)}</div>");
            html.Append(CopyButton(fullUrl, "Copiar URL"));
            html.Append("</div>");
        }

        html.Append("<div class='meta-grid'>");
        html.Append(Meta("calendar", "Fecha/Hora", timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture), copy: true, mono: true));
        if (isLogger)
        {
            html.Append(Meta("layers", "Categoría", log.RequestData, copy: true, mono: true));
            html.Append(Meta("code", "Origen", source, copy: true, mono: true));
            if (HasRequestContext(log))
            {
                html.Append(Meta("globe", "Solicitud", $"{log.Method} {log.HttpUrl}", copy: true, mono: true));
            }
        }
        else
        {
            html.Append(Meta("box", "Controlador", log.ControllerName, copy: true));
            html.Append(Meta("play", "Acción", log.ActionName, copy: true));
        }
        html.Append(Meta("globe", "IP del cliente", log.IpAddress, copy: true, mono: true));
        html.Append(Meta("server", "Servicio", log.ServiceName));
        html.Append(Meta("hash", "ID", log.Id, copy: true, mono: true));
        if (!string.IsNullOrEmpty(log.RelatedRequestId))
        {
            html.Append(Meta("request", "Solicitud HTTP", log.RelatedRequestId, mono: true,
                rawHtml: $"<a href='{root}/detail/{Uri.EscapeDataString(log.RelatedRequestId)}'>Ver solicitud {Icon("chevron-right")}</a>"));
        }
        html.Append("</div>");
        html.Append("</section>");

        if (!isLogger)
        {
            html.Append($"<pre id='curl-src' hidden>{E(BuildCurl(log, headers))}</pre>");
        }

        // Pestañas
        var tabs = new List<(string Key, string Icon, string Label, string? Count, bool IsError)>();
        if (isLogger)
        {
            tabs.Add(("message", "file", "Mensaje", null, false));
        }
        else
        {
            tabs.Add(("summary", "activity", "Resumen", null, false));
            tabs.Add(("request", "request", "Solicitud", headers?.Count.ToString(CultureInfo.InvariantCulture), false));
            tabs.Add(("response", "response", "Respuesta", null, false));
        }
        if (hasError)
        {
            tabs.Add(("error", "alert", "Error", null, true));
        }
        if (!isLogger)
        {
            tabs.Add(("database", "database", "Base de datos", log.DatabaseQueries.Count.ToString(CultureInfo.InvariantCulture), false));
            tabs.Add(("logs", "file", "Logs", relatedLogs.Count.ToString(CultureInfo.InvariantCulture), relatedLogs.Any(l => l.IsError)));
        }

        html.Append("<div id='tabs-anchor'></div><div class='tabs' role='tablist'>");
        for (var i = 0; i < tabs.Count; i++)
        {
            var tab = tabs[i];
            html.Append($"<button type='button' class='tab{(tab.IsError ? " tab-error" : "")}' role='tab' data-tab='{tab.Key}' aria-selected='false' title='{tab.Label} ({i + 1})'>");
            html.Append(Icon(tab.Icon));
            html.Append(tab.Label);
            if (tab.Count != null)
            {
                html.Append($"<span class='count'>{tab.Count}</span>");
            }
            html.Append($"<span class='key'>{i + 1}</span></button>");
        }
        html.Append("</div>");

        if (isLogger)
        {
            html.Append(RenderLoggerMessagePanel(log, level, source));
        }
        else
        {
            html.Append(RenderSummaryPanel(log, relatedLogs, headers, hasError));
            html.Append(RenderRequestPanel(log, headers));
            html.Append(RenderResponsePanel(log));
        }

        if (hasError)
        {
            html.Append(RenderErrorPanel(log));
        }

        if (!isLogger)
        {
            html.Append(RenderDatabasePanel(log));
            html.Append(RenderRelatedLogsPanel(log, relatedLogs, root));
        }

        html.Append("</main>");
        html.Append(GenerateHtmlFooter());

        return html.ToString();
    }

    private string RenderSummaryPanel(GeneralLog log, List<GeneralLog> relatedLogs, List<KeyValuePair<string, string>>? headers, bool hasError)
    {
        var dbTime = log.DatabaseQueries.Sum(q => q.ExecutionTime);
        var total = log.ExecutionTime;
        var requestBytes = Encoding.UTF8.GetByteCount(log.RequestData ?? string.Empty);
        var responseBytes = Encoding.UTF8.GetByteCount(log.ResponseData ?? string.Empty);
        var warnings = relatedLogs.Count(l => ParseLoggerAction(l.ActionName).Level == "Warning");
        var errors = relatedLogs.Count(l => l.IsError);

        string Mini(string tab, string icon, string color, string label, string value) =>
            $"<button type='button' class='mini' style='--c:var(--{color})' data-goto='{tab}'><span class='mini-ico'>{Icon(icon)}</span><span class='mini-text'><span class='mini-k'>{label}</span><span class='mini-v'>{value}</span></span></button>";

        var html = new StringBuilder("<div class='tab-panel' id='panel-summary' role='tabpanel'>");

        if (hasError)
        {
            html.Append($"<div class='alert'>{Icon("alert")}<div><div class='alert-title'>La solicitud terminó con error</div><div class='alert-text'>{E(Truncate(log.ErrorMessage ?? "Error sin mensaje", 600))}</div></div>");
            html.Append("<button type='button' class='btn small danger' data-goto='error'>Ver detalle</button></div>");
        }

        html.Append("<div class='mini-stats'>");
        html.Append(Mini("summary", "clock", DurationVar(total), "Duración", $"{total}<small>ms</small>"));
        html.Append(Mini("database", "database", "accent", "Consultas BD", $"{log.DatabaseQueries.Count}<small>· {dbTime} ms</small>"));
        html.Append(Mini("logs", "file", errors > 0 ? "red" : warnings > 0 ? "amber" : "primary-2", "Logs", $"{relatedLogs.Count}{(errors + warnings > 0 ? $"<small>· {errors} err · {warnings} warn</small>" : "")}"));
        html.Append(Mini("request", "request", "green", "Cuerpo solicitud", FormatBytes(requestBytes)));
        html.Append(Mini("response", "response", "blue", "Cuerpo respuesta", FormatBytes(responseBytes)));
        html.Append(Mini("request", "list", "violet", "Cabeceras", (headers?.Count ?? 0).ToString(CultureInfo.InvariantCulture)));
        html.Append("</div>");

        html.Append("<div class='grid-2'>");

        // Desglose del tiempo de la solicitud
        html.Append($"<section class='section'><div class='section-head'><div class='section-title'>{Icon("clock")}Desglose de tiempo</div><span class='code-meta'>{total} ms en total</span></div><div class='section-body timing'>");
        if (total > 0)
        {
            var dbShare = Math.Min(100, (int)Math.Round(100.0 * dbTime / total));
            var appTime = Math.Max(0, total - dbTime);
            html.Append($"<div class='tbar'><span class='t-db' style='width:{dbShare}%' title='Base de datos'></span><span class='t-app' style='width:{100 - dbShare}%' title='Aplicación'></span></div>");
            html.Append("<div class='legend'>");
            html.Append($"<span><i style='background:#22d3ee'></i>Base de datos <b>{dbTime} ms</b> ({dbShare}%)</span>");
            html.Append($"<span><i style='background:#a78bfa'></i>Aplicación y red <b>{appTime} ms</b> ({100 - dbShare}%)</span>");
            html.Append("</div>");
        }
        else
        {
            html.Append("<div class='muted'>No se registró la duración de esta solicitud.</div>");
        }

        var slowestQuery = log.DatabaseQueries.OrderByDescending(q => q.ExecutionTime).FirstOrDefault();
        if (slowestQuery != null)
        {
            html.Append($"<div class='info-note'>{Icon("database")}<span>Consulta más lenta: <b>{E(slowestQuery.OperationType ?? "QUERY")}</b> {E(slowestQuery.TableName)} · <b>{slowestQuery.ExecutionTime} ms</b></span><button type='button' class='btn small ghost' data-goto='database' style='margin-left:auto'>Ver consultas</button></div>");
        }
        html.Append("</div></section>");

        // Actividad de logs por nivel
        html.Append($"<section class='section'><div class='section-head'><div class='section-title'>{Icon("file")}Actividad de logs</div><span class='code-meta'>{relatedLogs.Count} mensajes</span></div><div class='section-body'>");
        if (relatedLogs.Count == 0)
        {
            html.Append("<div class='muted'>Esta solicitud no generó mensajes de ILogger.</div>");
        }
        else
        {
            html.Append("<div class='level-grid'>");
            foreach (var group in relatedLogs.GroupBy(l => ParseLoggerAction(l.ActionName).Level).OrderBy(g => LevelOrder(g.Key)))
            {
                html.Append($"<button type='button' class='level-card {LevelClass(group.Key)}' data-goto='logs' data-level='{E(group.Key)}'><span class='lv-k'><span class='dot'></span>{E(group.Key)}</span><span class='lv-v'>{group.Count()}</span></button>");
            }
            html.Append("</div>");
        }
        html.Append("</div></section>");

        html.Append("</div>");

        // Cabeceras más consultadas
        var keyHeaders = new[] { "User-Agent", "Content-Type", "Accept", "Origin", "Referer", "Authorization", "X-Forwarded-For", "Accept-Language" };
        var highlighted = headers?
            .Where(h => keyHeaders.Contains(h.Key, StringComparer.OrdinalIgnoreCase))
            .OrderBy(h => Array.FindIndex(keyHeaders, k => k.Equals(h.Key, StringComparison.OrdinalIgnoreCase)))
            .ToList();
        if (highlighted != null && highlighted.Count > 0)
        {
            html.Append($"<section class='section' style='margin-top:16px'><div class='section-head'><div class='section-title'>{Icon("list")}Cabeceras destacadas</div><button type='button' class='btn small ghost' data-goto='request'>Ver todas</button></div>");
            html.Append(KeyValueTable(highlighted, null));
            html.Append("</section>");
        }

        html.Append("</div>");
        return html.ToString();
    }

    private static string RenderRequestPanel(GeneralLog log, List<KeyValuePair<string, string>>? headers)
    {
        var html = new StringBuilder("<div class='tab-panel' id='panel-request' role='tabpanel'><div class='grid-2'><div class='vstack'>");

        // Parámetros de la URL
        var queryParams = ParseQueryParams(log.QueryParams);
        html.Append($"<section class='section'><div class='section-head'><div class='section-title'>{Icon("filter")}Parámetros de consulta<span class='count'>{queryParams.Count}</span></div>");
        if (queryParams.Count > 0)
        {
            html.Append($"<div class='section-tools'><button type='button' class='btn small ghost' data-copy='{E(log.QueryParams)}'>{Icon("copy")}{Icon("check")}Copiar</button></div>");
        }
        html.Append("</div>");
        html.Append(queryParams.Count > 0 ? KeyValueTable(queryParams, "query-table") : "<div class='kv-empty'>La solicitud no tiene parámetros de consulta.</div>");
        html.Append("</section>");

        // Cabeceras
        html.Append($"<section class='section'><div class='section-head'><div class='section-title'>{Icon("list")}Cabeceras<span class='count'>{headers?.Count ?? 0}</span></div>");
        if (headers != null && headers.Count > 0)
        {
            html.Append("<div class='section-tools'>");
            html.Append("<input class='input sm tool-search' type='search' placeholder='Filtrar cabeceras…' data-filter-rows='#headers-table' aria-label='Filtrar cabeceras'>");
            html.Append($"<button type='button' class='btn small ghost' data-copy-target='#headers-raw'>{Icon("copy")}{Icon("check")}Copiar JSON</button>");
            html.Append("</div></div>");
            html.Append(KeyValueTable(headers, "headers-table"));
            html.Append($"<pre id='headers-raw' hidden>{E(FormatJson(log.RequestHeaders ?? string.Empty))}</pre>");
        }
        else if (headers == null)
        {
            html.Append("</div>");
            html.Append($"<pre class='code small wrap' data-lang='text'>{E(log.RequestHeaders)}</pre>");
        }
        else
        {
            html.Append("</div><div class='kv-empty'>No se capturaron cabeceras.</div>");
        }
        html.Append("</section>");
        html.Append("</div>");

        // Cuerpo de la solicitud
        html.Append(CodeSection("request-body", "Cuerpo de la solicitud", "request", log.RequestData, $"request-{log.Id}"));

        html.Append("</div></div>");
        return html.ToString();
    }

    private static string RenderResponsePanel(GeneralLog log)
    {
        return "<div class='tab-panel' id='panel-response' role='tabpanel'>" +
               CodeSection("response-body", $"Cuerpo de la respuesta · {log.StatusCode}", "response", log.ResponseData, $"response-{log.Id}") +
               "</div>";
    }

    private static string RenderErrorPanel(GeneralLog log)
    {
        var html = new StringBuilder("<div class='tab-panel' id='panel-error' role='tabpanel'>");

        html.Append($"<section class='section error-section'><div class='section-head'><div class='section-title'>{Icon("alert")}Mensaje de error</div>");
        if (!string.IsNullOrEmpty(log.ErrorMessage))
        {
            html.Append($"<div class='section-tools'><button type='button' class='btn small ghost' data-copy='{E(log.ErrorMessage)}'>{Icon("copy")}{Icon("check")}Copiar</button></div>");
        }
        html.Append("</div>");
        html.Append($"<div class='section-body'><div class='alert-text'>{(string.IsNullOrEmpty(log.ErrorMessage) ? "Sin mensaje de error." : E(log.ErrorMessage))}</div></div>");
        html.Append("</section>");

        if (!string.IsNullOrEmpty(log.StackTrace))
        {
            html.Append($"<section class='section'><div class='section-head'><div class='section-title'>{Icon("layers")}Stack trace</div><div class='section-tools'>");
            html.Append($"<button type='button' class='btn small ghost' data-toggle-fw='stack-trace' title='Ocultar los frames de System.* y Microsoft.*'>{Icon("eye")}Solo mi código</button>");
            html.Append($"<button type='button' class='btn small ghost' data-copy-target='#stack-raw'>{Icon("copy")}{Icon("check")}Copiar</button>");
            html.Append("</div></div>");
            html.Append(RenderStackTrace(log.StackTrace, "stack-trace"));
            html.Append($"<pre id='stack-raw' hidden>{E(log.StackTrace)}</pre>");
            html.Append("</section>");
        }

        html.Append("</div>");
        return html.ToString();
    }

    private static string RenderDatabasePanel(GeneralLog log)
    {
        var queries = log.DatabaseQueries;
        var html = new StringBuilder("<div class='tab-panel' id='panel-database' role='tabpanel'>");

        if (queries.Count == 0)
        {
            html.Append($"<section class='section'><div class='empty'>{Icon("database")}<h3>Sin consultas</h3><p>No se registraron consultas a bases de datos durante esta solicitud.</p></div></section>");
            html.Append("</div>");
            return html.ToString();
        }

        var totalTime = queries.Sum(q => q.ExecutionTime);
        var maxTime = Math.Max(1, queries.Max(q => q.ExecutionTime));
        var allSql = string.Join(";\n\n", queries.Select(q => q.Query));

        html.Append($"<section class='section'><div class='section-head'><div class='section-title'>{Icon("database")}Consultas<span class='count'>{queries.Count}</span><span class='code-meta'>· {totalTime} ms en total</span></div>");
        html.Append("<div class='section-tools'>");
        html.Append($"<button type='button' class='btn small ghost' id='query-sort' title='Ordenar por duración'>{Icon("zap")}Más lentas primero</button>");
        html.Append($"<button type='button' class='btn small ghost' data-copy='{E(allSql)}'>{Icon("copy")}{Icon("check")}Copiar todas</button>");
        html.Append("</div></div>");

        html.Append("<div id='query-list'>");
        for (var i = 0; i < queries.Count; i++)
        {
            var query = queries[i];
            var operation = string.IsNullOrEmpty(query.OperationType) ? "QUERY" : query.OperationType!.ToUpperInvariant();
            var width = Math.Max(2, (int)Math.Round(100.0 * query.ExecutionTime / maxTime));

            html.Append($"<div class='q-item{(query.IsSuccess ? "" : " failed")}' data-ms='{query.ExecutionTime}'>");
            html.Append("<div class='q-head'>");
            html.Append($"<span class='q-idx'>#{i + 1}</span>");
            html.Append($"<span class='badge {OperationClass(operation)}'>{E(operation)}</span>");
            html.Append("<span class='q-meta'>");
            html.Append($"<span>{Icon("database")}{E(query.DatabaseType)} · {E(query.DatabaseName)}</span>");
            if (!string.IsNullOrEmpty(query.TableName))
            {
                html.Append($"<span>{Icon("layers")}{E(query.TableName)}</span>");
            }
            if (!string.IsNullOrEmpty(query.CallerMethod))
            {
                html.Append($"<span>{Icon("code")}{E(query.CallerMethod)}</span>");
            }
            if (query.RowCount.HasValue)
            {
                html.Append($"<span>{Icon("rows")}{query.RowCount} filas</span>");
            }
            if (!query.IsSuccess)
            {
                html.Append($"<span class='error-text'>{Icon("x-circle")}Fallida</span>");
            }
            html.Append("</span>");
            html.Append($"<div class='q-time {DurationClass(query.ExecutionTime, 100, 500)}'><span class='q-bar'><i style='width:{width}%'></i></span><span class='dur-v'>{query.ExecutionTime} ms</span>{CopyButton(query.Query, "Copiar consulta")}</div>");
            html.Append("</div>");

            html.Append($"<pre class='code small wrap' data-lang='sql'>{E(query.Query)}</pre>");

            if (!string.IsNullOrEmpty(query.Parameters))
            {
                html.Append("<div class='q-sub'>Parámetros</div>");
                html.Append($"<pre class='code small wrap' data-lang='{(TryFormatJson(query.Parameters, out var parameters) ? "json" : "text")}'>{E(parameters)}</pre>");
            }

            if (!string.IsNullOrEmpty(query.ErrorMessage))
            {
                html.Append($"<div class='q-sub error-text'>{E(query.ErrorMessage)}</div>");
            }

            html.Append("</div>");
        }
        html.Append("</div></section>");

        html.Append("</div>");
        return html.ToString();
    }

    private string RenderRelatedLogsPanel(GeneralLog log, List<GeneralLog> relatedLogs, string root)
    {
        var html = new StringBuilder("<div class='tab-panel' id='panel-logs' role='tabpanel'>");

        if (relatedLogs.Count == 0)
        {
            html.Append($"<section class='section'><div class='empty'>{Icon("file")}<h3>Sin logs</h3><p>Esta solicitud no generó mensajes de ILogger.</p></div></section>");
            html.Append("</div>");
            return html.ToString();
        }

        var requestStart = ToDisplayTime(log.Timestamp);
        var parsed = relatedLogs
            .OrderBy(l => l.Timestamp)
            .Select(l => (Log: l, Info: ParseLoggerAction(l.ActionName)))
            .ToList();

        html.Append($"<section class='section'><div class='section-head'><div class='section-title'>{Icon("file")}Logs de la aplicación<span class='count'>{relatedLogs.Count}</span></div>");
        html.Append("<div class='section-tools'><div class='chips'>");
        html.Append($"<button type='button' class='chip all active' data-level-chip=''>Todos<span class='count'>{relatedLogs.Count}</span></button>");
        foreach (var group in parsed.GroupBy(p => p.Info.Level).OrderBy(g => LevelOrder(g.Key)))
        {
            html.Append($"<button type='button' class='chip {LevelClass(group.Key)}' data-level-chip='{E(group.Key)}'><span class='dot'></span>{E(group.Key)}<span class='count'>{group.Count()}</span></button>");
        }
        html.Append("</div>");
        html.Append("<input class='input sm tool-search' id='log-search' type='search' placeholder='Buscar en los logs…' aria-label='Buscar en los logs'>");
        html.Append("<span class='match-info' id='log-count'></span>");
        html.Append("</div></div>");

        html.Append("<div id='log-list'>");
        foreach (var (relatedLog, info) in parsed)
        {
            var offset = (relatedLog.Timestamp - requestStart).TotalMilliseconds;
            html.Append($"<div class='log-row lv-{E(info.Level.ToLowerInvariant())}' data-level='{E(info.Level)}'>");
            html.Append($"<div class='log-time'>{relatedLog.Timestamp:HH:mm:ss.fff}{(offset >= 0 && offset < 86_400_000 ? $"<div class='log-src'>+{offset.ToString("0", CultureInfo.InvariantCulture)} ms</div>" : "")}</div>");
            html.Append($"<div>{LevelBadge(info.Level)}</div>");
            html.Append("<div class='log-main'>");
            html.Append($"<div class='log-msg'>{E(relatedLog.ResponseData)}</div>");
            html.Append($"<div class='log-cat'>{E(relatedLog.RequestData)}{(string.IsNullOrEmpty(info.Source) ? "" : $" <span class='log-src'>· {E(info.Source)}</span>")}</div>");
            if (!string.IsNullOrEmpty(relatedLog.ErrorMessage))
            {
                html.Append($"<details class='log-err'><summary>{E(relatedLog.ErrorMessage)}</summary>");
                if (!string.IsNullOrEmpty(relatedLog.StackTrace))
                {
                    html.Append(RenderStackTrace(relatedLog.StackTrace, null));
                }
                html.Append("</details>");
            }
            html.Append("</div>");
            html.Append($"<div class='row-actions'>{CopyButton(relatedLog.ResponseData ?? string.Empty, "Copiar mensaje")}<a class='btn icon small ghost' href='{root}/detail/{Uri.EscapeDataString(relatedLog.Id ?? string.Empty)}' title='Abrir este log'>{Icon("external")}</a></div>");
            html.Append("</div>");
        }
        html.Append("</div></section>");

        html.Append("</div>");
        return html.ToString();
    }

    private static string RenderLoggerMessagePanel(GeneralLog log, string level, string source)
    {
        var html = new StringBuilder("<div class='tab-panel' id='panel-message' role='tabpanel'><div class='grid-2'>");
        html.Append(CodeSection("log-message", "Mensaje", "file", log.ResponseData, $"log-{log.Id}"));

        var context = new List<KeyValuePair<string, string>>
        {
            new("Nivel", level),
            new("Categoría", log.RequestData ?? string.Empty),
            new("Origen", source),
            new("Método", HasRequestContext(log) ? log.Method : string.Empty),
            new("URL", HasRequestContext(log) ? log.HttpUrl : string.Empty),
            new("IP", log.IpAddress),
            new("Servicio", log.ServiceName)
        };
        html.Append($"<section class='section'><div class='section-head'><div class='section-title'>{Icon("info")}Contexto</div></div>");
        html.Append(KeyValueTable(context.Where(c => !string.IsNullOrEmpty(c.Value)).ToList(), null));
        html.Append("</section>");

        html.Append("</div></div>");
        return html.ToString();
    }

    /// <summary>
    /// Sección con un visor de código: resaltado, búsqueda, ajuste de línea, expandir, copiar y descargar.
    /// </summary>
    private static string CodeSection(string id, string title, string icon, string? content, string downloadName)
    {
        var html = new StringBuilder("<section class='section'>");
        html.Append($"<div class='section-head'><div class='section-title'>{Icon(icon)}{E(title)}</div>");

        if (string.IsNullOrEmpty(content))
        {
            html.Append($"</div><div class='empty'>{Icon("inbox")}<h3>Sin contenido</h3><p>No se capturó ningún cuerpo.</p></div></section>");
            return html.ToString();
        }

        var isJson = TryFormatJson(content, out var formatted);
        var lines = formatted.Count(c => c == '\n') + 1;
        var bytes = Encoding.UTF8.GetByteCount(content);

        html.Append($"<div class='section-tools' data-code-tools='{id}'>");
        html.Append($"<span class='code-meta'>{(isJson ? "JSON" : "Texto")} · {FormatBytes(bytes)} · {lines} líneas</span>");
        html.Append("<input class='input sm tool-search' type='search' placeholder='Buscar…  (Enter: siguiente)' aria-label='Buscar en el contenido'><span class='match-info'></span>");
        html.Append($"<button type='button' class='btn small ghost icon' data-act='wrap' title='Ajustar líneas'>{Icon("wrap")}</button>");
        html.Append($"<button type='button' class='btn small ghost icon' data-act='expand' title='Mostrar todo el contenido'>{Icon("maximize")}</button>");
        html.Append($"<button type='button' class='btn small ghost icon' data-act='download' data-name='{E(downloadName)}.{(isJson ? "json" : "txt")}' title='Descargar'>{Icon("download")}</button>");
        html.Append($"<button type='button' class='btn small ghost' data-copy-target='#{id}'>{Icon("copy")}{Icon("check")}Copiar</button>");
        html.Append("</div></div>");
        html.Append($"<pre class='code{(isJson ? "" : " wrap")}' id='{id}' data-lang='{(isJson ? "json" : "text")}'>{E(formatted)}</pre>");
        html.Append("</section>");
        return html.ToString();
    }

    /// <summary>
    /// Tabla clave/valor con botón de copiar por fila. Los valores enmascarados se marcan como tales.
    /// </summary>
    private static string KeyValueTable(List<KeyValuePair<string, string>> rows, string? id)
    {
        var html = new StringBuilder($"<table class='kv'{(id == null ? "" : $" id='{id}'")}><tbody>");
        foreach (var row in rows)
        {
            var masked = row.Value == "*****";
            html.Append($"<tr data-row><td class='k'>{E(row.Key)}</td>");
            html.Append($"<td class='v'>{(masked ? $"<span class='masked'>{Icon("eye")}enmascarado</span>" : E(row.Value))}</td>");
            html.Append($"<td class='a'>{(masked ? "" : CopyButton(row.Value, $"Copiar {row.Key}"))}</td></tr>");
        }
        html.Append("</tbody></table>");
        return html.ToString();
    }

    /// <summary>
    /// Stack trace con los frames del framework atenuados y la ubicación en el código resaltada.
    /// </summary>
    private static string RenderStackTrace(string stackTrace, string? id)
    {
        var html = new StringBuilder($"<div class='stack'{(id == null ? "" : $" id='{id}'")}>");
        foreach (var rawLine in stackTrace.Replace("\r\n", "\n").Split('\n'))
        {
            var line = rawLine.TrimEnd();
            if (line.Length == 0)
            {
                continue;
            }

            var trimmed = line.TrimStart();
            var isFramework = trimmed.StartsWith("at System.", StringComparison.Ordinal) ||
                              trimmed.StartsWith("at Microsoft.", StringComparison.Ordinal) ||
                              trimmed.StartsWith("---", StringComparison.Ordinal);
            var sourceIndex = line.LastIndexOf(" in ", StringComparison.Ordinal);
            var css = isFramework ? "fw" : sourceIndex > 0 ? "app" : string.Empty;

            html.Append($"<span class='st-line {css}'>");
            if (sourceIndex > 0 && !isFramework)
            {
                html.Append(E(line.Substring(0, sourceIndex)));
                html.Append($" in <span class='st-src'>{E(line.Substring(sourceIndex + 4))}</span>");
            }
            else
            {
                html.Append(E(line));
            }
            html.Append("</span>");
        }
        html.Append("</div>");
        return html.ToString();
    }

    /// <summary>
    /// Construye un comando cURL que reproduce la solicitud capturada (las cabeceras enmascaradas se mantienen así).
    /// </summary>
    private static string BuildCurl(GeneralLog log, List<KeyValuePair<string, string>>? headers)
    {
        static string Quote(string value) => "'" + value.Replace("'", "'\\''") + "'";

        var headerList = headers ?? new List<KeyValuePair<string, string>>();
        var host = headerList.FirstOrDefault(h => h.Key.Equals("Host", StringComparison.OrdinalIgnoreCase)).Value;
        var scheme = headerList.FirstOrDefault(h => h.Key.Equals("X-Forwarded-Proto", StringComparison.OrdinalIgnoreCase)).Value;
        var url = (string.IsNullOrEmpty(host) ? string.Empty : $"{(string.IsNullOrEmpty(scheme) ? "http" : scheme)}://{host}") +
                  log.HttpUrl + (HasQuery(log.QueryParams) ? log.QueryParams : string.Empty);

        var skip = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Host", "Content-Length", "Connection", "Accept-Encoding" };
        var curl = new StringBuilder($"curl -X {log.Method} {Quote(url)}");
        foreach (var header in headerList.Where(h => !skip.Contains(h.Key) && !h.Key.StartsWith(":", StringComparison.Ordinal)))
        {
            curl.Append($" \\\n  -H {Quote($"{header.Key}: {header.Value}")}");
        }

        if (!string.IsNullOrEmpty(log.RequestData) && !HttpMethods.IsGet(log.Method) && !HttpMethods.IsHead(log.Method))
        {
            curl.Append($" \\\n  --data-raw {Quote(log.RequestData)}");
        }

        return curl.ToString();
    }

    /// <summary>
    /// Elimina todos los logs y redirige a la página principal.
    /// </summary>
    /// <returns>HTML con mensaje de redirección</returns>
    public async Task<string> DeleteAllLogsAsync()
    {
        await _hubbleService.DeleteAllLogsAsync();

        var html = GenerateHtmlHeader("Hubble - Logs eliminados", true);
        html += TopBar("logs");
        html += "<main class='container'>";
        html += "<div class='card success-card'>";
        html += "<h2>Operación exitosa</h2>";
        html += "<p>Todos los logs han sido eliminados correctamente.</p><br>";
        html += $"<a href='{_prefixPath}{_basePath}' class='btn primary'>{Icon("arrow-left")}Volver a la lista</a>";
        html += "</div>";
        html += "</main>";
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
        html += TopBar("logs");
        html += "<main class='container'>";
        html += $"<div class='card error-card'><h2>{E(title)}</h2><p>{E(message)}</p><br>";
        html += $"<a href='{_prefixPath}{_basePath}' class='btn primary'>{Icon("arrow-left")}Volver a la lista</a>";
        html += "</div>";
        html += "</main>";
        html += GenerateHtmlFooter();

        return html;
    }

    /// <summary>
    /// Codifica un valor para insertarlo de forma segura en el HTML (texto o atributos).
    /// Todo dato que proviene de los logs debe pasar por aquí: puede contener contenido controlado por un atacante.
    /// </summary>
    private static string E(object? value) => HubbleHtml.Encode(value);

    private static string Icon(string name, string? extraClass = null) => HubbleAssets.Icon(name, extraClass);

    /// <summary>
    /// Campo oculto con el token antiforgery para los formularios que modifican datos.
    /// </summary>
    private string AntiforgeryField()
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext == null)
        {
            return string.Empty;
        }

        var tokens = _antiforgery.GetAndStoreTokens(httpContext);
        return $"<input type='hidden' name='{E(tokens.FormFieldName)}' value='{E(tokens.RequestToken)}'>";
    }

    /// <summary>
    /// Botón de cierre de sesión (solo si la autenticación está habilitada).
    /// </summary>
    private string LogoutButton()
    {
        return _options.RequireAuthentication
            ? $"<a href='{_prefixPath}{_basePath}/logout' class='btn small ghost' title='Cerrar sesión'>{Icon("logout")}<span>Cerrar sesión</span></a>"
            : string.Empty;
    }

    /// <summary>
    /// Barra superior común a todas las páginas del dashboard.
    /// </summary>
    private string TopBar(string active)
    {
        var root = $"{_prefixPath}{_basePath}";
        return "<header class='topbar'><div class='topbar-inner'>" +
               $"<div class='brand'>{GetHubbleLogo()}<div class='brand-meta'><span class='app-title'>Hubble for .NET</span><span class='app-version'>{E(_version)}</span></div></div>" +
               "<nav class='nav'>" +
               $"<a href='{root}'{(active == "logs" ? " class='active'" : "")}>{Icon("list")}<span>Logs</span></a>" +
               $"<a href='{root}/config'{(active == "config" ? " class='active'" : "")}>{Icon("settings")}<span>Configuración</span></a>" +
               "</nav>" +
               "<div class='topbar-right'>" +
               $"<button type='button' class='btn small ghost icon' data-open-dialog='shortcuts-dialog' title='Atajos de teclado (?)'>{Icon("keyboard")}</button>" +
               LogoutButton() +
               "</div></div></header>";
    }

    /// <summary>
    /// Celda de la cuadrícula de metadatos del detalle, con botón de copiar opcional.
    /// </summary>
    private static string Meta(string icon, string label, string? value, bool copy = false, bool mono = false, string? rawHtml = null)
    {
        var display = rawHtml ?? (string.IsNullOrEmpty(value) ? "<span class='muted'>—</span>" : E(value));
        var copyButton = copy && !string.IsNullOrEmpty(value) ? CopyButton(value!, $"Copiar {label.ToLowerInvariant()}") : string.Empty;
        return $"<div class='meta'><div class='meta-k'>{Icon(icon)}{E(label)}</div><div class='meta-v{(mono ? " mono" : "")}' title='{E(value)}'>{display}</div>{copyButton}</div>";
    }

    private static string CopyButton(string text, string title = "Copiar", string extraClass = "ghost")
    {
        return $"<button type='button' class='btn icon small {extraClass}' data-copy='{E(text)}' title='{E(title)}' aria-label='{E(title)}'>{Icon("copy")}{Icon("check")}</button>";
    }

    private static bool IsLoggerEntry(GeneralLog log) => log.ControllerName == "ApplicationLogger";

    /// <summary>
    /// Los logs de ILogger escritos fuera de una solicitud HTTP llevan "No Method" / "No URL available".
    /// </summary>
    private static bool HasRequestContext(GeneralLog log) => !string.IsNullOrEmpty(log.Method) && log.Method != "No Method";

    private static bool HasQuery(string? queryParams) => !string.IsNullOrEmpty(queryParams) && queryParams != "?";

    private static string Truncate(string? value, int max) =>
        value == null ? string.Empty : value.Length <= max ? value : value.Substring(0, max) + "…";

    private static string MethodClass(string? method) => (method ?? string.Empty).ToUpperInvariant() switch
    {
        "GET" => "m-get",
        "POST" => "m-post",
        "PUT" => "m-put",
        "PATCH" => "m-patch",
        "DELETE" => "m-delete",
        "OPTIONS" => "m-options",
        "HEAD" => "m-head",
        _ => "m-other"
    };

    private static string MethodBadge(string? method, bool large = false) =>
        string.IsNullOrEmpty(method) || method == "No Method"
            ? "<span class='muted'>—</span>"
            : $"<span class='badge {MethodClass(method)}{(large ? " lg" : "")}'>{E(method)}</span>";

    private static int StatusFamily(int statusCode) => statusCode >= 100 && statusCode < 600 ? statusCode / 100 : 0;

    private static string StatusPill(int statusCode, bool withReason = false)
    {
        if (statusCode <= 0)
        {
            return "<span class='badge pill s-0'>—</span>";
        }

        var reason = withReason ? ReasonPhrases.GetReasonPhrase(statusCode) : string.Empty;
        return $"<span class='badge pill s-{StatusFamily(statusCode)}{(withReason ? " lg" : "")}'><span class='dot'></span>{statusCode}{(string.IsNullOrEmpty(reason) ? "" : " " + E(reason))}</span>";
    }

    /// <summary>
    /// Separa el nivel y el origen del ActionName de un log de ILogger ("Warning [Archivo.cs:12 → Metodo]").
    /// </summary>
    private static (string Level, string Source) ParseLoggerAction(string? actionName)
    {
        var value = actionName ?? string.Empty;
        var bracket = value.IndexOf(" [", StringComparison.Ordinal);
        return bracket < 0
            ? (value.Trim(), string.Empty)
            : (value.Substring(0, bracket).Trim(), value.Substring(bracket + 2).TrimEnd(']').Trim());
    }

    private static string LevelClass(string level)
    {
        var normalized = level.ToLowerInvariant();
        return normalized is "information" or "warning" or "error" or "critical" or "debug" or "trace" ? $"l-{normalized}" : "l-none";
    }

    private static int LevelOrder(string level) => level switch
    {
        "Critical" => 0,
        "Error" => 1,
        "Warning" => 2,
        "Information" => 3,
        "Debug" => 4,
        "Trace" => 5,
        _ => 6
    };

    private static string LevelBadge(string level) =>
        $"<span class='badge pill plain {LevelClass(level)}'><span class='dot'></span>{E(string.IsNullOrEmpty(level) ? "Log" : level)}</span>";

    private static string OperationClass(string operation) => operation switch
    {
        "SELECT" or "FIND" or "QUERY" => "m-get",
        "INSERT" or "CREATE" => "m-post",
        "UPDATE" or "MERGE" or "UPSERT" => "m-put",
        "DELETE" or "DROP" => "m-delete",
        _ => "m-patch"
    };

    private static string DurationClass(long ms, long mid = 300, long slow = 1000) => ms >= slow ? "slow" : ms >= mid ? "mid" : "fast";

    private static string DurationColor(long ms) => DurationClass(ms) switch { "slow" => "s-5", "mid" => "s-4", _ => "s-2" };

    private static string DurationVar(long ms) => DurationClass(ms) switch { "slow" => "red", "mid" => "amber", _ => "green" };

    private static string FormatBytes(long bytes)
    {
        if (bytes < 1024)
        {
            return $"{bytes} B";
        }

        return bytes < 1024 * 1024
            ? (bytes / 1024.0).ToString("0.#", CultureInfo.InvariantCulture) + " KB"
            : (bytes / 1048576.0).ToString("0.##", CultureInfo.InvariantCulture) + " MB";
    }

    /// <summary>
    /// Texto relativo inicial ("hace 5 min"); el navegador lo mantiene actualizado.
    /// </summary>
    private static string FormatAgo(double seconds)
    {
        var s = Math.Max(0, Math.Round(seconds));
        if (s < 5) return "ahora mismo";
        if (s < 60) return $"hace {s} s";
        var minutes = Math.Floor(s / 60);
        if (minutes < 60) return $"hace {minutes} min";
        var hours = Math.Floor(minutes / 60);
        if (hours < 24) return $"hace {hours} h";
        var days = Math.Floor(hours / 24);
        return days == 1 ? "hace 1 día" : $"hace {days} días";
    }

    /// <summary>
    /// Convierte a la zona horaria configurada las fechas que llegan en UTC (el listado ya viene convertido).
    /// </summary>
    private DateTime ToDisplayTime(DateTime timestamp) =>
        timestamp.Kind == DateTimeKind.Utc ? TimeZoneInfo.ConvertTimeFromUtc(timestamp, _timeZone) : timestamp;

    /// <summary>
    /// Segundos transcurridos desde la fecha del log, tenga o no aplicada la zona horaria configurada.
    /// </summary>
    private double AgeSeconds(DateTime timestamp) => timestamp.Kind == DateTimeKind.Utc
        ? (DateTime.UtcNow - timestamp).TotalSeconds
        : (TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, _timeZone) - timestamp).TotalSeconds;

    /// <summary>
    /// Lee las cabeceras serializadas como objeto JSON. Devuelve null si no tienen ese formato.
    /// </summary>
    private static List<KeyValuePair<string, string>>? ParseHeaders(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new List<KeyValuePair<string, string>>();
        }

        try
        {
            var headers = JsonConvert.DeserializeObject<Dictionary<string, object?>>(json!);
            return headers?
                .OrderBy(h => h.Key, StringComparer.OrdinalIgnoreCase)
                .Select(h => new KeyValuePair<string, string>(h.Key, h.Value?.ToString() ?? string.Empty))
                .ToList();
        }
        catch
        {
            return null;
        }
    }

    private static List<KeyValuePair<string, string>> ParseQueryParams(string? queryString)
    {
        if (!HasQuery(queryString))
        {
            return new List<KeyValuePair<string, string>>();
        }

        try
        {
            return QueryHelpers.ParseQuery(queryString)
                .SelectMany(p => p.Value.Select(v => new KeyValuePair<string, string>(p.Key, v ?? string.Empty)))
                .ToList();
        }
        catch
        {
            return new List<KeyValuePair<string, string>> { new("query", queryString!) };
        }
    }

    /// <summary>
    /// Construye el enlace de paginación conservando los filtros, con los valores codificados para URL y HTML.
    /// </summary>
    private static string PageLink(int page, int pageSize, string? method, string? url, string? statusGroup, string? logType)
    {
        var query = $"?page={page}&pageSize={pageSize}" +
                    $"&method={Uri.EscapeDataString(method ?? string.Empty)}" +
                    $"&url={Uri.EscapeDataString(url ?? string.Empty)}" +
                    $"&statusGroup={Uri.EscapeDataString(statusGroup ?? string.Empty)}" +
                    $"&logType={Uri.EscapeDataString(logType ?? string.Empty)}";
        return E(query);
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
    /// Genera el encabezado HTML con los estilos del dashboard.
    /// </summary>
    /// <param name="title">Título de la página</param>
    /// <param name="showLogout">Indica si se debe mostrar el botón de logout</param>
    /// <returns>HTML del encabezado</returns>
    private string GenerateHtmlHeader(string title, bool showLogout = false)
    {
        return "<!DOCTYPE html><html lang='es'><head>" +
               "<meta charset='UTF-8'>" +
               "<meta name='viewport' content='width=device-width, initial-scale=1.0'>" +
               "<meta name='color-scheme' content='dark'>" +
               "<link rel='icon' href=\"data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 32 32'%3E%3Ccircle cx='16' cy='16' r='14' fill='%23131726' stroke='%238b5cf6' stroke-width='3'/%3E%3Ccircle cx='20' cy='17' r='4' fill='%2322d3ee'/%3E%3Ccircle cx='11' cy='12' r='3' fill='%23c4b5fd'/%3E%3C/svg%3E\">" +
               $"<title>{E(title)}</title>" +
               $"<style>{HubbleAssets.Styles}</style>" +
               "</head><body>";
    }

    /// <summary>
    /// Genera el pie de página HTML, el diálogo de atajos y los scripts del dashboard.
    /// </summary>
    /// <returns>HTML del pie de página</returns>
    private string GenerateHtmlFooter()
    {
        return $"<footer class='app-footer'><span class='app-title'>Hubble for .NET</span><span class='app-version'>{E(_version)}</span> · Pulsa <kbd>?</kbd> para ver los atajos de teclado</footer>" +
               "<dialog class='modal' id='shortcuts-dialog'>" +
               $"<div class='modal-head'><span>Atajos de teclado</span><button type='button' class='btn small ghost icon' data-close title='Cerrar'>{Icon("x")}</button></div>" +
               "<div class='modal-body'><div class='shortcuts'>" +
               "<span><kbd>/</kbd></span><span>Buscar por URL</span>" +
               "<span><kbd>F</kbd></span><span>Refinar los resultados de la página</span>" +
               "<span><kbd>J</kbd> <kbd>K</kbd></span><span>Moverse entre registros</span>" +
               "<span><kbd>Enter</kbd></span><span>Abrir el registro seleccionado</span>" +
               "<span><kbd>C</kbd></span><span>Copiar la URL del registro seleccionado</span>" +
               "<span><kbd>←</kbd> <kbd>→</kbd></span><span>Página anterior / siguiente</span>" +
               "<span><kbd>L</kbd></span><span>Activar o pausar la actualización en vivo</span>" +
               "<span><kbd>1</kbd> … <kbd>6</kbd></span><span>Cambiar de pestaña en el detalle</span>" +
               "<span><kbd>B</kbd></span><span>Volver a la lista desde el detalle</span>" +
               "<span><kbd>Esc</kbd></span><span>Salir del campo de búsqueda</span>" +
               "</div></div></dialog>" +
               $"<script>{HubbleAssets.Scripts}</script>" +
               "</body></html>";
    }

    /// <summary>
    /// Formatea una cadena JSON para mostrarla con formato.
    /// </summary>
    /// <param name="json">Cadena JSON</param>
    /// <returns>JSON formateado</returns>
    private static string FormatJson(string json)
    {
        return TryFormatJson(json, out var formatted) ? formatted : json ?? string.Empty;
    }

    /// <summary>
    /// Indenta un objeto o array JSON sin alterar sus valores (fechas y decimales se conservan tal cual).
    /// </summary>
    /// <param name="json">Texto a formatear</param>
    /// <param name="formatted">JSON indentado, o el texto original si no es JSON</param>
    /// <returns>true si el texto era JSON válido</returns>
    private static bool TryFormatJson(string? json, out string formatted)
    {
        formatted = json ?? string.Empty;
        var trimmed = formatted.TrimStart();
        if (trimmed.Length == 0 || (trimmed[0] != '{' && trimmed[0] != '['))
        {
            return false;
        }

        try
        {
            using var reader = new JsonTextReader(new StringReader(formatted))
            {
                DateParseHandling = DateParseHandling.None,
                FloatParseHandling = FloatParseHandling.Decimal
            };
            var token = JToken.ReadFrom(reader);
            if (reader.Read())
            {
                // Contenido adicional después del JSON: no es un documento JSON válido
                return false;
            }

            formatted = token.ToString(Formatting.Indented);
            return true;
        }
        catch (JsonException)
        {
            return false;
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

        html += TopBar("config");
        html += "<main class='container'>";
        html += "<div class='page-head'><div><h1 class='page-h1'>Configuración</h1><p class='page-sub'>Estadísticas de almacenamiento y opciones activas de Hubble</p></div></div>";

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

        // Retención de datos (índice TTL) y última limpieza manual
        html += "<div class='stats-card'>";
        html += "<h3>Retención de datos</h3>";
        html += "<div class='info-message'>";
        html += _options.EnableDataPrune
            ? $"<p>MongoDB elimina automáticamente los logs con más de {_options.MaxLogAgeHours} horas (índice TTL).</p>"
            : "<p>La retención automática está deshabilitada: los logs se conservan hasta que se eliminen manualmente.</p>";
        html += "</div>";

        if (stats.LastPrune.LastPruneDate.HasValue)
        {
            var lastPruneDate = stats.LastPrune.LastPruneDate.Value.ToLocalTime();
            var timeAgoText = FormatTimeAgo(DateTime.Now - lastPruneDate);

            html += "<div class='stats-grid' style='margin-top: 15px;'>";
            html += $"<div class='stat-item'><span class='stat-value'>{lastPruneDate:dd/MM/yyyy}</span><span class='stat-label'>Fecha</span></div>";
            html += $"<div class='stat-item'><span class='stat-value'>{lastPruneDate:HH:mm:ss}</span><span class='stat-label'>Hora</span></div>";
            html += $"<div class='stat-item'><span class='stat-value'>{stats.LastPrune.LogsDeleted}</span><span class='stat-label'>Logs eliminados</span></div>";
            html += $"<div class='stat-item time-ago'><span class='time-ago-text'>Última limpieza manual: {timeAgoText}</span></div>";
            html += "</div>";
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
        html += $"<div class='config-value'>{E(_options.ServiceName)}</div>";
        html += "</div>";
        html += "<div class='config-group'>";
        html += "<label>Versión:</label>";
        html += $"<div class='config-value'>{E(_version)}</div>";
        html += "</div>";
        html += "<div class='config-group'>";
        html += "<label>Base de datos:</label>";
        html += $"<div class='config-value'>{E(_options.DatabaseName)}</div>";
        html += "</div>";
        html += "<div class='config-group'>";
        html += "<label>MongoDB:</label>";
        html += $"<div class='config-value'>Driver {E(config.SystemInfo.MongoDBVersion)}</div>";
        html += "</div>";
        html += "<div class='config-group'>";
        html += "<label>Zona horaria:</label>";
        html += $"<div class='config-value'>{E(string.IsNullOrEmpty(_options.TimeZoneId) ? "UTC" : _options.TimeZoneId)}</div>";
        html += "</div>";
        html += "<div class='config-group'>";
        html += "<label>Diagnósticos:</label>";
        html += $"<div class='config-value'>{(_options.EnableDiagnostics ? "Activado" : "Desactivado")}</div>";
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

        // Inicio del proceso actual (no la fecha en que se creó la configuración en MongoDB)
        var startTime = System.Diagnostics.Process.GetCurrentProcess().StartTime;
        var uptime = DateTime.Now - startTime;
        var uptimeText = FormatTimeAgo(uptime);

        html += $"<div class='config-value'>{startTime:dd/MM/yyyy HH:mm:ss} <span class='uptime'>({uptimeText})</span></div>";
        html += "</div>";
        html += "</div>";
        html += "</div>";

        // Configuración de limpieza
        html += "<div class='stats-card'>";
        html += "<h3>Retención y almacenamiento</h3>";
        html += "<div class='config-form'>";
        html += "<div class='config-group'>";
        html += "<label>Retención automática (TTL):</label>";
        html += $"<div class='config-value'>{(_options.EnableDataPrune ? "Activada" : "Desactivada")}</div>";
        html += "</div>";
        html += "<div class='config-group'>";
        html += "<label>Edad máxima de los logs (horas):</label>";
        html += $"<div class='config-value'>{(_options.EnableDataPrune ? _options.MaxLogAgeHours.ToString() : "Sin límite")}</div>";
        html += "</div>";
        html += "<div class='config-group'>";
        html += "<label>Índices de MongoDB:</label>";
        html += $"<div class='config-value'>{E(DescribeStorageStatus())}</div>";
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
        html += "<label>Capturar solicitudes HTTP:</label>";
        html += $"<div class='config-value'>{(_options.CaptureHttpRequests ? "Activado" : "Desactivado")}</div>";
        html += "</div>";
        html += "<div class='config-group'>";
        html += "<label>Capturar mensajes de ILogger:</label>";
        html += $"<div class='config-value'>{(_options.CaptureLoggerMessages ? "Activado" : "Desactivado")}</div>";
        html += "</div>";
        html += "<div class='config-group'>";
        html += "<label>Nivel mínimo de log:</label>";
        html += $"<div class='config-value'>{E(_options.MinimumLogLevel)}</div>";
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

        if (_options.IgnorePaths.Count > 0)
        {
            html += "<ul class='ignored-paths-list'>";
            foreach (var path in _options.IgnorePaths)
            {
                html += $"<li>{E(path)}</li>";
            }
            html += "</ul>";
        }
        else
        {
            html += "<div class='config-value'>No hay rutas ignoradas configuradas.</div>";
        }

        html += "</div>";
        html += "</div>";
        html += "<div class='info-message'>";
        html += "<p>La configuración no puede ser modificada. Consulte al administrador del sistema para realizar cambios.</p>";
        html += "</div>";
        html += "</div>";

        html += "</div>"; // Fin de la segunda columna
        html += "</div>"; // Fin del contenedor de configuración

        html += "</main>"; // Fin del contenedor principal

        // Agregar estilos CSS específicos para la página de configuración
        html += "<style>";
        html += ".config-container { display: flex; flex-wrap: wrap; gap: 20px; margin-top: 20px; }";
        html += ".config-column { flex: 1; min-width: 300px; }";
        html += ".stats-column, .config-settings-column { display: flex; flex-direction: column; gap: 20px; }";
        html += ".stats-card { background: var(--surface); border: 1px solid var(--border); border-radius: 16px; padding: 20px; color: var(--text); }";
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
            var maxAgeHours = _options.MaxLogAgeHours > 0 ? _options.MaxLogAgeHours : 24;
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
    /// Describe el estado de los índices (incluido el TTL de retención) para la página de configuración.
    /// </summary>
    private string DescribeStorageStatus()
    {
        if (_storageStatus == null)
        {
            return "Desconocido";
        }

        if (_storageStatus.IndexesReady)
        {
            return "Creados";
        }

        return _storageStatus.LastError == null
            ? "Pendientes"
            : $"Error ({_storageStatus.LastError}). Se reintentará automáticamente.";
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

            var maxAgeHours = _options.MaxLogAgeHours > 0 ? _options.MaxLogAgeHours : 24;
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