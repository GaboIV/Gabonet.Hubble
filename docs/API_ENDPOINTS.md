# Hubble API Endpoints

Este documento describe los endpoints de API JSON disponibles en Hubble. Todos los endpoints funcionan en paralelo con la interfaz web, permitiendo el acceso tanto visual como programático.

## Base Path

Todos los endpoints de API usan el prefijo `/api` después del base path configurado. Por ejemplo, si el base path es `/hubble`, los endpoints serán:

```
https://tu-aplicacion/hubble/api/[endpoint]
```

## Autenticación

Si la autenticación está habilitada en Hubble, los endpoints de API requerirán la misma autenticación que la interfaz web.

## Endpoints Disponibles

### 1. Obtener Logs

**GET** `/api/logs`

Obtiene una lista paginada de logs con filtros opcionales.

#### Parámetros de consulta:
- `method` (string, opcional): Filtrar por método HTTP (GET, POST, etc.)
- `url` (string, opcional): Filtrar por URL
- `statusGroup` (string, opcional): Filtrar por grupo de códigos de estado (200, 400, 500)
- `logType` (string, opcional): Filtrar por tipo de log (ApplicationLogger, HTTP)
- `page` (int, opcional): Número de página (default: 1)
- `pageSize` (int, opcional): Tamaño de página (default: 50)

#### Ejemplo de respuesta:
```json
{
  "logs": [
    {
      "id": "507f1f77bcf86cd799439011",
      "timestamp": "2024-01-15T10:30:00Z",
      "method": "GET",
      "url": "/api/users",
      "statusCode": 200,
      "duration": 150,
      "controllerName": "UsersController",
      "actionName": "GetUsers",
      "requestId": "req-123"
    }
  ],
  "page": 1,
  "pageSize": 50,
  "totalCount": 150,
  "totalPages": 3,
  "hasNextPage": true,
  "hasPreviousPage": false
}
```

#### Ejemplo de uso con curl:
```bash
curl -X GET "https://tu-aplicacion/hubble/api/logs?method=GET&page=1&pageSize=20"
```

### 2. Obtener Detalles de un Log

**GET** `/api/logs/{id}`

Obtiene los detalles completos de un log específico incluyendo logs relacionados.

#### Parámetros:
- `id` (string): ID del log a obtener

#### Ejemplo de respuesta:
```json
{
  "found": true,
  "log": {
    "id": "507f1f77bcf86cd799439011",
    "timestamp": "2024-01-15T10:30:00Z",
    "method": "GET",
    "url": "/api/users",
    "statusCode": 200,
    "duration": 150,
    "controllerName": "UsersController",
    "actionName": "GetUsers",
    "requestId": "req-123",
    "requestBody": "",
    "responseBody": "[{\"id\":1,\"name\":\"Juan\"}]",
    "headers": {
      "User-Agent": "Mozilla/5.0...",
      "Accept": "application/json"
    }
  },
  "relatedLogs": [
    {
      "id": "507f1f77bcf86cd799439012",
      "timestamp": "2024-01-15T10:30:01Z",
      "controllerName": "ApplicationLogger",
      "message": "User query executed successfully",
      "logLevel": "Information",
      "requestId": "req-123"
    }
  ]
}
```

#### Ejemplo de uso con curl:
```bash
curl -X GET "https://tu-aplicacion/hubble/api/logs/507f1f77bcf86cd799439011"
```

### 3. Eliminar Todos los Logs

**DELETE** `/api/logs`

Elimina todos los logs almacenados en el sistema.

#### Ejemplo de respuesta:
```json
{
  "success": true,
  "message": "All logs have been deleted successfully",
  "deletedCount": 1523
}
```

#### Ejemplo de uso con curl:
```bash
curl -X DELETE "https://tu-aplicacion/hubble/api/logs"
```

### 4. Obtener Configuración y Estadísticas

**GET** `/api/config`

Obtiene la configuración actual del sistema y las estadísticas.

#### Ejemplo de respuesta:
```json
{
  "statistics": {
    "totalRequests": 15230,
    "totalErrors": 45,
    "averageResponseTime": 234.5,
    "lastPruneDate": "2024-01-15T09:00:00Z",
    "lastPruneDeletedCount": 1000
  },
  "configuration": {
    "enableDataPrune": true,
    "dataPruneIntervalHours": 24,
    "maxLogAgeHours": 168,
    "captureHttpRequests": true,
    "captureLoggerMessages": true,
    "ignorePaths": ["/health", "/metrics"]
  },
  "options": {
    "basePath": "/hubble",
    "serviceName": "MiServicio",
    "captureHttpRequests": true,
    "captureLoggerMessages": true,
    "ignorePaths": ["/health", "/metrics"],
    "version": "v0.2.8"
  }
}
```

#### Ejemplo de uso con curl:
```bash
curl -X GET "https://tu-aplicacion/hubble/api/config"
```

### 5. Ejecutar Limpieza Manual

**GET** `/api/prune`

Ejecuta una limpieza manual de logs basada en la configuración actual.

#### Ejemplo de respuesta:
```json
{
  "success": true,
  "message": "Manual prune completed successfully. 150 logs were deleted.",
  "prunedCount": 150,
  "cutoffDate": "2024-01-08T10:30:00Z"
}
```

