namespace Gabonet.Hubble.UI;

using System.Collections.Generic;

/// <summary>
/// Recursos estáticos del dashboard (estilos, scripts e iconos).
/// Todo va embebido en la página: la CSP del dashboard no permite cargar recursos externos.
/// </summary>
internal static class HubbleAssets
{
    private static readonly Dictionary<string, string> IconPaths = new()
    {
        ["search"] = "<circle cx='11' cy='11' r='7'/><path d='m20 20-3.5-3.5'/>",
        ["copy"] = "<rect x='9' y='9' width='12' height='12' rx='2'/><path d='M5 15V5a2 2 0 0 1 2-2h10'/>",
        ["check"] = "<path d='M20 6 9 17l-5-5'/>",
        ["external"] = "<path d='M14 4h6v6'/><path d='M20 4 10 14'/><path d='M19 14v5a1 1 0 0 1-1 1H5a1 1 0 0 1-1-1V6a1 1 0 0 1 1-1h5'/>",
        ["x"] = "<path d='M18 6 6 18M6 6l12 12'/>",
        ["arrow-left"] = "<path d='M19 12H5m6-6-6 6 6 6'/>",
        ["chevron-left"] = "<path d='m15 18-6-6 6-6'/>",
        ["chevron-right"] = "<path d='m9 18 6-6-6-6'/>",
        ["chevrons-left"] = "<path d='m11 17-5-5 5-5M18 17l-5-5 5-5'/>",
        ["chevrons-right"] = "<path d='m13 17 5-5-5-5M6 17l5-5-5-5'/>",
        ["settings"] = "<path d='M4 21v-7M4 10V3M12 21v-9M12 8V3M20 21v-5M20 12V3M1 14h6M9 8h6M17 16h6'/>",
        ["list"] = "<path d='M8 6h13M8 12h13M8 18h13M3 6h.01M3 12h.01M3 18h.01'/>",
        ["logout"] = "<path d='M9 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h4M16 17l5-5-5-5M21 12H9'/>",
        ["trash"] = "<path d='M3 6h18M8 6V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2M19 6l-1 14a2 2 0 0 1-2 2H8a2 2 0 0 1-2-2L5 6'/>",
        ["live"] = "<circle cx='12' cy='12' r='2'/><path d='M16.24 7.76a6 6 0 0 1 0 8.49M7.76 16.24a6 6 0 0 1 0-8.49M19.07 4.93a10 10 0 0 1 0 14.14M4.93 19.07a10 10 0 0 1 0-14.14'/>",
        ["download"] = "<path d='M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4M7 10l5 5 5-5M12 15V3'/>",
        ["terminal"] = "<path d='m4 17 6-6-6-6M12 19h8'/>",
        ["clock"] = "<circle cx='12' cy='12' r='9'/><path d='M12 7v5l3 2'/>",
        ["database"] = "<ellipse cx='12' cy='5' rx='8' ry='3'/><path d='M4 5v14c0 1.66 3.58 3 8 3s8-1.34 8-3V5M4 12c0 1.66 3.58 3 8 3s8-1.34 8-3'/>",
        ["alert"] = "<path d='M10.3 3.9 1.8 18a2 2 0 0 0 1.7 3h17a2 2 0 0 0 1.7-3L13.7 3.9a2 2 0 0 0-3.4 0zM12 9v4M12 17h.01'/>",
        ["check-circle"] = "<circle cx='12' cy='12' r='9'/><path d='m8 12 3 3 5-6'/>",
        ["x-circle"] = "<circle cx='12' cy='12' r='9'/><path d='m15 9-6 6M9 9l6 6'/>",
        ["activity"] = "<path d='M22 12h-4l-3 9L9 3l-3 9H2'/>",
        ["layers"] = "<path d='m12 2 10 5-10 5L2 7l10-5zM2 17l10 5 10-5M2 12l10 5 10-5'/>",
        ["bookmark"] = "<path d='M19 21l-7-5-7 5V5a2 2 0 0 1 2-2h10a2 2 0 0 1 2 2z'/>",
        ["filter"] = "<path d='M22 3H2l8 9.46V19l4 2v-8.54L22 3z'/>",
        ["request"] = "<path d='M7 17 17 7M7 7h10v10'/>",
        ["response"] = "<path d='M17 7 7 17M17 17H7V7'/>",
        ["file"] = "<path d='M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z'/><path d='M14 2v6h6M16 13H8M16 17H8M10 9H8'/>",
        ["wrap"] = "<path d='M3 6h18M3 12h15a3 3 0 1 1 0 6h-4m0 0 2-2m-2 2 2 2M3 18h7'/>",
        ["maximize"] = "<path d='M8 3H5a2 2 0 0 0-2 2v3m18 0V5a2 2 0 0 0-2-2h-3m0 18h3a2 2 0 0 0 2-2v-3M3 16v3a2 2 0 0 0 2 2h3'/>",
        ["zap"] = "<path d='M13 2 3 14h9l-1 8 10-12h-9l1-8z'/>",
        ["rows"] = "<path d='M3 5h18M3 12h18M3 19h18'/>",
        ["keyboard"] = "<rect x='2' y='6' width='20' height='12' rx='2'/><path d='M6 10h.01M10 10h.01M14 10h.01M18 10h.01M7 14h10'/>",
        ["hash"] = "<path d='M4 9h16M4 15h16M10 3 8 21M16 3l-2 18'/>",
        ["globe"] = "<circle cx='12' cy='12' r='9'/><path d='M3 12h18M12 3a14 14 0 0 1 0 18M12 3a14 14 0 0 0 0 18'/>",
        ["server"] = "<rect x='3' y='4' width='18' height='7' rx='2'/><rect x='3' y='13' width='18' height='7' rx='2'/><path d='M7 7.5h.01M7 16.5h.01'/>",
        ["calendar"] = "<rect x='3' y='5' width='18' height='16' rx='2'/><path d='M16 3v4M8 3v4M3 11h18'/>",
        ["box"] = "<path d='M21 8 12 3 3 8v8l9 5 9-5z'/><path d='m3 8 9 5 9-5M12 13v8'/>",
        ["play"] = "<path d='m6 4 14 8-14 8z'/>",
        ["eye"] = "<path d='M2 12s3.5-7 10-7 10 7 10 7-3.5 7-10 7S2 12 2 12z'/><circle cx='12' cy='12' r='3'/>",
        ["plus"] = "<path d='M12 5v14M5 12h14'/>",
        ["code"] = "<path d='m16 18 6-6-6-6M8 6l-6 6 6 6'/>",
        ["info"] = "<circle cx='12' cy='12' r='9'/><path d='M12 16v-4M12 8h.01'/>",
        ["inbox"] = "<path d='M22 12h-6l-2 3h-4l-2-3H2'/><path d='M5.45 5.11 2 12v6a2 2 0 0 0 2 2h16a2 2 0 0 0 2-2v-6l-3.45-6.89A2 2 0 0 0 16.76 4H7.24a2 2 0 0 0-1.79 1.11z'/>"
    };

    /// <summary>
    /// Devuelve un icono SVG en línea (trazos de 24x24 que heredan el color del texto).
    /// </summary>
    public static string Icon(string name, string? extraClass = null)
    {
        var paths = IconPaths.TryGetValue(name, out var p) ? p : string.Empty;
        var cls = extraClass == null ? $"i i-{name}" : $"i i-{name} {extraClass}";
        return $"<svg class='{cls}' viewBox='0 0 24 24' fill='none' stroke='currentColor' stroke-width='2' stroke-linecap='round' stroke-linejoin='round' aria-hidden='true'>{paths}</svg>";
    }

