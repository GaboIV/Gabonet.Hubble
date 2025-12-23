# Enmascaramiento de Datos en Logs con Hubble

## Descripción

Hubble proporciona una utilidad llamada `HubbleMaskingHelper` que permite enmascarar datos sensibles en tus logs personalizados, utilizando la misma configuración de enmascaramiento que se aplica automáticamente a las solicitudes y respuestas HTTP.

## Problema que resuelve

Cuando utilizas `JsonSerializer.Serialize()` o `JsonConvert.SerializeObject()` directamente en tus logs, los datos sensibles (contraseñas, tokens, números de tarjetas de crédito, etc.) se exponen en texto plano, incluso si has configurado el enmascaramiento en Hubble para las solicitudes HTTP.

### Antes (sin enmascaramiento)

```csharp
_logger.LogInformation("Creating player: {Player}", JsonSerializer.Serialize(playerDto));
// Output: Creating player: {"Id":"...","Name":"John","Password":"Secret123","Token":"abc-xyz-123",...}
```

### Después (con enmascaramiento)

```csharp
_logger.LogInformation("Creating player: {Player}", 
    HubbleMaskingHelper.SerializeMasked(playerDto));
// Output: Creating player: {"Id":"...","Name":"John","Password":"*****","Token":"*****",...}
```

## Configuración

### Paso 1: Configurar Hubble con las propiedades a enmascarar

En tu `Program.cs` o `Startup.cs`, configura las propiedades que deseas enmascarar:

```csharp
builder.Services.AddHubble(options =>
{
    options.ConnectionString = "mongodb://localhost:27017";
    options.DatabaseName = "HubbleDB";
    
    // Configurar propiedades a enmascarar
    options.Security.MaskBodyProperties = new List<string> 
    { 
        "password",    // Enmascara cualquier campo llamado "password" (case-insensitive)
        "token",       // Enmascara tokens
        "creditCard",  // Enmascara números de tarjeta
        "cvv",         // Enmascara códigos CVV
        "pin",         // Enmascara códigos PIN
        "tarjeta",     // Enmascara el campo "tarjeta"
        "cuentaOrigen" // Enmascara cuentas bancarias
    };

    // Propiedades adicionales para enmascarar SOLO en requests
    options.Security.MaskRequestBodyProperties = new List<string> 
    { 
        "secretKey",
        "clave"
    };

    // Propiedades adicionales para enmascarar SOLO en responses
    options.Security.MaskResponseBodyProperties = new List<string> 
    { 
        "internalId",
        "secretData" 
    };
});
```

### Paso 2: Usar HubbleMaskingHelper en tu código

```csharp
using Gabonet.Hubble.Utilities;

public class PlayerService
{
    private readonly ILogger<PlayerService> _logger;

    public PlayerService(ILogger<PlayerService> logger)
    {
        _logger = logger;
    }

    public async Task<PlayerDto> CreatePlayerAsync(PlayerDto playerDto)
    {
        // Usar HubbleMaskingHelper para serializar y enmascarar datos sensibles
        _logger.LogInformation("Creating player: {Player}", 
            HubbleMaskingHelper.SerializeMasked(playerDto));
        
        // Tu lógica de negocio aquí
        var result = await _repository.AddAsync(playerDto);
        
        _logger.LogInformation("Player created: {Player}", 
            HubbleMaskingHelper.SerializeMasked(result));
        
        return result;
    }
}
```

## API de HubbleMaskingHelper

### `SerializeMasked<T>(T obj, List<string>? additionalMaskProperties = null, bool indent = false)`

Serializa un objeto a JSON y enmascara las propiedades sensibles según la configuración de Hubble.

**Parámetros:**
- `obj`: Objeto a serializar
- `additionalMaskProperties`: Lista opcional de propiedades adicionales a enmascarar (más allá de las configuradas)
- `indent`: Si `true`, el JSON de salida será indentado para mejor legibilidad

**Ejemplos:**

```csharp
// Uso básico
var maskedJson = HubbleMaskingHelper.SerializeMasked(myObject);

// Con propiedades adicionales a enmascarar
var maskedJson = HubbleMaskingHelper.SerializeMasked(
    myObject, 
    new List<string> { "customField", "secretData" }
);

// Con JSON indentado para mejor legibilidad
var maskedJson = HubbleMaskingHelper.SerializeMasked(myObject, indent: true);
```

### `MaskJson(string jsonString, List<string>? additionalMaskProperties = null)`