#### Ejemplo de uso con curl:
```bash
curl -X GET "https://tu-aplicacion/hubble/api/prune"
```

### 6. Recalcular Estadísticas

**GET** `/api/recalculate-stats`

Recalcula las estadísticas del sistema.

#### Ejemplo de respuesta:
```json
{
  "success": true,
  "message": "Statistics recalculated successfully"
}
```

#### Ejemplo de uso con curl:
```bash
curl -X GET "https://tu-aplicacion/hubble/api/recalculate-stats"
```

### 7. Guardar Configuración de Limpieza

**POST** `/api/config/prune`

Guarda la configuración de limpieza automática de datos.

#### Cuerpo de la solicitud:
```json
{
  "enableDataPrune": true,
  "dataPruneIntervalHours": 24,
  "maxLogAgeHours": 168
}
```

#### Ejemplo de respuesta:
```json
{
  "success": true,
  "message": "Prune configuration saved successfully"
}
```

#### Ejemplo de uso con curl:
```bash
curl -X POST "https://tu-aplicacion/hubble/api/config/prune" \
  -H "Content-Type: application/json" \
  -d '{
    "enableDataPrune": true,
    "dataPruneIntervalHours": 24,
    "maxLogAgeHours": 168
  }'
```

### 8. Guardar Configuración de Captura

**POST** `/api/config/capture`

Guarda la configuración de captura de datos.

#### Cuerpo de la solicitud:
```json
{
  "captureHttpRequests": true,
  "captureLoggerMessages": true
}
```

#### Ejemplo de respuesta:
```json
{
  "success": true,
  "message": "Capture configuration saved successfully"
}
```

#### Ejemplo de uso con curl:
```bash
curl -X POST "https://tu-aplicacion/hubble/api/config/capture" \
  -H "Content-Type: application/json" \
  -d '{
    "captureHttpRequests": true,
    "captureLoggerMessages": true
  }'
```

### 9. Guardar Rutas Ignoradas

**POST** `/api/config/ignore-paths`

Guarda la lista de rutas que deben ser ignoradas por Hubble.

#### Cuerpo de la solicitud:
```json
{
  "ignorePaths": ["/health", "/metrics", "/swagger"]
}
```

#### Ejemplo de respuesta:
```json
{
  "success": true,
  "message": "Ignore paths configuration saved successfully"
}
```

#### Ejemplo de uso con curl:
```bash
curl -X POST "https://tu-aplicacion/hubble/api/config/ignore-paths" \
  -H "Content-Type: application/json" \
  -d '{
    "ignorePaths": ["/health", "/metrics", "/swagger"]
  }'
```

## Códigos de Estado HTTP

- **200 OK**: Operación exitosa
- **400 Bad Request**: Solicitud inválida (cuerpo JSON malformado)
- **404 Not Found**: Endpoint o recurso no encontrado
- **405 Method Not Allowed**: Método HTTP no soportado
- **500 Internal Server Error**: Error interno del servidor

## Uso con Postman

Para usar estos endpoints con Postman:

1. **Importar Collection**: Puedes crear una nueva colección en Postman con todos estos endpoints
2. **Base URL**: Configura la variable `{{baseUrl}}` con tu URL base (ej: `https://tu-aplicacion/hubble`)
3. **Headers**: Para endpoints POST, asegúrate de incluir `Content-Type: application/json`
4. **Autenticación**: Si está habilitada, primero autentícate a través de la interfaz web o configura las cookies necesarias

## Ejemplos de Colección Postman

```json
{
  "info": {
    "name": "Hubble API",
    "description": "API endpoints for Hubble .NET monitoring tool"
  },
  "variable": [
    {
      "key": "baseUrl",
      "value": "https://tu-aplicacion/hubble"
    }
  ],
  "item": [
    {
      "name": "Get Logs",
      "request": {
        "method": "GET",
        "url": "{{baseUrl}}/api/logs?page=1&pageSize=20"
      }
    },
    {
      "name": "Get Log Details",
      "request": {
        "method": "GET",
        "url": "{{baseUrl}}/api/logs/{{logId}}"
      }
    },
    {
      "name": "Delete All Logs",
      "request": {
        "method": "DELETE",
        "url": "{{baseUrl}}/api/logs"
      }
    },
    {
      "name": "Get Configuration",
      "request": {
        "method": "GET",
        "url": "{{baseUrl}}/api/config"
      }
    }
  ]
}
```

## Filtrado Avanzado

### Filtros de Logs

El endpoint `/api/logs` soporta varios filtros que se pueden combinar:

```bash
# Obtener solo errores HTTP
curl "https://tu-aplicacion/hubble/api/logs?statusGroup=500"

# Obtener logs de aplicación (ILogger)
curl "https://tu-aplicacion/hubble/api/logs?logType=ApplicationLogger"

# Obtener solo requests POST a una URL específica
curl "https://tu-aplicacion/hubble/api/logs?method=POST&url=/api/users"

# Combinación de filtros con paginación
curl "https://tu-aplicacion/hubble/api/logs?method=GET&statusGroup=200&page=2&pageSize=25"
```

Esta documentación cubre todos los endpoints disponibles en la API JSON de Hubble, permitiendo un acceso programático completo a todas las funcionalidades disponibles en la interfaz web.