    /// <summary>
    /// Hoja de estilos compartida por todas las páginas del dashboard.
    /// </summary>
    public const string Styles = @"
:root {
    --bg: #0a0c14;
    --bg-2: #0e1120;
    --surface: #131726;
    --surface-2: #1a1f33;
    --surface-3: #242a42;
    --border: #232944;
    --border-strong: #343c5e;
    --text: #e9ebf8;
    --text-2: #a7adcb;
    --text-3: #6d749a;
    --primary: #8b5cf6;
    --primary-2: #a78bfa;
    --primary-3: #c4b5fd;
    --primary-glow: rgba(139, 92, 246, 0.35);
    --accent: #22d3ee;
    --green: #4ade80;
    --amber: #fbbf24;
    --red: #f87171;
    --blue: #60a5fa;
    --pink: #f472b6;
    --violet: #c084fc;
    --gray: #94a3b8;
    --brand-gradient: linear-gradient(135deg, #8b5cf6 0%, #6366f1 45%, #22d3ee 100%);
    --radius: 14px;
    --topbar-h: 64px;
    --font: 'Segoe UI Variable Text', 'Segoe UI', system-ui, -apple-system, 'Helvetica Neue', Roboto, sans-serif;
    --mono: 'Cascadia Code', 'JetBrains Mono', 'Fira Code', Consolas, 'Courier New', monospace;
    --primary-color: var(--primary);
    --primary-light: var(--primary-2);
    --secondary-color: var(--accent);
    --background: var(--bg);
    --error: var(--red);
    --success: var(--green);
    --danger: #ef4444;
    --text-primary: var(--text);
    --text-secondary: var(--text-2);
    --border-color: var(--border);
}

* { box-sizing: border-box; margin: 0; padding: 0; }
html { color-scheme: dark; scroll-padding-top: calc(var(--topbar-h) + 60px); }
body {
    background:
        radial-gradient(1200px 500px at 85% -10%, rgba(34, 211, 238, 0.07), transparent 60%),
        radial-gradient(900px 500px at 0% -10%, rgba(139, 92, 246, 0.12), transparent 60%),
        var(--bg);
    background-attachment: fixed;
    color: var(--text);
    font-family: var(--font);
    font-size: 14px;
    line-height: 1.5;
    -webkit-font-smoothing: antialiased;
    min-height: 100vh;
}
a { color: var(--primary-3); text-decoration: none; }
a:hover { color: #fff; }
::selection { background: rgba(139, 92, 246, 0.45); color: #fff; }
::-webkit-scrollbar { width: 10px; height: 10px; }
::-webkit-scrollbar-track { background: transparent; }
::-webkit-scrollbar-thumb { background: var(--surface-3); border-radius: 10px; border: 2px solid var(--bg); }
::-webkit-scrollbar-thumb:hover { background: var(--border-strong); }
:focus-visible { outline: 2px solid var(--primary-2); outline-offset: 2px; }
.i { width: 16px; height: 16px; flex: none; }
.mono, code, kbd, .input.mono, .code, .stack { font-family: var(--mono); font-variant-ligatures: none; }
.muted { color: var(--text-3); }
.nowrap { white-space: nowrap; }
.ta-r { text-align: right !important; }
[hidden] { display: none !important; }
kbd {
    font-family: var(--mono); font-size: 11px; line-height: 1;
    padding: 3px 6px; border-radius: 6px;
    background: var(--surface-3); border: 1px solid var(--border-strong); border-bottom-width: 2px;
    color: var(--text-2);
}

/* ---------- Barra superior ---------- */
.topbar {
    position: sticky; top: 0; z-index: 60;
    background: rgba(10, 12, 20, 0.78);
    backdrop-filter: blur(14px); -webkit-backdrop-filter: blur(14px);
    border-bottom: 1px solid var(--border);
}
.topbar::after {
    content: ''; position: absolute; left: 0; right: 0; bottom: -1px; height: 1px;
    background: linear-gradient(90deg, transparent, rgba(139, 92, 246, 0.6), rgba(34, 211, 238, 0.5), transparent);
}
.topbar-inner { max-width: 1480px; margin: 0 auto; padding: 0 24px; height: var(--topbar-h); display: flex; align-items: center; gap: 18px; }
.brand { display: flex; align-items: center; gap: 12px; min-width: 0; }
.logo-container { display: flex; align-items: center; }
.logo-link { display: block; transition: transform 0.25s ease; }
.logo-link:hover { transform: scale(1.04); }
.hubble-logo { height: 40px; width: auto; max-width: 128px; display: block; }
.logo-link:hover .hubble-logo text { fill: var(--primary-3); }
.brand-meta { display: flex; flex-direction: column; line-height: 1.15; padding-left: 12px; border-left: 1px solid var(--border); }
.app-title { font-weight: 700; font-size: 14px; background: var(--brand-gradient); -webkit-background-clip: text; background-clip: text; color: transparent; }
.app-version { font-size: 11px; color: var(--text-3); font-family: var(--mono); }
.nav { display: flex; gap: 4px; margin-left: 8px; }
.nav a {
    display: inline-flex; align-items: center; gap: 8px;
    height: 36px; padding: 0 14px; border-radius: 10px;
    color: var(--text-2); font-weight: 600; font-size: 13.5px;
    transition: background 0.15s, color 0.15s;
}
.nav a:hover { background: var(--surface-2); color: var(--text); }
.nav a.active { background: rgba(139, 92, 246, 0.16); color: var(--primary-3); box-shadow: inset 0 0 0 1px rgba(139, 92, 246, 0.35); }
.topbar-right { margin-left: auto; display: flex; align-items: center; gap: 10px; }

.container { max-width: 1480px; margin: 0 auto; padding: 24px; }

/* ---------- Encabezado de página ---------- */
.page-head { display: flex; align-items: flex-end; justify-content: space-between; gap: 16px; flex-wrap: wrap; margin-bottom: 18px; }
.page-h1 { font-size: 24px; font-weight: 750; letter-spacing: -0.02em; color: var(--text); margin: 0; }
.page-sub { color: var(--text-3); font-size: 13.5px; margin-top: 2px; }
.page-head-actions { display: flex; gap: 8px; flex-wrap: wrap; align-items: center; }
.page-title { color: var(--text); margin-bottom: 18px; font-size: 22px; }

/* ---------- Botones ---------- */
.btn {
    display: inline-flex; align-items: center; justify-content: center; gap: 8px;
    height: 38px; padding: 0 16px; border-radius: 10px;
    border: 1px solid var(--border-strong);
    background: var(--surface-2); color: var(--text);
    font: inherit; font-weight: 600; font-size: 13.5px; line-height: 1;
    cursor: pointer; white-space: nowrap; text-decoration: none;
    transition: background 0.15s, border-color 0.15s, color 0.15s, box-shadow 0.15s, transform 0.05s;
}
.btn:hover { background: var(--surface-3); color: #fff; }
.btn:active { transform: translateY(1px); }
.btn.primary { background: linear-gradient(135deg, #7c3aed, #8b5cf6 60%, #6366f1); border-color: transparent; color: #fff; box-shadow: 0 6px 18px -8px var(--primary-glow), inset 0 1px 0 rgba(255,255,255,0.15); }
.btn.primary:hover { filter: brightness(1.12); }
.btn.secondary, .btn.ghost { background: transparent; color: var(--text-2); }
.btn.secondary:hover, .btn.ghost:hover { background: var(--surface-2); color: var(--text); }
.btn.soft { background: rgba(139, 92, 246, 0.14); border-color: rgba(139, 92, 246, 0.35); color: var(--primary-3); }
.btn.soft:hover { background: rgba(139, 92, 246, 0.26); color: #fff; }
.btn.danger { background: rgba(239, 68, 68, 0.1); border-color: rgba(239, 68, 68, 0.4); color: #fca5a5; }
.btn.danger:hover { background: #ef4444; border-color: #ef4444; color: #fff; }
.btn.small { height: 30px; padding: 0 11px; font-size: 12.5px; border-radius: 8px; gap: 6px; }
.btn.small .i { width: 14px; height: 14px; }
.btn.icon { width: 38px; padding: 0; }
.btn.icon.small { width: 30px; }
.btn.on { background: rgba(74, 222, 128, 0.12); border-color: rgba(74, 222, 128, 0.45); color: var(--green); }
.btn .i-check { display: none; }
.btn.is-copied { border-color: rgba(74, 222, 128, 0.5); color: var(--green); }
.btn.is-copied .i-copy { display: none; }
.btn.is-copied .i-check { display: inline; }
.live-dot { width: 8px; height: 8px; border-radius: 50%; background: var(--text-3); flex: none; }
.btn.on .live-dot { background: var(--green); box-shadow: 0 0 0 0 rgba(74, 222, 128, 0.7); animation: live-pulse 1.6s infinite; }
@keyframes live-pulse { 0% { box-shadow: 0 0 0 0 rgba(74, 222, 128, 0.6); } 70% { box-shadow: 0 0 0 8px rgba(74, 222, 128, 0); } 100% { box-shadow: 0 0 0 0 rgba(74, 222, 128, 0); } }

/* ---------- Campos ---------- */
.field { position: relative; display: flex; align-items: center; min-width: 0; }
.field.grow { flex: 1 1 280px; }
.field > .i { position: absolute; left: 12px; color: var(--text-3); pointer-events: none; }
.field > kbd { position: absolute; right: 10px; pointer-events: none; }
.input, .select {
    height: 38px; width: 100%;
    background: var(--bg-2); border: 1px solid var(--border); border-radius: 10px;
    color: var(--text); font: inherit; font-size: 13.5px;
    padding: 0 12px; outline: none;
    transition: border-color 0.15s, box-shadow 0.15s, background 0.15s;
}
.input::placeholder { color: var(--text-3); }
.input:hover, .select:hover { border-color: var(--border-strong); }
.input:focus, .select:focus { border-color: var(--primary); box-shadow: 0 0 0 3px rgba(139, 92, 246, 0.22); background: #0c0f1c; }
.field > .i + .input { padding-left: 36px; }
.field > .input:not(:last-child) { padding-right: 40px; }
.input.sm, .select.sm { height: 30px; font-size: 12.5px; border-radius: 8px; }
.input.mono { font-family: var(--mono); }
.select {
    width: auto; min-width: 150px; padding-right: 34px; cursor: pointer;
    appearance: none; -webkit-appearance: none;
    background-image: url(""data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' width='14' height='14' viewBox='0 0 24 24' fill='none' stroke='%23a7adcb' stroke-width='2.5' stroke-linecap='round'%3E%3Cpath d='m6 9 6 6 6-6'/%3E%3C/svg%3E"");
    background-repeat: no-repeat; background-position: right 11px center;
}
select option { background: var(--surface); color: var(--text); }
input[type=search]::-webkit-search-cancel-button { filter: invert(0.7); cursor: pointer; }

/* ---------- Chips ---------- */
.chips { display: flex; flex-wrap: wrap; gap: 6px; align-items: center; }
.chip {
    position: relative; display: inline-flex; align-items: center; gap: 7px;
    height: 30px; padding: 0 12px; border-radius: 999px;
    border: 1px solid var(--border); background: var(--bg-2);
    color: var(--text-2); font: inherit; font-size: 12.5px; font-weight: 650;
    cursor: pointer; user-select: none; white-space: nowrap;
    transition: background 0.15s, border-color 0.15s, color 0.15s;
}
.chip input { position: absolute; opacity: 0; pointer-events: none; width: 0; height: 0; }
.chip:hover { border-color: var(--border-strong); color: var(--text); }
.chip .dot { width: 7px; height: 7px; border-radius: 50%; background: var(--c, var(--text-2)); }
.chip.active, .chip:has(input:checked) {
    color: var(--c, var(--text)); border-color: color-mix(in srgb, var(--c, var(--text-2)) 55%, transparent);
    background: color-mix(in srgb, var(--c, var(--text-2)) 15%, transparent);
}
.chip.all { --c: var(--primary-3); }
.chip:focus-within { box-shadow: 0 0 0 3px rgba(139, 92, 246, 0.25); }
.chip .count { font-size: 11px; padding: 0 6px; border-radius: 999px; background: var(--surface-3); color: var(--text-2); }
.chip.saved { padding-right: 4px; }
.chip.saved .chip-x { display: inline-flex; align-items: center; justify-content: center; width: 22px; height: 22px; border-radius: 50%; border: none; background: transparent; color: var(--text-3); cursor: pointer; }
.chip.saved .chip-x:hover { background: rgba(239, 68, 68, 0.2); color: var(--red); }
.chip.saved .chip-x .i { width: 12px; height: 12px; }

/* Colores por método, estado y nivel */
.m-get { --c: var(--blue); }
.m-post { --c: var(--green); }
.m-put { --c: var(--amber); }
.m-patch { --c: var(--violet); }
.m-delete { --c: var(--red); }
.m-options, .m-head, .m-other { --c: var(--gray); }
.s-2 { --c: var(--green); }
.s-3 { --c: var(--accent); }
.s-4 { --c: var(--amber); }
.s-5 { --c: var(--red); }
.s-0 { --c: var(--gray); }
.l-information { --c: var(--accent); }
.l-warning { --c: var(--amber); }
.l-error, .l-critical { --c: var(--red); }
.l-debug, .l-trace, .l-none { --c: var(--gray); }

.badge {
    display: inline-flex; align-items: center; gap: 6px;
    height: 24px; padding: 0 9px; border-radius: 7px;
    font-size: 11.5px; font-weight: 750; letter-spacing: 0.03em;
    color: var(--c, var(--gray));
    background: color-mix(in srgb, var(--c, var(--gray)) 14%, transparent);
    border: 1px solid color-mix(in srgb, var(--c, var(--gray)) 32%, transparent);
    white-space: nowrap; font-family: var(--mono);
}
.badge.lg { height: 30px; padding: 0 12px; font-size: 13px; border-radius: 9px; }
.badge.pill { border-radius: 999px; }
.badge .dot { width: 7px; height: 7px; border-radius: 50%; background: var(--c, var(--gray)); }
.badge.plain { font-family: var(--font); letter-spacing: 0; font-weight: 650; }

/* Compatibilidad: niveles de log usados en otras vistas */
.log-level { display: inline-block; padding: 2px 8px; border-radius: 6px; font-size: 12px; font-weight: 600; }
.log-level.information { background: rgba(34, 211, 238, 0.15); color: var(--accent); }
.log-level.warning { background: rgba(251, 191, 36, 0.15); color: var(--amber); }
.log-level.error, .log-level.critical { background: rgba(248, 113, 113, 0.15); color: var(--red); }
.log-level.debug, .log-level.trace { background: rgba(255, 255, 255, 0.08); color: var(--text-2); }

/* ---------- Tarjetas de estadísticas ---------- */
.stats { display: grid; grid-template-columns: repeat(auto-fit, minmax(175px, 1fr)); gap: 14px; margin-bottom: 18px; }
.stat {
    --c: var(--primary-2);
    position: relative; overflow: hidden; display: block;
    background: var(--surface); border: 1px solid var(--border); border-radius: var(--radius);
    padding: 14px 16px 14px; color: var(--text);
    transition: border-color 0.15s, transform 0.15s, background 0.15s;
}
.stat::before { content: ''; position: absolute; inset: 0 0 auto 0; height: 2px; background: var(--c); opacity: 0.9; }
.stat::after { content: ''; position: absolute; width: 140px; height: 140px; right: -60px; top: -70px; background: radial-gradient(circle, color-mix(in srgb, var(--c) 22%, transparent), transparent 70%); pointer-events: none; }
a.stat:hover { border-color: color-mix(in srgb, var(--c) 50%, transparent); transform: translateY(-1px); color: var(--text); }
.stat.active { border-color: color-mix(in srgb, var(--c) 60%, transparent); background: color-mix(in srgb, var(--c) 7%, var(--surface)); }
.stat-label { display: flex; align-items: center; gap: 7px; font-size: 11.5px; font-weight: 700; color: var(--text-3); text-transform: uppercase; letter-spacing: 0.07em; }
.stat-label .i { color: var(--c); width: 15px; height: 15px; }
.stat-value { font-size: 26px; font-weight: 750; letter-spacing: -0.02em; margin-top: 4px; line-height: 1.2; font-variant-numeric: tabular-nums; }
.stat-value small { font-size: 13px; font-weight: 600; color: var(--text-3); margin-left: 3px; }
.stat-sub { font-size: 12px; color: var(--text-2); margin-top: 2px; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
.meter { height: 4px; border-radius: 4px; background: var(--surface-3); margin-top: 10px; overflow: hidden; }
.meter > span { display: block; height: 100%; background: var(--c); border-radius: 4px; }

/* ---------- Paneles ---------- */
.panel { background: var(--surface); border: 1px solid var(--border); border-radius: 16px; margin-bottom: 18px; }
.filters { padding: 14px 16px; display: flex; flex-direction: column; gap: 12px; }
.filter-form { display: flex; flex-direction: column; gap: 12px; }
.filter-row { display: flex; flex-wrap: wrap; align-items: center; gap: 10px; }
.filter-row + .filter-row.bordered, .filter-row.bordered { padding-top: 12px; border-top: 1px dashed var(--border); }
.filter-label { display: inline-flex; align-items: center; gap: 6px; font-size: 11px; font-weight: 750; color: var(--text-3); text-transform: uppercase; letter-spacing: 0.08em; margin-right: 2px; }
.filter-label .i { width: 14px; height: 14px; }
.vsep { width: 1px; height: 22px; background: var(--border); margin: 0 6px; }
.refine-count { font-size: 12.5px; color: var(--text-2); white-space: nowrap; }
.refine-count b { color: var(--primary-3); }
.save-form { display: inline-flex; gap: 6px; align-items: center; }
.save-form .input { width: 220px; }
.syntax-help { display: grid; grid-template-columns: repeat(auto-fit, minmax(230px, 1fr)); gap: 6px 18px; padding: 12px 14px; background: var(--bg-2); border: 1px solid var(--border); border-radius: 12px; font-size: 12.5px; color: var(--text-2); }
.syntax-help code { font-family: var(--mono); color: var(--primary-3); background: rgba(139, 92, 246, 0.12); padding: 1px 6px; border-radius: 5px; cursor: pointer; }
.syntax-help code:hover { background: rgba(139, 92, 246, 0.25); color: #fff; }

/* ---------- Tabla de logs ---------- */
.results-head { display: flex; align-items: center; justify-content: space-between; gap: 12px; flex-wrap: wrap; padding: 12px 16px; border-bottom: 1px solid var(--border); }
.results-title { color: var(--text-2); font-size: 13px; }
.results-title b { color: var(--text); font-variant-numeric: tabular-nums; }
.results-actions { display: flex; gap: 8px; align-items: center; flex-wrap: wrap; }
.results-actions form { margin: 0; }
.table-wrap { overflow-x: auto; }
table.logs { width: 100%; border-collapse: separate; border-spacing: 0; }
.logs th {
    position: sticky; top: 0; z-index: 2;
    padding: 10px 14px; text-align: left; white-space: nowrap;
    font-size: 11px; font-weight: 750; color: var(--text-3); text-transform: uppercase; letter-spacing: 0.07em;
    background: var(--surface); border-bottom: 1px solid var(--border);
}
.logs th.sortable { cursor: pointer; user-select: none; }
.logs th.sortable:hover { color: var(--text); }
.logs th .sort-ind { display: inline-block; width: 10px; margin-left: 4px; color: var(--primary-2); }
.logs th.sorted { color: var(--primary-3); }
.logs td { padding: 10px 14px; border-bottom: 1px solid var(--border); vertical-align: middle; }
.logs tbody tr:last-child td { border-bottom: none; }
.logs tbody tr.row { cursor: pointer; transition: background 0.12s; }
.logs tbody tr.row:hover { background: var(--surface-2); }
.logs tbody tr.row td:first-child { box-shadow: inset 3px 0 0 var(--row-c, transparent); }
.logs tbody tr.row.st-4 { --row-c: rgba(251, 191, 36, 0.8); }
.logs tbody tr.row.st-5 { --row-c: #ef4444; background: rgba(239, 68, 68, 0.05); }
.logs tbody tr.row.st-5:hover { background: rgba(239, 68, 68, 0.1); }
.logs tbody tr.row.is-selected { background: rgba(139, 92, 246, 0.12); }
.logs tbody tr.row.is-selected td:first-child { box-shadow: inset 3px 0 0 var(--primary); }
.logs tbody tr.row.new-service { animation: row-new 2.4s ease-out infinite alternate; }
.logs tbody tr.row.new-service td:first-child { box-shadow: inset 3px 0 0 var(--amber); }
@keyframes row-new { from { background: rgba(251, 191, 36, 0.16); } to { background: rgba(251, 191, 36, 0.04); } }
.new-tag { display: inline-block; font-size: 10px; font-weight: 800; letter-spacing: 0.06em; color: #1a1300; background: var(--amber); border-radius: 5px; padding: 1px 5px; margin-left: 6px; vertical-align: 1px; }
.c-time { white-space: nowrap; width: 1%; }
.t-rel { font-weight: 650; color: var(--text); font-size: 13px; }
.t-abs { font-family: var(--mono); font-size: 11.5px; color: var(--text-3); }
.c-type, .c-method, .c-status { width: 1%; white-space: nowrap; }
.c-url { max-width: 0; width: 100%; }
.u-path { font-family: var(--mono); font-size: 13px; color: var(--text); white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
.u-q { display: inline-block; max-width: 100%; margin-top: 3px; font-family: var(--mono); font-size: 11.5px; color: var(--primary-3); background: rgba(139, 92, 246, 0.1); border: 1px solid rgba(139, 92, 246, 0.2); padding: 0 6px; border-radius: 5px; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; vertical-align: top; }
.u-msg { font-size: 12.5px; color: var(--text-2); white-space: nowrap; overflow: hidden; text-overflow: ellipsis; margin-top: 2px; }
.u-cat { font-family: var(--mono); font-size: 12px; color: var(--accent); white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
.c-dur { width: 1%; white-space: nowrap; }
.dur { --c: var(--green); display: flex; flex-direction: column; gap: 5px; min-width: 84px; }
.dur.mid { --c: var(--amber); }
.dur.slow { --c: var(--red); }
.dur-v { font-family: var(--mono); font-weight: 650; font-size: 13px; color: var(--c); font-variant-numeric: tabular-nums; }
.dur-bar { height: 3px; border-radius: 3px; background: var(--surface-3); overflow: hidden; }
.dur-bar > i { display: block; height: 100%; background: var(--c); border-radius: 3px; min-width: 2px; }
.c-act { width: 1%; white-space: nowrap; }
.row-actions { display: flex; gap: 4px; justify-content: flex-end; align-items: center; }
.row-actions .reveal { opacity: 0; transition: opacity 0.12s; }
.logs tr.row:hover .reveal, .logs tr.row.is-selected .reveal, .row-actions .reveal:focus-visible { opacity: 1; }
@media (hover: none) { .row-actions .reveal { opacity: 1; } }
body.compact .logs td { padding-top: 5px; padding-bottom: 5px; }
body.compact .t-abs, body.compact .dur-bar { display: none; }
body.compact .u-q { margin-top: 0; margin-left: 6px; }
body.compact .u-path { display: inline; }
.empty { padding: 56px 16px; text-align: center; color: var(--text-3); }
.empty .i { width: 42px; height: 42px; opacity: 0.6; margin-bottom: 10px; color: var(--primary-2); }
.empty h3 { color: var(--text); font-size: 16px; margin-bottom: 4px; }
.empty p { margin-bottom: 14px; }

@media (min-width: 1101px) {
    .table-wrap { overflow: visible; }
    .logs th { top: var(--topbar-h); }
}
.q-time.fast { --c: var(--green); }
.q-time.mid { --c: var(--amber); }
.q-time.slow { --c: var(--red); }
.q-time .dur-v { color: var(--c); }

/* ---------- Paginación ---------- */
.pager { display: flex; justify-content: space-between; align-items: center; gap: 12px; flex-wrap: wrap; padding: 12px 16px; border-top: 1px solid var(--border); }
.pager-info { color: var(--text-2); font-size: 13px; }
.pager-nav { display: flex; gap: 4px; align-items: center; flex-wrap: wrap; }
.page-btn {
    min-width: 34px; height: 34px; padding: 0 8px;
    display: inline-flex; align-items: center; justify-content: center;
    border-radius: 9px; color: var(--text-2); font-weight: 650; font-size: 13px;
    border: 1px solid transparent; font-variant-numeric: tabular-nums;
}
.page-btn:hover { background: var(--surface-2); color: var(--text); border-color: var(--border); }
.page-btn.current { background: var(--brand-gradient); color: #fff; box-shadow: 0 4px 14px -6px var(--primary-glow); }
.page-btn.disabled { opacity: 0.3; pointer-events: none; }
.page-ellipsis { color: var(--text-3); padding: 0 4px; }
.page-jump { display: inline-flex; gap: 6px; align-items: center; color: var(--text-3); font-size: 12.5px; margin-left: 8px; }
.page-jump .input { width: 64px; text-align: center; }

/* ---------- Detalle: cabecera ---------- */
.crumbs { display: flex; align-items: center; gap: 8px; color: var(--text-3); font-size: 13px; margin-bottom: 14px; flex-wrap: wrap; }
.crumbs a { color: var(--text-2); display: inline-flex; align-items: center; gap: 6px; }
.crumbs a:hover { color: #fff; }
.crumbs .i { width: 14px; height: 14px; }
.crumbs .id { font-family: var(--mono); color: var(--text-3); }
.hero {
    position: relative; overflow: hidden;
    background: linear-gradient(160deg, rgba(139, 92, 246, 0.13), rgba(34, 211, 238, 0.04) 45%, transparent 70%), var(--surface);
    border: 1px solid var(--border); border-radius: 18px; padding: 20px 22px; margin-bottom: 16px;
}
.hero.is-error { background: linear-gradient(160deg, rgba(239, 68, 68, 0.16), rgba(239, 68, 68, 0.03) 50%, transparent 75%), var(--surface); border-color: rgba(239, 68, 68, 0.35); }
.hero.is-warn { background: linear-gradient(160deg, rgba(251, 191, 36, 0.13), transparent 60%), var(--surface); }
.hero-top { display: flex; flex-wrap: wrap; gap: 12px; align-items: center; justify-content: space-between; }
.hero-badges { display: flex; gap: 8px; flex-wrap: wrap; align-items: center; }
.hero-actions { display: flex; gap: 8px; flex-wrap: wrap; }
.hero-url { display: flex; align-items: flex-start; gap: 10px; margin: 16px 0 18px; }
.hero-url-text { font-family: var(--mono); font-size: 19px; font-weight: 650; word-break: break-all; line-height: 1.4; color: #fff; }
.hero-url-text .q { color: var(--primary-3); font-weight: 500; }
.hero-msg { font-family: var(--mono); font-size: 15px; white-space: pre-wrap; word-break: break-word; color: #fff; margin: 14px 0 16px; line-height: 1.5; max-height: 220px; overflow: auto; }
.meta-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(190px, 1fr)); gap: 1px; background: var(--border); border: 1px solid var(--border); border-radius: 12px; overflow: hidden; }
.meta { background: var(--bg-2); padding: 10px 14px; min-width: 0; position: relative; }
.meta-k { display: flex; align-items: center; gap: 6px; font-size: 11px; font-weight: 750; text-transform: uppercase; letter-spacing: 0.07em; color: var(--text-3); }
.meta-k .i { width: 13px; height: 13px; }
.meta-v { font-weight: 600; margin-top: 3px; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
.meta-v.mono { font-size: 13px; }
.meta-v small { color: var(--text-3); font-weight: 500; margin-left: 6px; }
.meta .btn.icon { position: absolute; right: 8px; bottom: 8px; opacity: 0; width: 26px; height: 26px; background: var(--surface-3); }
.meta:hover .btn.icon, .meta .btn.icon:focus-visible { opacity: 1; }

/* ---------- Mini KPIs ---------- */
.mini-stats { display: grid; grid-template-columns: repeat(auto-fit, minmax(160px, 1fr)); gap: 12px; margin-bottom: 16px; }
.mini { --c: var(--primary-2); display: flex; align-items: center; gap: 12px; background: var(--surface); border: 1px solid var(--border); border-radius: 14px; padding: 12px 14px; color: var(--text); cursor: pointer; transition: border-color 0.15s, background 0.15s; text-align: left; font: inherit; }
.mini:hover { border-color: color-mix(in srgb, var(--c) 50%, transparent); background: color-mix(in srgb, var(--c) 6%, var(--surface)); }
.mini-ico { width: 36px; height: 36px; border-radius: 10px; display: grid; place-items: center; color: var(--c); background: color-mix(in srgb, var(--c) 15%, transparent); flex: none; }
.mini-ico .i { width: 18px; height: 18px; }
.mini-text { display: flex; flex-direction: column; min-width: 0; }
.mini-k { font-size: 11px; font-weight: 700; color: var(--text-3); text-transform: uppercase; letter-spacing: 0.06em; }
.mini-v { font-size: 18px; font-weight: 750; font-variant-numeric: tabular-nums; line-height: 1.25; color: var(--text); }
.mini-v small { font-size: 12px; color: var(--text-3); font-weight: 600; margin-left: 2px; }

/* ---------- Pestañas ---------- */
.tabs {
    position: sticky; top: var(--topbar-h); z-index: 20;
    display: flex; gap: 2px; overflow-x: auto; scrollbar-width: none;
    background: rgba(10, 12, 20, 0.88); backdrop-filter: blur(10px); -webkit-backdrop-filter: blur(10px);
    border-bottom: 1px solid var(--border); margin: 0 -24px 16px; padding: 6px 24px 0;
}
.tabs::-webkit-scrollbar { display: none; }
.tab {
    display: inline-flex; align-items: center; gap: 8px; white-space: nowrap;
    padding: 10px 14px 11px; margin-bottom: -1px;
    border: none; border-bottom: 2px solid transparent; background: none;
    color: var(--text-2); font: inherit; font-weight: 650; font-size: 13.5px; cursor: pointer;
    border-radius: 8px 8px 0 0; transition: color 0.15s, background 0.15s;
}
.tab .i { width: 15px; height: 15px; }
.tab:hover { color: var(--text); background: var(--surface); }
.tab.active { color: #fff; border-bottom-color: var(--primary); }
.tab.active .i { color: var(--primary-2); }
.tab .count { font-size: 11px; font-weight: 700; min-width: 20px; padding: 1px 7px; border-radius: 999px; background: var(--surface-3); color: var(--text-2); text-align: center; }
.tab.active .count { background: var(--primary); color: #fff; }
.tab.tab-error { color: #fca5a5; }
.tab.tab-error .i { color: var(--red); }
.tab.tab-error .count { background: rgba(239, 68, 68, 0.22); color: #fecaca; }
.tab.tab-error.active { border-bottom-color: #ef4444; }
.tab .key { font-family: var(--mono); font-size: 10px; color: var(--text-3); opacity: 0; transition: opacity 0.15s; }
.tabs:hover .tab .key { opacity: 1; }
.tab-panel { display: none; }
.tab-panel.active { display: block; animation: fade-in 0.18s ease; }
@keyframes fade-in { from { opacity: 0; transform: translateY(4px); } to { opacity: 1; transform: none; } }

/* ---------- Secciones ---------- */
.grid-2 { display: grid; grid-template-columns: repeat(auto-fit, minmax(380px, 1fr)); gap: 16px; align-items: start; }
.grid-2 > .section { margin-bottom: 0; }
.vstack { display: flex; flex-direction: column; gap: 16px; min-width: 0; }
.section { background: var(--surface); border: 1px solid var(--border); border-radius: 14px; margin-bottom: 16px; overflow: hidden; min-width: 0; }
.section-head { display: flex; align-items: center; justify-content: space-between; gap: 10px; flex-wrap: wrap; padding: 10px 14px 10px 16px; border-bottom: 1px solid var(--border); background: linear-gradient(180deg, rgba(255,255,255,0.015), transparent); }
.section-title { display: flex; align-items: center; gap: 8px; font-weight: 700; font-size: 14px; color: var(--text); }
.section-title .i { color: var(--primary-2); }
.section-title .count { font-size: 11px; font-weight: 700; padding: 1px 7px; border-radius: 999px; background: var(--surface-3); color: var(--text-2); }
.section-tools { display: flex; gap: 6px; align-items: center; flex-wrap: wrap; }
.section-body { padding: 14px 16px; }
.section.error-section { border-color: rgba(239, 68, 68, 0.4); }
.section.error-section .section-title .i { color: var(--red); }
.code-meta { font-size: 12px; color: var(--text-3); font-variant-numeric: tabular-nums; }
.match-info { font-size: 12px; color: var(--text-2); min-width: 50px; text-align: right; font-variant-numeric: tabular-nums; }
.tool-search { width: 190px; }

/* Tabla clave/valor */
.kv { width: 100%; border-collapse: collapse; font-size: 13px; table-layout: fixed; }
.kv td { padding: 7px 16px; border-bottom: 1px solid var(--border); vertical-align: top; }
.kv tr:last-child td { border-bottom: none; }
.kv tr:hover td { background: var(--surface-2); }
.kv td.k { width: 30%; color: var(--primary-3); font-family: var(--mono); font-size: 12.5px; word-break: break-all; }
.kv td.v { font-family: var(--mono); font-size: 12.5px; word-break: break-all; color: var(--text); }
.kv td.a { width: 48px; text-align: right; padding-right: 10px; }
.kv td.a .btn { opacity: 0; }
.kv tr:hover td.a .btn, .kv td.a .btn:focus-visible { opacity: 1; }
.masked { display: inline-flex; align-items: center; gap: 5px; color: var(--amber); font-family: var(--font); font-size: 11.5px; font-weight: 700; background: rgba(251, 191, 36, 0.12); border: 1px solid rgba(251, 191, 36, 0.3); padding: 1px 8px; border-radius: 999px; }
.kv-empty { padding: 18px 16px; color: var(--text-3); font-size: 13px; }

/* Visor de código */
.code {
    margin: 0; padding: 14px 16px;
    background: #080a12; color: #d4d8ee;
    font-family: var(--mono); font-size: 12.8px; line-height: 1.65; tab-size: 2;
    white-space: pre; overflow: auto; max-height: 560px;
    counter-reset: line;
}
.code.wrap { white-space: pre-wrap; word-break: break-word; }
.code.expanded { max-height: none; }
.code.small { max-height: 320px; }
.code-block { background: #080a12; padding: 14px 16px; border-radius: 10px; font-family: var(--mono); font-size: 12.8px; white-space: pre-wrap; word-break: break-word; overflow: auto; }
.j-key { color: #c4b5fd; }
.j-str { color: #86efac; }
.j-num { color: #fbbf24; }
.j-bool { color: #f472b6; font-weight: 600; }
.j-null { color: #94a3b8; font-style: italic; }
.j-punc { color: #5b6388; }
.s-kw { color: #60a5fa; font-weight: 700; }
.s-par { color: #f472b6; }
mark { background: rgba(250, 204, 21, 0.3); color: inherit; border-radius: 3px; box-shadow: 0 0 0 1px rgba(250, 204, 21, 0.4); }
mark.current { background: #facc15; color: #111; }

/* Stack trace */
.stack { font-family: var(--mono); font-size: 12.5px; line-height: 1.7; padding: 12px 16px; background: #080a12; overflow: auto; max-height: 600px; }
.st-line { display: block; white-space: pre-wrap; word-break: break-word; padding: 1px 6px; border-radius: 4px; color: #d4d8ee; }
.st-line.app { background: rgba(139, 92, 246, 0.1); }
.st-line.fw { color: var(--text-3); }
.st-line .st-src { color: var(--accent); }
.stack.hide-fw .st-line.fw { display: none; }

/* Alertas */
.alert { display: flex; gap: 12px; align-items: flex-start; padding: 14px 16px; border-radius: 12px; border: 1px solid rgba(239, 68, 68, 0.4); background: rgba(239, 68, 68, 0.08); margin-bottom: 16px; }
.alert > .i { width: 20px; height: 20px; color: var(--red); margin-top: 1px; }
.alert-title { font-weight: 700; color: #fecaca; }
.alert-text { font-family: var(--mono); font-size: 13px; color: #fca5a5; white-space: pre-wrap; word-break: break-word; margin-top: 2px; }
.alert .btn { margin-left: auto; }
.info-note { display: flex; gap: 10px; align-items: center; padding: 10px 14px; border-radius: 10px; background: rgba(34, 211, 238, 0.07); border: 1px solid rgba(34, 211, 238, 0.25); color: #a5f3fc; font-size: 13px; }
.info-note .i { color: var(--accent); }

/* Desglose de tiempo */
.timing { display: flex; flex-direction: column; gap: 14px; }
.tbar { display: flex; height: 14px; border-radius: 7px; overflow: hidden; background: var(--surface-3); }
.tbar > span { display: block; height: 100%; }
.tbar .t-db { background: linear-gradient(90deg, #06b6d4, #22d3ee); }
.tbar .t-app { background: linear-gradient(90deg, #7c3aed, #a78bfa); }
.legend { display: flex; flex-wrap: wrap; gap: 8px 18px; font-size: 12.5px; color: var(--text-2); }
.legend span { display: inline-flex; align-items: center; gap: 7px; }
.legend i { width: 10px; height: 10px; border-radius: 3px; display: inline-block; }
.legend b { color: var(--text); font-variant-numeric: tabular-nums; }
.level-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(120px, 1fr)); gap: 10px; }
.level-card { border: 1px solid var(--border); border-radius: 12px; padding: 10px 12px; background: var(--bg-2); cursor: pointer; transition: border-color 0.15s; text-align: left; font: inherit; color: var(--text); }
.level-card:hover { border-color: color-mix(in srgb, var(--c, var(--gray)) 55%, transparent); }
.level-card .lv-k { display: flex; align-items: center; gap: 6px; font-size: 11.5px; font-weight: 700; color: var(--c, var(--gray)); text-transform: uppercase; letter-spacing: 0.05em; }
.level-card .lv-k .dot { width: 7px; height: 7px; border-radius: 50%; background: var(--c, var(--gray)); }
.level-card .lv-v { font-size: 22px; font-weight: 750; font-variant-numeric: tabular-nums; }

/* Consultas a base de datos */
.q-item { border-bottom: 1px solid var(--border); }
.q-item:last-child { border-bottom: none; }
.q-head { display: flex; align-items: center; gap: 10px; padding: 10px 14px 10px 16px; flex-wrap: wrap; }
.q-idx { font-family: var(--mono); font-size: 11.5px; color: var(--text-3); min-width: 26px; }
.q-meta { color: var(--text-2); font-size: 12.5px; display: flex; gap: 12px; flex-wrap: wrap; align-items: center; }
.q-meta span { display: inline-flex; gap: 5px; align-items: center; }
.q-meta .i { width: 13px; height: 13px; color: var(--text-3); }
.q-time { margin-left: auto; display: flex; align-items: center; gap: 10px; }
.q-bar { width: 110px; height: 5px; border-radius: 5px; background: var(--surface-3); overflow: hidden; }
.q-bar > i { display: block; height: 100%; background: var(--c, var(--green)); border-radius: 5px; min-width: 2px; }
.q-item .code { max-height: 300px; border-top: 1px solid var(--border); }
.q-sub { padding: 8px 16px; font-size: 12px; color: var(--text-3); border-top: 1px solid var(--border); background: var(--bg-2); }
.q-item.failed .q-head { background: rgba(239, 68, 68, 0.06); }

/* Logs relacionados */
.log-row { display: grid; grid-template-columns: 96px 120px minmax(0, 1fr) auto; gap: 12px; padding: 10px 14px 10px 16px; border-bottom: 1px solid var(--border); align-items: start; }
.log-row:last-child { border-bottom: none; }
.log-row:hover { background: var(--surface-2); }
.log-row.lv-error, .log-row.lv-critical { background: rgba(239, 68, 68, 0.05); box-shadow: inset 3px 0 0 #ef4444; }
.log-row.lv-warning { box-shadow: inset 3px 0 0 var(--amber); }
.log-time { font-family: var(--mono); font-size: 12px; color: var(--text-3); padding-top: 3px; }
.log-main { min-width: 0; }
.log-msg { font-family: var(--mono); font-size: 12.8px; white-space: pre-wrap; word-break: break-word; color: var(--text); }
.log-cat { font-size: 11.5px; color: var(--accent); font-family: var(--mono); margin-top: 3px; word-break: break-all; }
.log-src { font-size: 11.5px; color: var(--text-3); font-family: var(--mono); }
.log-err { margin-top: 8px; border-radius: 8px; overflow: hidden; border: 1px solid rgba(239, 68, 68, 0.3); }
.log-err summary { cursor: pointer; padding: 6px 10px; color: #fca5a5; font-size: 12.5px; background: rgba(239, 68, 68, 0.08); }
.log-row .btn.icon { opacity: 0; }
.log-row:hover .btn.icon, .log-row .btn.icon:focus-visible { opacity: 1; }

/* ---------- Toasts y diálogos ---------- */
.toast-host { position: fixed; right: 20px; bottom: 20px; display: flex; flex-direction: column; gap: 8px; z-index: 200; pointer-events: none; }
.toast { display: flex; align-items: center; gap: 9px; padding: 10px 14px; border-radius: 12px; background: var(--surface-3); border: 1px solid var(--border-strong); color: var(--text); font-weight: 600; font-size: 13px; box-shadow: 0 12px 30px -10px rgba(0,0,0,0.6); animation: toast-in 0.2s ease; }
.toast .i { color: var(--green); }
.toast.err .i { color: var(--red); }
.toast.out { opacity: 0; transform: translateY(6px); transition: all 0.2s; }
@keyframes toast-in { from { opacity: 0; transform: translateY(8px); } to { opacity: 1; transform: none; } }
dialog.modal { margin: auto; border: 1px solid var(--border-strong); border-radius: 16px; background: var(--surface); color: var(--text); padding: 0; width: min(520px, calc(100vw - 32px)); box-shadow: 0 30px 80px -20px rgba(0,0,0,0.8); }
dialog.modal::backdrop { background: rgba(5, 6, 12, 0.65); backdrop-filter: blur(3px); }
.modal-head { display: flex; justify-content: space-between; align-items: center; padding: 14px 18px; border-bottom: 1px solid var(--border); font-weight: 700; }
.modal-body { padding: 14px 18px 18px; }
.shortcuts { display: grid; grid-template-columns: auto 1fr; gap: 9px 16px; align-items: center; font-size: 13px; color: var(--text-2); }
.shortcuts kbd { justify-self: start; }

/* ---------- Compatibilidad con otras páginas ---------- */
.card { background: var(--surface); border: 1px solid var(--border); border-radius: 16px; padding: 20px; margin-bottom: 20px; }
.error-card { background: rgba(239, 68, 68, 0.08); border-color: rgba(239, 68, 68, 0.4); }
.success-card { background: rgba(74, 222, 128, 0.07); border-color: rgba(74, 222, 128, 0.35); }
.success-card h2 { color: var(--green); margin-bottom: 6px; }
.error-text { color: var(--red); }
.success-text { color: var(--green); }
h2 { color: var(--primary-3); margin-bottom: 15px; }
h3 { color: var(--text-2); margin: 15px 0 10px; }

footer.app-footer { text-align: center; padding: 28px 16px 36px; color: var(--text-3); font-size: 12.5px; }
footer.app-footer .app-version { margin-left: 6px; }

/* ---------- Responsive ---------- */
@media (max-width: 1100px) {
    .c-type { display: none; }
}
@media (max-width: 860px) {
    :root { --topbar-h: 56px; }
    .container { padding: 16px; }
    .topbar-inner { padding: 0 16px; gap: 10px; }
    .brand-meta { display: none; }
    .nav a span, .topbar-right .btn span { display: none; }
    .nav a, .topbar-right .btn { padding: 0 10px; }
    .tabs { margin: 0 -16px 16px; padding: 6px 16px 0; }
    .c-time .t-abs, .c-method, .logs th.h-method { display: none; }
    .grid-2 { grid-template-columns: 1fr; }
    .log-row { grid-template-columns: 1fr auto; }
    .log-row .log-time { grid-column: 1; }
    .hero-url-text { font-size: 16px; }
    .vsep { display: none; }
    .q-time { margin-left: 0; }
    .stats, .mini-stats { grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 10px; }
    .stat { padding: 12px 14px; }
    .stat-value { font-size: 22px; }
    .page-h1 { font-size: 21px; }
}
";

    /// <summary>
    /// Script compartido: utilidades (copiar, toasts, tiempos relativos) y la lógica de la lista y del detalle.
    /// Solo manipula texto con textContent o con HTML ya escapado: los datos de los logs no son de confianza.
    /// </summary>
    public const string Scripts = @"
(function () {
    'use strict';
    var H = window.Hubble = {};
    var $ = function (s, r) { return (r || document).querySelector(s); };
    var $$ = function (s, r) { return Array.prototype.slice.call((r || document).querySelectorAll(s)); };
    var ICON_OK = '<svg class=i viewBox=""0 0 24 24"" fill=none stroke=currentColor stroke-width=2.5 stroke-linecap=round stroke-linejoin=round><path d=""M20 6 9 17l-5-5""/></svg>';
    var ICON_ERR = '<svg class=i viewBox=""0 0 24 24"" fill=none stroke=currentColor stroke-width=2.5 stroke-linecap=round><path d=""M18 6 6 18M6 6l12 12""/></svg>';

    function esc(s) { return String(s).replace(/[&<>'""]/g, function (c) { return '&#' + c.charCodeAt(0) + ';'; }); }
    H.esc = esc;

    var store = H.store = {
        get: function (k, d) { try { var v = localStorage.getItem('hubble:' + k); return v === null ? d : JSON.parse(v); } catch (e) { return d; } },
        set: function (k, v) { try { localStorage.setItem('hubble:' + k, JSON.stringify(v)); } catch (e) { } }
    };
    var session = H.session = {
        get: function (k) { try { return sessionStorage.getItem('hubble:' + k); } catch (e) { return null; } },
        set: function (k, v) { try { sessionStorage.setItem('hubble:' + k, v); } catch (e) { } }
    };

    H.toast = function (msg, kind) {
        var host = $('#toast-host');
        if (!host) { host = document.createElement('div'); host.id = 'toast-host'; host.className = 'toast-host'; document.body.appendChild(host); }
        var t = document.createElement('div');
        t.className = 'toast ' + (kind || 'ok');
        t.innerHTML = (kind === 'err' ? ICON_ERR : ICON_OK) + '<span></span>';
        t.lastChild.textContent = msg;
        host.appendChild(t);
        setTimeout(function () { t.classList.add('out'); setTimeout(function () { t.remove(); }, 250); }, 1900);
    };

    H.copy = function (text, btn) {
        function done(ok) {
            H.toast(ok ? 'Copiado al portapapeles' : 'No se pudo copiar', ok ? 'ok' : 'err');
            if (ok && btn) { btn.classList.add('is-copied'); setTimeout(function () { btn.classList.remove('is-copied'); }, 1300); }
        }
        function fallback() {
            var ta = document.createElement('textarea');
            ta.value = text; ta.setAttribute('readonly', ''); ta.style.position = 'fixed'; ta.style.opacity = '0';
            document.body.appendChild(ta); ta.select();
            var ok = false; try { ok = document.execCommand('copy'); } catch (e) { }
            ta.remove(); done(ok);
        }
        if (navigator.clipboard && window.isSecureContext) {
            navigator.clipboard.writeText(text).then(function () { done(true); }, fallback);
        } else { fallback(); }
    };

    H.download = function (name, text, type) {
        var blob = new Blob([text], { type: type || 'text/plain;charset=utf-8' });
        var a = document.createElement('a');
        a.href = URL.createObjectURL(blob); a.download = name;
        document.body.appendChild(a); a.click();
        setTimeout(function () { URL.revokeObjectURL(a.href); a.remove(); }, 500);
    };

    document.addEventListener('click', function (e) {
        var b = e.target.closest('[data-copy],[data-copy-target]');
        if (!b) return;
        e.preventDefault();
        var text = b.getAttribute('data-copy');
        if (text === null) {
            var el = document.querySelector(b.getAttribute('data-copy-target'));
            text = el ? el.textContent : '';
        }
        H.copy(text, b);
    });

    // Tiempos relativos que se actualizan solos
    var loadedAt = Date.now();
    function fmtAgo(s) {
        s = Math.max(0, Math.round(s));
        if (s < 5) return 'ahora mismo';
        if (s < 60) return 'hace ' + s + ' s';
        var m = Math.floor(s / 60); if (m < 60) return 'hace ' + m + ' min';
        var h = Math.floor(m / 60); if (h < 24) return 'hace ' + h + ' h';
        var d = Math.floor(h / 24); return d === 1 ? 'hace 1 día' : 'hace ' + d + ' días';
    }
    H.refreshAges = function (root) {
        $$('[data-age]', root).forEach(function (el) {
            var at = parseFloat(el.getAttribute('data-age-at')) || loadedAt;
            el.textContent = fmtAgo(parseFloat(el.getAttribute('data-age')) + (Date.now() - at) / 1000);
        });
    };
    setInterval(function () { H.refreshAges(); }, 10000);

    function isTyping(e) {
        var t = e.target;
        return t && (t.tagName === 'INPUT' || t.tagName === 'TEXTAREA' || t.tagName === 'SELECT' || t.isContentEditable);
    }
    H.isTyping = isTyping;

    // Atajos globales
    document.addEventListener('keydown', function (e) {
        if (e.ctrlKey || e.metaKey || e.altKey) return;
        if (e.key === 'Escape' && isTyping(e)) { e.target.blur(); return; }
        if (isTyping(e)) return;
        if (e.key === '/') { var s = $('[data-search-focus]'); if (s) { e.preventDefault(); s.focus(); s.select(); } }
        else if (e.key === '?') { var d = $('#shortcuts-dialog'); if (d && d.showModal) { e.preventDefault(); d.showModal(); } }
    });
    $$('[data-open-dialog]').forEach(function (b) {
        b.addEventListener('click', function () { var d = document.getElementById(b.getAttribute('data-open-dialog')); if (d && d.showModal) d.showModal(); });
    });
    $$('dialog.modal').forEach(function (d) {
        d.addEventListener('click', function (e) { if (e.target === d || e.target.closest('[data-close]')) d.close(); });
    });

    // ------------------------------------------------------------------
    // Resaltado de sintaxis (sobre texto plano, siempre escapado)
    // ------------------------------------------------------------------
    var JSON_RE = /(""(?:\\.|[^""\\])*"")(\s*:)?|\b(true|false)\b|\bnull\b|-?\d+(?:\.\d+)?(?:[eE][+-]?\d+)?|[{}\[\],]/g;
    function highlightJson(text) {
        var out = '', last = 0, m;
        JSON_RE.lastIndex = 0;
        while ((m = JSON_RE.exec(text))) {
            out += esc(text.slice(last, m.index));
            if (m[1]) out += m[2] ? '<span class=j-key>' + esc(m[1]) + '</span>' + esc(m[2]) : '<span class=j-str>' + esc(m[1]) + '</span>';
            else if (m[3]) out += '<span class=j-bool>' + m[0] + '</span>';
            else if (m[0] === 'null') out += '<span class=j-null>null</span>';
            else if (/^[{}\[\],]$/.test(m[0])) out += '<span class=j-punc>' + m[0] + '</span>';
            else out += '<span class=j-num>' + m[0] + '</span>';
            last = JSON_RE.lastIndex;
        }
        return out + esc(text.slice(last));
    }
    var SQL_RE = /('(?:''|[^'])*')|\b(SELECT|FROM|WHERE|AND|OR|NOT|INSERT|INTO|VALUES|UPDATE|SET|DELETE|JOIN|LEFT|RIGHT|INNER|OUTER|CROSS|FULL|ON|AS|ORDER|BY|GROUP|HAVING|LIMIT|OFFSET|TOP|DISTINCT|COUNT|SUM|AVG|MIN|MAX|IN|IS|NULL|LIKE|BETWEEN|EXISTS|CASE|WHEN|THEN|ELSE|END|UNION|ALL|CREATE|ALTER|DROP|TABLE|INDEX|WITH|DESC|ASC|FETCH|NEXT|ROWS|ONLY|RETURNING|OUTPUT|INSERTED|DELETED|BEGIN|COMMIT|ROLLBACK|TRANSACTION|DECLARE|CAST|COALESCE|APPLY)\b|([@$:]\w+|\?)/gi;
    function highlightSql(text) {
        var out = '', last = 0, m;
        SQL_RE.lastIndex = 0;
        while ((m = SQL_RE.exec(text))) {
            out += esc(text.slice(last, m.index));
            if (m[1]) out += '<span class=j-str>' + esc(m[1]) + '</span>';
            else if (m[2]) out += '<span class=s-kw>' + esc(m[2]) + '</span>';
            else out += '<span class=s-par>' + esc(m[0]) + '</span>';
            last = SQL_RE.lastIndex;
        }
        return out + esc(text.slice(last));
    }
    var MAX_HL = 400000;
    var rawCache = new WeakMap();
    function renderCode(pre) {
        var raw = rawCache.get(pre);
        if (raw === undefined) { raw = pre.textContent; rawCache.set(pre, raw); }
        var lang = pre.getAttribute('data-lang');
        if (raw.length > MAX_HL) { pre.textContent = raw; return; }
        if (lang === 'json') pre.innerHTML = highlightJson(raw);
        else if (lang === 'sql') pre.innerHTML = highlightSql(raw);
        else pre.textContent = raw;
    }
    function searchCode(pre, term) {
        var raw = rawCache.get(pre); if (raw === undefined) { raw = pre.textContent; rawCache.set(pre, raw); }
        if (!term) { renderCode(pre); return 0; }
        var lower = raw.toLowerCase(), t = term.toLowerCase(), out = '', last = 0, idx, n = 0;
        while ((idx = lower.indexOf(t, last)) !== -1 && n < 5000) {
            out += esc(raw.slice(last, idx)) + '<mark>' + esc(raw.slice(idx, idx + t.length)) + '</mark>';
            last = idx + t.length; n++;
        }
        pre.innerHTML = out + esc(raw.slice(last));
        return n;
    }
    H.renderCode = renderCode;

    function initCodeViewers(root) {
        $$('pre.code[data-lang]', root).forEach(renderCode);
        $$('[data-code-tools]', root).forEach(function (tools) {
            var pre = document.getElementById(tools.getAttribute('data-code-tools'));
            if (!pre) return;
            var input = $('.tool-search', tools), info = $('.match-info', tools), current = -1;
            function focusMatch(i) {
                var marks = $$('mark', pre);
                if (!marks.length) return;
                marks.forEach(function (m) { m.classList.remove('current'); });
                current = (i + marks.length) % marks.length;
                marks[current].classList.add('current');
                marks[current].scrollIntoView({ block: 'nearest', inline: 'nearest' });
                info.textContent = (current + 1) + ' / ' + marks.length;
            }
            if (input) {
                var timer;
                input.addEventListener('input', function () {
                    clearTimeout(timer);
                    timer = setTimeout(function () {
                        var n = searchCode(pre, input.value.trim());
                        current = -1;
                        info.textContent = input.value.trim() ? (n ? n + ' coincid.' : 'Sin resultados') : '';
                        if (n) focusMatch(0);
                    }, 120);
                });
                input.addEventListener('keydown', function (e) {
                    if (e.key === 'Enter') { e.preventDefault(); focusMatch(current + (e.shiftKey ? -1 : 1)); }
                });
            }
            tools.addEventListener('click', function (e) {
                var b = e.target.closest('[data-act]'); if (!b) return;
                var act = b.getAttribute('data-act');
                if (act === 'wrap') { pre.classList.toggle('wrap'); b.classList.toggle('on', pre.classList.contains('wrap')); store.set('wrap', pre.classList.contains('wrap')); }
                else if (act === 'expand') { pre.classList.toggle('expanded'); b.classList.toggle('on', pre.classList.contains('expanded')); }
                else if (act === 'download') { H.download(b.getAttribute('data-name') || 'contenido.txt', rawCache.get(pre) || pre.textContent); }
            });
            if (store.get('wrap', false)) pre.classList.add('wrap');
            var wb = $('[data-act=wrap]', tools); if (wb) wb.classList.toggle('on', pre.classList.contains('wrap'));
        });
    }

    // Filtro de filas en tablas clave/valor y listas
    function initRowFilters(root) {
        $$('[data-filter-rows]', root).forEach(function (input) {
            var target = document.querySelector(input.getAttribute('data-filter-rows'));
            if (!target) return;
            input.addEventListener('input', function () {
                var q = input.value.trim().toLowerCase();
                $$('[data-row]', target).forEach(function (r) { r.hidden = !!q && r.textContent.toLowerCase().indexOf(q) === -1; });
            });
        });
    }

    // ------------------------------------------------------------------
    // Lista de logs
    // ------------------------------------------------------------------
    function initList() {
        var page = $('#logs-page'); if (!page) return;
        session.set('lastList', location.href);
        var tbody = $('#logs-tbody'), refine = $('#refine'), form = $('#filter-form');

        if (store.get('compact', false)) document.body.classList.add('compact');
        var densityBtn = $('#density-toggle');
        if (densityBtn) {
            densityBtn.classList.toggle('on', document.body.classList.contains('compact'));
            densityBtn.addEventListener('click', function () {
                var on = document.body.classList.toggle('compact');
                densityBtn.classList.toggle('on', on); store.set('compact', on);
            });
        }

        // Envío del formulario: auto-submit y URL limpia (sin parámetros vacíos)
        $$('[data-autosubmit]').forEach(function (el) {
            el.addEventListener('change', function () { if (form.requestSubmit) form.requestSubmit(); else form.submit(); });
        });
        form.addEventListener('submit', function () {
            $$('input[name],select[name]', form).forEach(function (el) {
                if ((el.type === 'radio' && !el.checked) || el.value === '' || (el.name === 'pageSize' && el.value === '50')) el.disabled = true;
            });
        });
        window.addEventListener('pageshow', function () { $$('[disabled]', form).forEach(function (el) { el.disabled = false; }); });

        // Navegación por fila
        tbody.addEventListener('click', function (e) {
            if (e.target.closest('a,button,input,label')) return;
            var tr = e.target.closest('tr[data-href]'); if (!tr) return;
            if (String(window.getSelection ? window.getSelection() : '').length > 0) return;
            if (e.ctrlKey || e.metaKey) window.open(tr.getAttribute('data-href'), '_blank', 'noopener');
            else location.href = tr.getAttribute('data-href');
        });
        tbody.addEventListener('auxclick', function (e) {
            if (e.button !== 1 || e.target.closest('a')) return;
            var tr = e.target.closest('tr[data-href]'); if (tr) window.open(tr.getAttribute('data-href'), '_blank', 'noopener');
        });

        // --- Búsqueda avanzada sobre la página actual ---
        function tokenize(q) {
            var out = [], re = /(-)?(?:(\w+)(:|>=|<=|>|<))?(?:""([^""]*)""|(\S+))/g, m;
            while ((m = re.exec(q))) {
                var val = m[4] !== undefined ? m[4] : (m[5] || '');
                if (!m[2] && !val) continue;
                out.push({ neg: !!m[1], key: (m[2] || '').toLowerCase(), op: m[3] || '', val: val.toLowerCase() });
            }
            return out;
        }
        function cmp(a, op, b) {
            if (isNaN(b)) return false;
            switch (op) { case '>': return a > b; case '>=': return a >= b; case '<': return a < b; case '<=': return a <= b; default: return a === b; }
        }
        function matchStatus(code, v, op) {
            if (/^[1-5]xx$/.test(v)) return Math.floor(code / 100) === +v.charAt(0);
            if (op && op !== ':') return cmp(code, op, +v);
            return String(code) === v;
        }
        function rowMatches(tr, toks) {
            var d = tr.dataset, text = null;
            return toks.every(function (t) {
                var r;
                switch (t.key) {
                    case 'method': case 'm': r = (d.method || '').toLowerCase() === t.val; break;
                    case 'status': case 's': r = d.type === 'http' && matchStatus(+d.status, t.val, t.op); break;
                    case 'ms': case 'time': case 'dur': r = d.type === 'http' && cmp(+d.ms, t.op === ':' ? '=' : t.op, +t.val.replace('ms', '')); break;
                    case 'type': case 't': r = (t.val === 'log' || t.val === 'logger' || t.val === 'ilogger') ? d.type === 'log' : d.type === t.val; break;
                    case 'level': case 'l': r = (d.level || '').toLowerCase().indexOf(t.val) === 0; break;
                    case 'url': case 'path': case 'u': r = (d.url || '').toLowerCase().indexOf(t.val) !== -1; break;
                    case 'is':
                        if (t.val === 'error' || t.val === 'err') r = d.error === '1';
                        else if (t.val === 'ok' || t.val === 'success') r = d.error !== '1';
                        else if (t.val === 'new') r = tr.classList.contains('new-service');
                        else if (t.val === 'slow') r = +d.ms >= 1000;
                        else r = true;
                        break;
                    default:
                        if (text === null) text = tr.textContent.toLowerCase();
                        r = text.indexOf((t.key ? t.key + t.op : '') + t.val) !== -1;
                }
                return t.neg ? !r : r;
            });
        }
        var lastSelected = null;
        function applyRefine() {
            var q = refine ? refine.value.trim() : '';
            var toks = tokenize(q), rows = $$('tr.row', tbody), shown = 0;
            rows.forEach(function (tr) { var ok = !toks.length || rowMatches(tr, toks); tr.hidden = !ok; if (ok) shown++; });
            var count = $('#refine-count');
            if (count) count.innerHTML = q ? '<b>' + shown + '</b> de ' + rows.length + ' en esta página' : '';
            var none = $('#refine-empty');
            if (none) none.hidden = !(q && rows.length && !shown);
            $$('[data-token]').forEach(function (c) { c.classList.toggle('active', (' ' + q + ' ').indexOf(' ' + c.getAttribute('data-token') + ' ') !== -1); });
            session.set('refine', q);
        }
        if (refine) {
            var saved = session.get('refine');
            if (saved && !refine.value) refine.value = saved;
            var rt; refine.addEventListener('input', function () { clearTimeout(rt); rt = setTimeout(applyRefine, 80); });
            refine.addEventListener('keydown', function (e) { if (e.key === 'Enter') e.preventDefault(); });
        }
        $$('[data-token]').forEach(function (c) {
            c.addEventListener('click', function () {
                var tok = c.getAttribute('data-token'), q = ' ' + refine.value.trim() + ' ';
                q = q.indexOf(' ' + tok + ' ') !== -1 ? q.replace(' ' + tok + ' ', ' ') : q + tok;
                refine.value = q.trim().replace(/\s+/g, ' ') + (q.trim() ? ' ' : '');
                applyRefine(); refine.focus();
            });
        });
        var clearRefine = $('#refine-clear');
        if (clearRefine) clearRefine.addEventListener('click', function () { refine.value = ''; applyRefine(); });
        var helpBtn = $('#syntax-toggle'), help = $('#syntax-help');
        if (helpBtn && help) {
            helpBtn.addEventListener('click', function () { help.hidden = !help.hidden; helpBtn.classList.toggle('on', !help.hidden); });
            help.addEventListener('click', function (e) {
                var c = e.target.closest('code'); if (!c) return;
                refine.value = (refine.value.trim() + ' ' + c.textContent).trim() + ' '; applyRefine(); refine.focus();
            });
        }

        // --- Ordenación por columnas (en cliente) ---
        var sort = (function () { try { return JSON.parse(session.get('sort')) || null; } catch (e) { return null; } })();
        function sortValue(tr, key) {
            var d = tr.dataset;
            if (key === 'time') return +d.ticks; if (key === 'ms') return +d.ms; if (key === 'status') return +d.status;
            if (key === 'method') return d.method || ''; if (key === 'url') return (d.url || '').toLowerCase();
            return +d.idx;
        }
        function applySort() {
            $$('.logs th[data-sort]').forEach(function (th) {
                var on = sort && th.getAttribute('data-sort') === sort.key;
                th.classList.toggle('sorted', !!on);
                $('.sort-ind', th).textContent = on ? (sort.dir > 0 ? '↑' : '↓') : '';
            });
            var key = sort ? sort.key : 'idx', dir = sort ? sort.dir : 1;
            var rows = $$('tr.row', tbody);
            rows.sort(function (a, b) {
                var x = sortValue(a, key), y = sortValue(b, key);
                return (x < y ? -1 : x > y ? 1 : 0) * dir || (+a.dataset.idx - +b.dataset.idx);
            });
            rows.forEach(function (r) { tbody.appendChild(r); });
        }
        // Cada clic en una cabecera alterna: orden por defecto -> inverso -> orden original
        $$('.logs th[data-sort]').forEach(function (th) {
            th.addEventListener('click', function () {
                var key = th.getAttribute('data-sort');
                if (!sort || sort.key !== key) sort = { key: key, dir: key === 'url' || key === 'method' ? 1 : -1, step: 1 };
                else if (sort.step === 1) { sort.dir = -sort.dir; sort.step = 2; }
                else sort = null;
                session.set('sort', JSON.stringify(sort));
                applySort();
            });
        });

        // --- Navegación con teclado ---
        function visibleRows() { return $$('tr.row', tbody).filter(function (r) { return !r.hidden; }); }
        function select(tr) {
            $$('tr.is-selected', tbody).forEach(function (r) { r.classList.remove('is-selected'); });
            if (!tr) return;
            tr.classList.add('is-selected'); lastSelected = tr.getAttribute('data-id');
            tr.scrollIntoView({ block: 'nearest' });
        }
        document.addEventListener('keydown', function (e) {
            if (e.ctrlKey || e.metaKey || e.altKey || isTyping(e)) return;
            var rows = visibleRows(), cur = $('tr.is-selected', tbody), i = rows.indexOf(cur);
            if (e.key === 'j' || e.key === 'ArrowDown') { e.preventDefault(); select(rows[Math.min(rows.length - 1, i + 1)]); }
            else if (e.key === 'k' || e.key === 'ArrowUp') { if (i === -1) return; e.preventDefault(); select(rows[Math.max(0, i - 1)]); }
            else if ((e.key === 'Enter' || e.key === 'o') && cur) { e.preventDefault(); location.href = cur.getAttribute('data-href'); }
            else if (e.key === 'c' && cur) { H.copy(cur.getAttribute('data-url'), null); }
            else if (e.key === 'f' && refine) { e.preventDefault(); refine.focus(); }
            else if (e.key === 'l') { var lb = $('#live-toggle'); if (lb) lb.click(); }
            else if (e.key === 'ArrowRight' || e.key === 'n') { var nx = $('[data-page-next]'); if (nx && nx.href) location.href = nx.href; }
            else if (e.key === 'ArrowLeft' || e.key === 'p') { var pv = $('[data-page-prev]'); if (pv && pv.href) location.href = pv.href; }
        });

        // --- Exportar CSV de las filas visibles ---
        var exportBtn = $('#export-csv');
        if (exportBtn) exportBtn.addEventListener('click', function () {
            var cols = ['ts', 'type', 'level', 'method', 'url', 'status', 'ms', 'id'];
            var lines = [['Fecha', 'Tipo', 'Nivel', 'Metodo', 'URL', 'Estado', 'Duracion (ms)', 'Id'].join(',')];
            visibleRows().forEach(function (tr) {
                lines.push(cols.map(function (c) { var v = tr.dataset[c] || ''; return /[,""\n]/.test(v) ? '""' + v.replace(/""/g, '""""') + '""' : v; }).join(','));
            });
            var d = new Date(), p = function (n) { return (n < 10 ? '0' : '') + n; };
            H.download('hubble-logs-' + d.getFullYear() + p(d.getMonth() + 1) + p(d.getDate()) + '-' + p(d.getHours()) + p(d.getMinutes()) + '.csv', '\ufeff' + lines.join('\r\n'), 'text/csv;charset=utf-8');
            H.toast(visibleRows().length + ' registros exportados');
        });

        // --- Búsquedas guardadas ---
        var savedList = $('#saved-list');
        function readSaved() { var s = store.get('saved', []); return Array.isArray(s) ? s : []; }
        function renderSaved() {
            if (!savedList) return;
            var items = readSaved();
            savedList.innerHTML = '';
            if (!items.length) { savedList.innerHTML = '<span class=muted>Guarda combinaciones de filtros para volver a ellas con un clic.</span>'; return; }
            items.forEach(function (s, i) {
                var a = document.createElement('a');
                a.className = 'chip saved'; a.href = location.pathname + (s.qs || '');
                if ((s.qs || '') === location.search && (s.refine || '') === (refine ? refine.value.trim() : '')) a.classList.add('active');
                a.title = (s.qs || 'Sin filtros') + (s.refine ? '  ·  ' + s.refine : '');
                a.innerHTML = '<span class=dot></span><span></span><button type=button class=chip-x title=Eliminar>' + ICON_ERR + '</button>';
                a.children[1].textContent = s.name;
                a.addEventListener('click', function (e) {
                    if (e.target.closest('.chip-x')) {
                        e.preventDefault(); var list = readSaved(); list.splice(i, 1); store.set('saved', list); renderSaved(); H.toast('Búsqueda eliminada'); return;
                    }
                    session.set('refine', s.refine || '');
                });
                savedList.appendChild(a);
            });
        }
        function defaultName() {
            var p = new URLSearchParams(location.search), parts = [];
            if (p.get('method')) parts.push(p.get('method'));
            if (p.get('statusGroup')) parts.push(p.get('statusGroup').charAt(0) + 'xx');
            if (p.get('logType')) parts.push(p.get('logType') === 'HTTP' ? 'HTTP' : 'ILogger');
            if (p.get('url')) parts.push(p.get('url'));
            if (refine && refine.value.trim()) parts.push(refine.value.trim());
            return parts.join(' · ') || 'Todos los registros';
        }
        var saveBtn = $('#save-search'), saveForm = $('#save-form'), saveName = $('#save-name');
        if (saveBtn) {
            saveBtn.addEventListener('click', function () { saveForm.hidden = false; saveBtn.hidden = true; saveName.value = defaultName(); saveName.focus(); saveName.select(); });
            var closeSave = function () { saveForm.hidden = true; saveBtn.hidden = false; };
            $('#save-cancel').addEventListener('click', closeSave);
            var doSave = function () {
                var name = saveName.value.trim(); if (!name) { saveName.focus(); return; }
                var list = readSaved().filter(function (s) { return s.name !== name; });
                list.push({ name: name.slice(0, 80), qs: location.search, refine: refine ? refine.value.trim() : '' });
                store.set('saved', list.slice(-20)); closeSave(); renderSaved(); H.toast('Búsqueda guardada');
            };
            $('#save-confirm').addEventListener('click', doSave);
            saveName.addEventListener('keydown', function (e) { if (e.key === 'Enter') { e.preventDefault(); doSave(); } else if (e.key === 'Escape') closeSave(); });
        }
        renderSaved();

        // --- Actualización en vivo ---
        var liveBtn = $('#live-toggle'), liveTimer = null, interval = +page.getAttribute('data-live-interval') || 5000;
        function refresh() {
            if (document.hidden) return;
            fetch(location.href, { credentials: 'same-origin', headers: { 'X-Requested-With': 'fetch' } })
                .then(function (r) { if (r.redirected || !r.ok) throw new Error(); return r.text(); })
                .then(function (html) {
                    var doc = new DOMParser().parseFromString(html, 'text/html');
                    if (!doc.querySelector('#logs-tbody')) return;
                    ['#logs-tbody', '#stats', '#pager', '#results-count'].forEach(function (sel) {
                        var n = doc.querySelector(sel), o = $(sel); if (n && o) o.innerHTML = n.innerHTML;
                    });
                    var now = Date.now();
                    $$('[data-age]').forEach(function (el) { if (!el.getAttribute('data-age-at')) el.setAttribute('data-age-at', now); });
                    H.refreshAges(); applySort(); applyRefine();
                    if (lastSelected) { var tr = tbody.querySelector('tr[data-id=""' + CSS.escape(lastSelected) + '""]'); if (tr) tr.classList.add('is-selected'); }
                    var stamp = $('#live-stamp'); if (stamp) stamp.textContent = new Date().toLocaleTimeString();
                })
                .catch(function () { });
        }
        function setLive(on) {
            clearInterval(liveTimer); liveTimer = null;
            if (liveBtn) { liveBtn.classList.toggle('on', on); liveBtn.setAttribute('aria-pressed', on ? 'true' : 'false'); }
            if (on) liveTimer = setInterval(refresh, interval);
        }
        if (liveBtn) {
            var liveOn = store.get('live', page.getAttribute('data-live-default') === '1');
            setLive(liveOn);
            liveBtn.addEventListener('click', function () { liveOn = !liveOn; store.set('live', liveOn); setLive(liveOn); H.toast(liveOn ? 'Actualización en vivo activada' : 'Actualización en vivo pausada'); if (liveOn) refresh(); });
        }

        applySort();
        applyRefine();
        H.refreshAges();
    }

    // ------------------------------------------------------------------
    // Detalle de un log
    // ------------------------------------------------------------------
    function initDetail() {
        var page = $('#detail-page'); if (!page) return;
        var last = session.get('lastList');
        if (last) $$('[data-back]').forEach(function (a) { a.href = last; });

        var tabs = $$('.tab[data-tab]');
        function activate(name, updateHash) {
            var found = tabs.some(function (t) { return t.getAttribute('data-tab') === name; });
            if (!found) return false;
            tabs.forEach(function (t) { var on = t.getAttribute('data-tab') === name; t.classList.toggle('active', on); t.setAttribute('aria-selected', on ? 'true' : 'false'); });
            $$('.tab-panel').forEach(function (p) { p.classList.toggle('active', p.id === 'panel-' + name); });
            if (updateHash) history.replaceState(null, '', '#' + name);
            return true;
        }
        tabs.forEach(function (t) { t.addEventListener('click', function () { activate(t.getAttribute('data-tab'), true); }); });
        document.addEventListener('click', function (e) {
            var g = e.target.closest('[data-goto]'); if (!g) return;
            e.preventDefault();
            activate(g.getAttribute('data-goto'), true);
            var lv = g.getAttribute('data-level'); if (lv) setLevel(lv);
            var anchor = $('#tabs-anchor');
            if (anchor) {
                var y = anchor.getBoundingClientRect().top + window.scrollY - ($('.topbar') || { offsetHeight: 0 }).offsetHeight;
                if (window.scrollY > y || anchor.getBoundingClientRect().top > window.innerHeight * 0.6) window.scrollTo({ top: y, behavior: 'smooth' });
            }
        });
        if (!(location.hash && activate(location.hash.slice(1), false)) && tabs.length) activate(tabs[0].getAttribute('data-tab'), false);

        document.addEventListener('keydown', function (e) {
            if (e.ctrlKey || e.metaKey || e.altKey || isTyping(e)) return;
            if (/^[1-9]$/.test(e.key) && tabs[+e.key - 1]) { e.preventDefault(); tabs[+e.key - 1].click(); }
            else if (e.key === 'b' || e.key === 'Backspace') { var b = $('[data-back]'); if (b) { e.preventDefault(); location.href = b.href; } }
        });

        // Frames del framework en el stack trace
        $$('[data-toggle-fw]').forEach(function (b) {
            var st = document.getElementById(b.getAttribute('data-toggle-fw'));
            b.addEventListener('click', function () { var on = st.classList.toggle('hide-fw'); b.classList.toggle('on', on); });
        });

        // Filtro por nivel en los logs relacionados
        var logList = $('#log-list'), activeLevel = '';
        function applyLogFilter() {
            if (!logList) return;
            var q = ($('#log-search') || { value: '' }).value.trim().toLowerCase(), shown = 0;
            $$('.log-row', logList).forEach(function (r) {
                var ok = (!activeLevel || r.getAttribute('data-level') === activeLevel) && (!q || r.textContent.toLowerCase().indexOf(q) !== -1);
                r.hidden = !ok; if (ok) shown++;
            });
            var info = $('#log-count'); if (info) info.textContent = shown + ' visibles';
            $$('[data-level-chip]').forEach(function (c) { c.classList.toggle('active', c.getAttribute('data-level-chip') === activeLevel); });
        }
        function setLevel(lv) { activeLevel = lv === 'all' ? '' : lv; applyLogFilter(); }
        $$('[data-level-chip]').forEach(function (c) { c.addEventListener('click', function () { setLevel(c.getAttribute('data-level-chip') || 'all'); }); });
        var ls = $('#log-search'); if (ls) ls.addEventListener('input', applyLogFilter);

        // Ordenar consultas por duración
        var qSort = $('#query-sort'), qList = $('#query-list');
        if (qSort && qList) {
            var original = $$('.q-item', qList);
            qSort.addEventListener('click', function () {
                var on = qSort.classList.toggle('on');
                var items = original.slice();
                if (on) items.sort(function (a, b) { return +b.getAttribute('data-ms') - +a.getAttribute('data-ms'); });
                items.forEach(function (i) { qList.appendChild(i); });
            });
        }

        // Descarga del log completo en JSON (API de Hubble)
        var dl = $('#download-json');
        if (dl) dl.addEventListener('click', function () {
            fetch(dl.getAttribute('data-api'), { credentials: 'same-origin' })
                .then(function (r) { if (!r.ok) throw new Error(); return r.json(); })
                .then(function (j) { H.download('hubble-log-' + page.getAttribute('data-id') + '.json', JSON.stringify(j, null, 2), 'application/json'); })
                .catch(function () { H.toast('No se pudo descargar el log', 'err'); });
        });

        H.refreshAges();
    }

    initCodeViewers(document);
    initRowFilters(document);
    initList();
    initDetail();
})();
";
}
