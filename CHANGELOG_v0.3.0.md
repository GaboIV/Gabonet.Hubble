# Changelog - Versión 0.3.0

Versión centrada en **seguridad del dashboard**, **resiliencia** (Hubble nunca bloquea a la aplicación anfitriona), **retención automática** y una **configuración unificada**. Incluye cambios incompatibles: revisa la sección [Migración desde 0.2.x](#migración-desde-02x) antes de actualizar.

Frameworks soportados: **.NET 6.0, .NET 7.0 y .NET 8.0** (las aplicaciones en .NET 9+ usan la compilación de .NET 8.0).

---

## Seguridad

- **Filtro de IPs solo para el dashboard.** `Security.AllowedIps` se evaluaba en cada solicitud y podía bloquear con `403` a **toda la aplicación** (y en la configuración desde `appsettings.json` el valor por defecto era `127.0.0.1`). Ahora solo protege el dashboard y su API, soporta CIDR IPv4/IPv6 y su valor por defecto es una lista vacía (sin restricción).
- **XSS corregido.** Todos los datos capturados (URL, cuerpos, headers, errores, SQL, logs) se codifican antes de mostrarse en el dashboard. Antes, un cuerpo con `<script>` enviado a cualquier endpoint se ejecutaba al abrir el panel.
- **Cookie de sesión firmada.** La cookie `HubbleAuth` era `base64("usuario:fecha")` y se podía falsificar. Ahora está cifrada y firmada con ASP.NET Core Data Protection, es `HttpOnly` y `SameSite=Strict`, expira a las 8 horas y se invalida al cambiar usuario o contraseña.
- **Protección CSRF.** Las acciones que modifican datos (`/delete-all`, `/run-prune`, `/recalculate-stats`, `/save-*`, login) solo aceptan `POST` con token antiforgery. Los `POST` de la API exigen `Content-Type: application/json`.
- **Fuerza bruta.** 5 intentos fallidos desde una IP en 15 minutos la bloquean 15 minutos (`429`). Las credenciales se comparan en tiempo constante.
- **Cabeceras de seguridad** en el dashboard: `Content-Security-Policy`, `X-Frame-Options: DENY`, `X-Content-Type-Options: nosniff`, `Referrer-Policy: no-referrer`, `Cache-Control: no-store`.
- La API responde `401` con `WWW-Authenticate: Basic` en lugar de devolver el HTML del login; `pageSize` está limitado a 200; la búsqueda se escapa antes de usarse como expresión regular en MongoDB.
- La aplicación no arranca si `RequireAuthentication` está activo con usuario o contraseña vacíos.

## Rendimiento y resiliencia

- **Captura en segundo plano.** Antes, cada solicitud esperaba a MongoDB antes y después de ejecutar el código de la aplicación: con MongoDB caído, cada request tardaba **60 segundos** y el arranque **30 segundos**. Ahora los logs se encolan en memoria (máximo 5.000) y un servicio en segundo plano los guarda por lotes de hasta 200. Con MongoDB caído, el arranque y las solicitudes no se ven afectados; los logs nuevos se descartan mientras no está disponible y se avisa por consola.
- El proveedor de `ILogger` ya no crea un `Task.Run` ni un scope sin liberar por cada línea de log.
- `HubbleStatsService` ya no consulta MongoDB en su constructor.
- El cliente de MongoDB de Hubble usa un timeout de selección de servidor de 5 segundos (salvo que la cadena de conexión indique `serverSelectionTimeoutMS`).
- **Hubble ya no registra `IMongoClient` en el contenedor de dependencias**, por lo que no puede reemplazar el cliente de MongoDB de la aplicación.

## Retención e índices

- La retención (`EnableDataPrune` + `MaxLogAgeHours`) se aplica con un **índice TTL de MongoDB** que Hubble crea y mantiene. Se eliminan `HubbleDataPruneManager` y `DataPruneService`; `DataPruneIntervalHours` queda sin uso.
- Hubble crea al arrancar los índices `hubble_timestamp` (orden del listado / TTL) y `hubble_related_request` (logs de ILogger por solicitud). Los índices creados a mano se respetan.
- La página `/config` muestra la configuración efectiva y el estado de los índices.

## Configuración unificada

- `HubbleOptions` es la **única clase de configuración** e incluye `ConnectionString`, `DatabaseName` y `MinimumLogLevel`.
- Nueva sobrecarga `AddHubble(configuration.GetSection("Hubble"), options => { ... })` basada en el binder estándar de .NET. Las listas configuradas reemplazan a los valores por defecto.
- Validación al arrancar con todos los errores en un solo mensaje.
- `CaptureLoggerMessages` (ahora `true` por defecto) y `MinimumLogLevel` tienen efecto; `IgnoreStaticFiles` se puede configurar desde código.

## Correcciones

- Los logs HTTP guardan la IP del cliente (antes quedaba vacía).
- `CaptureHttpRequests = false` desactiva realmente la captura.
- Una excepción no controlada se registra con estado `500` (antes podía quedar como `200`).
- Se restaura el stream de respuesta original, para que el `UseExceptionHandler` de la aplicación pueda escribir su respuesta tras una excepción.
- La ruta base se compara por segmentos (`/hubble` ya no captura `/hubblefoo`).
- `MaskRequestBodyProperties` y `MaskResponseBodyProperties` se leen desde `appsettings.json`.

## Calidad

- Nuevo proyecto de tests `tests/Gabonet.Hubble.Tests` (xUnit) con 55 tests, ejecutados en .NET 6 y .NET 8. Los 4 tests de integración con MongoDB se activan con la variable `HUBBLE_TEST_MONGODB`.
- README reescrito en inglés con guía de integración, checklist de producción y troubleshooting.

---

## Migración desde 0.2.x

| Antes | Ahora |
|---|---|
| `services.AddHubble(options => { ... })` | Sin cambios. |
| `services.AddHubble(configuration, connectionString, databaseName)` | Funciona, pero está `[Obsolete]`. Usa `services.AddHubble(configuration.GetSection("Hubble"), o => { o.ConnectionString = ...; o.DatabaseName = ...; })`. |
| `services.AddHubble(connectionString, databaseName, ...)` | Funciona, pero está `[Obsolete]`. Usa `AddHubble(options => ...)`. |
| Tipos `HubbleConfiguration`, `HubbleAuthConfiguration`, `Gabonet.Hubble.Models.SecurityConfiguration` | Eliminados. Usa `HubbleOptions` y `Gabonet.Hubble.Middleware.SecurityConfiguration`. |
| `GET /hubble/api/prune`, `GET /hubble/api/recalculate-stats` | `POST` con `Content-Type: application/json`. |
| `POST` a la API sin `Content-Type: application/json` | Responde `415`. |
| `Security.AllowedIps` bloqueaba toda la app | Solo afecta al dashboard. Desde `appsettings.json` ya no vale `127.0.0.1` por defecto. |
| `DataPruneIntervalHours` | Sin uso (retención por índice TTL). |
| `IMongoClient` registrado por Hubble | Ya no se registra. Si la aplicación lo resolvía por este motivo, debe registrar el suyo. |
| `TimeZoneId` inválido caía en UTC | La aplicación no arranca. |
| Sesiones del dashboard de versiones anteriores | Inválidas: hay que volver a iniciar sesión. |
| Varias instancias de la aplicación | Persistir y compartir las claves de Data Protection para que la sesión sea válida en todas. |

También cambian los constructores públicos de `HubbleMiddleware`, `HubbleLoggerProvider` y `HubbleController` (normalmente los crea el propio Hubble).