Enmascara propiedades sensibles en un string JSON ya serializado.

**Parámetros:**
- `jsonString`: String JSON a enmascarar
- `additionalMaskProperties`: Lista opcional de propiedades adicionales a enmascarar

**Ejemplos:**

```csharp
// Enmascarar un JSON ya serializado
var jsonString = JsonSerializer.Serialize(myObject);
var maskedJson = HubbleMaskingHelper.MaskJson(jsonString);

// Con propiedades adicionales
var maskedJson = HubbleMaskingHelper.MaskJson(
    jsonString, 
    new List<string> { "customField" }
);
```

## Ejemplos de Uso

### Ejemplo 1: Servicio con logs enmascarados

```csharp
public class UserService
{
    private readonly ILogger<UserService> _logger;

    public async Task<UserDto> CreateUserAsync(UserDto userDto)
    {
        // Log de entrada con datos enmascarados
        _logger.LogInformation("Creating user: {User}", 
            HubbleMaskingHelper.SerializeMasked(userDto));

        var user = await _repository.AddAsync(userDto);

        // Log de salida con datos enmascarados
        _logger.LogInformation("User created successfully: {User}", 
            HubbleMaskingHelper.SerializeMasked(user));

        return user;
    }

    public async Task UpdatePasswordAsync(Guid userId, PasswordChangeDto dto)
    {
        // Enmascarar campos adicionales específicos para este caso
        _logger.LogInformation("Updating password for user {UserId}. Data: {Data}", 
            userId, 
            HubbleMaskingHelper.SerializeMasked(dto, new List<string> { "oldPassword", "newPassword" }));

        await _repository.UpdatePasswordAsync(userId, dto);
    }
}
```

### Ejemplo 2: Controller con logs enmascarados

```csharp
[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly ILogger<AuthController> _logger;
    private readonly IAuthService _authService;

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginDto loginDto)
    {
        try
        {
            // Log de request con datos sensibles enmascarados
            _logger.LogInformation("Login attempt: {LoginData}", 
                HubbleMaskingHelper.SerializeMasked(loginDto));

            var result = await _authService.LoginAsync(loginDto);

            // Log de resultado con token enmascarado
            _logger.LogInformation("Login successful: {Result}", 
                HubbleMaskingHelper.SerializeMasked(result));

            return Ok(result);
        }
        catch (Exception ex)
        {
            // Incluso en errores, enmascarar datos sensibles
            _logger.LogError(ex, "Login failed for: {LoginData}", 
                HubbleMaskingHelper.SerializeMasked(loginDto));
            return Unauthorized();
        }
    }
}
```

### Ejemplo 3: Objetos anidados y arrays

```csharp
public class OrderService
{
    public async Task ProcessOrderAsync(OrderDto order)
    {
        // HubbleMaskingHelper enmascara recursivamente objetos anidados y arrays
        _logger.LogInformation("Processing order: {Order}", 
            HubbleMaskingHelper.SerializeMasked(order));

        // Si el order contiene:
        // - order.Customer.CreditCard
        // - order.PaymentMethods[0].CVV
        // - order.PaymentMethods[1].Token
        // Todos serán enmascarados automáticamente
    }
}
```

### Ejemplo 4: Logs de depuración con formato indentado

```csharp
public class ProductService
{
    public async Task<ProductDto> CreateProductAsync(ProductDto productDto)
    {
        // En desarrollo, usar indent: true para mejor legibilidad
        _logger.LogDebug("Creating product with details:\n{Product}", 
            HubbleMaskingHelper.SerializeMasked(productDto, indent: true));

        var result = await _repository.AddAsync(productDto);
        return result;
    }
}
```

## Características

### ✅ Automático
- Utiliza la configuración de `MaskBodyProperties`, `MaskRequestBodyProperties` y `MaskResponseBodyProperties` de Hubble
- No requiere configuración adicional una vez que Hubble está configurado

### ✅ Case-Insensitive
- No distingue entre mayúsculas y minúsculas
- `"Password"`, `"password"`, `"PASSWORD"` se enmascaran por igual

### ✅ Recursivo
- Enmascara propiedades en objetos anidados
- Enmascara propiedades en arrays de objetos
- Maneja estructuras complejas automáticamente

### ✅ Flexible
- Permite agregar propiedades adicionales a enmascarar en tiempo de ejecución
- Funciona con cualquier tipo de objeto serializable

### ✅ Seguro
- Si Hubble no está inicializado, devuelve el JSON sin enmascarar (degradación elegante)
- Maneja errores de serialización devolviendo un mensaje de error claro

## Mejores Prácticas

### 1. Siempre enmascarar datos sensibles en logs

```csharp
// ❌ MAL: Datos sensibles expuestos
_logger.LogInformation("User: {User}", JsonSerializer.Serialize(user));

// ✅ BIEN: Datos sensibles protegidos
_logger.LogInformation("User: {User}", HubbleMaskingHelper.SerializeMasked(user));
```

### 2. Usar en todos los niveles de log

```csharp
// Information
_logger.LogInformation("Creating user: {User}", 
    HubbleMaskingHelper.SerializeMasked(userDto));

// Debug (con indentación para mejor legibilidad)
_logger.LogDebug("User details:\n{User}", 
    HubbleMaskingHelper.SerializeMasked(userDto, indent: true));

// Warning
_logger.LogWarning("Invalid user data: {User}", 
    HubbleMaskingHelper.SerializeMasked(userDto));

// Error
_logger.LogError(ex, "Error processing user: {User}", 
    HubbleMaskingHelper.SerializeMasked(userDto));
```

### 3. Configurar propiedades a enmascarar según tu dominio

```csharp
// Para aplicaciones financieras
options.Security.MaskBodyProperties = new List<string> 
{ 
    "password", "token", "creditCard", "cvv", "pin",
    "accountNumber", "iban", "swift", "routing",
    "cuentaBancaria", "cbu", "alias"
};

// Para aplicaciones de salud
options.Security.MaskBodyProperties = new List<string> 
{ 
    "password", "token", "ssn", "medicalRecord",
    "healthInsurance", "diagnosis", "prescription"
};
```

### 4. Enmascarar en todos los puntos de logging

```csharp
public class OrderService
{
    public async Task<OrderDto> ProcessOrderAsync(OrderDto order)
    {
        // Log de entrada
        _logger.LogInformation("Received order: {Order}", 
            HubbleMaskingHelper.SerializeMasked(order));

        try
        {
            // Validación
            ValidateOrder(order);
            _logger.LogInformation("Order validated: {OrderId}", order.Id);

            // Procesamiento
            var result = await _repository.SaveAsync(order);
            
            // Log de salida exitosa
            _logger.LogInformation("Order processed: {Order}", 
                HubbleMaskingHelper.SerializeMasked(result));

            return result;
        }
        catch (Exception ex)
        {
            // Log de error - también enmascarar
            _logger.LogError(ex, "Failed to process order: {Order}", 
                HubbleMaskingHelper.SerializeMasked(order));
            throw;
        }
    }
}
```

## Integración con Hubble Middleware

`HubbleMaskingHelper` utiliza la misma lógica de enmascaramiento que el middleware de Hubble:

- **Middleware HTTP**: Enmascara automáticamente los requests y responses HTTP
- **HubbleMaskingHelper**: Permite enmascarar manualmente en logs y otros lugares

Ambos comparten la misma configuración, asegurando consistencia en toda la aplicación.

## Preguntas Frecuentes

### ¿Qué pasa si Hubble no está inicializado?

Si `HubbleMaskingHelper` se usa antes de que Hubble esté configurado, devolverá el JSON sin enmascarar. Esto evita errores en tiempo de ejecución.

### ¿Afecta el rendimiento?

El impacto en el rendimiento es mínimo. `HubbleMaskingHelper` solo se ejecuta cuando se genera un log, y el proceso de enmascaramiento es muy eficiente.

### ¿Puedo usar diferentes propiedades a enmascarar en diferentes partes del código?

Sí, puedes usar el parámetro `additionalMaskProperties` para agregar propiedades específicas a enmascarar en cada llamada:

```csharp
// Enmascarar propiedades configuradas + "email" y "phone"
_logger.LogInformation("User: {User}", 
    HubbleMaskingHelper.SerializeMasked(user, new List<string> { "email", "phone" }));
```

### ¿Funciona con Entity Framework entities?

Sí, funciona con cualquier objeto serializable, incluyendo entidades de Entity Framework:

```csharp
var user = await _context.Users.FindAsync(id);
_logger.LogInformation("User found: {User}", 
    HubbleMaskingHelper.SerializeMasked(user));
```

## Ver también

- [README principal](../README.md)
- [Ejemplo completo de uso](./MaskingExample.cs)
- [Documentación de API Endpoints](../docs/API_ENDPOINTS.md)

