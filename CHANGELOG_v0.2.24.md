# Changelog - Versión 0.2.24.0

## Nueva Funcionalidad: HubbleMaskingHelper

### Descripción
Se ha agregado una nueva utilidad llamada `HubbleMaskingHelper` que permite enmascarar datos sensibles en logs personalizados, utilizando la misma configuración de enmascaramiento que se aplica automáticamente a las solicitudes y respuestas HTTP.

### Problema Resuelto
Anteriormente, cuando los desarrolladores usaban `JsonSerializer.Serialize()` directamente en sus logs, los datos sensibles (contraseñas, tokens, números de tarjetas de crédito, etc.) se exponían en texto plano, incluso si tenían configurado el enmascaramiento en Hubble para las solicitudes HTTP.

### Archivos Agregados

1. **`src/Utilities/HubbleMaskingHelper.cs`**
   - Clase estática con métodos para serializar y enmascarar datos sensibles
   - `SerializeMasked<T>()`: Serializa un objeto a JSON y enmascara propiedades sensibles
   - `MaskJson()`: Enmascara propiedades en un JSON ya serializado
   - Utiliza la configuración de `Security.MaskBodyProperties`, `MaskRequestBodyProperties` y `MaskResponseBodyProperties`

2. **`examples/MaskingExample.cs`**
   - Ejemplos completos de uso del HubbleMaskingHelper
   - Casos de uso con servicios, controladores, y objetos anidados
   - Ejemplos de configuración

3. **`docs/MASKING_GUIDE.md`**
   - Guía completa de uso de la funcionalidad de enmascaramiento
   - Mejores prácticas
   - FAQ y troubleshooting

### Archivos Modificados

1. **`src/Extensions/ServiceCollectionExtensions.cs`**
   - Se agregó la inicialización automática de `HubbleMaskingHelper` en todos los métodos `AddHubble()`
   - Se agregó el using para `Gabonet.Hubble.Utilities`

2. **`src/Examples/HubbleExamples.cs`**
   - Se agregó una nueva clase de ejemplos `MaskingExamples` con casos de uso completos
   - Ejemplos de integración con la configuración de Hubble

3. **`README.md`**
   - Se agregó sección sobre enmascaramiento de datos en logs personalizados (en español e inglés)
   - Ejemplos de uso de `HubbleMaskingHelper`
   - Documentación de los métodos disponibles

4. **`src/src.csproj`**
   - Versión actualizada de `0.2.23.2` a `0.2.24.0`
   - Descripción actualizada para incluir "data masking capabilities"

### Características de HubbleMaskingHelper

- ✅ **Automático**: Utiliza la configuración existente de Hubble
- ✅ **Case-Insensitive**: No distingue entre mayúsculas y minúsculas
- ✅ **Recursivo**: Enmascara propiedades en objetos anidados y arrays
- ✅ **Flexible**: Permite agregar propiedades adicionales a enmascarar en runtime
- ✅ **Seguro**: Maneja errores de forma elegante

### Uso Básico

```csharp
using Gabonet.Hubble.Utilities;

public class PlayerService
{
    private readonly ILogger<PlayerService> _logger;

    public async Task<PlayerDto> CreatePlayerAsync(PlayerDto playerDto)
    {
        // Antes (sin enmascaramiento)
        // _logger.LogInformation("Creating player: {Player}", JsonSerializer.Serialize(playerDto));
        
        // Ahora (con enmascaramiento)
        _logger.LogInformation("Creating player: {Player}", 
            HubbleMaskingHelper.SerializeMasked(playerDto));
        
        // ...
    }
}
```

### Configuración

No se requiere configuración adicional. Una vez que Hubble está configurado con las propiedades a enmascarar, `HubbleMaskingHelper` las utiliza automáticamente:

```csharp
builder.Services.AddHubble(options =>
{
    options.ConnectionString = "mongodb://localhost:27017";
    options.DatabaseName = "HubbleDB";
    
    options.Security.MaskBodyProperties = new List<string> 
    { 
        "password", "token", "creditCard", "cvv", "pin"
    };
});

// HubbleMaskingHelper ya está listo para usar
```

### Breaking Changes
Ninguno. Esta es una funcionalidad completamente nueva y retrocompatible.

### Migración
No se requiere migración. Los usuarios existentes pueden continuar usando Hubble como siempre, y opcionalmente adoptar `HubbleMaskingHelper` donde lo necesiten.

### Testing
- ✅ Compilación exitosa sin errores
- ✅ Paquete NuGet generado: `Gabonet.Hubble.0.2.24.nupkg`
- ✅ Todas las funcionalidades existentes permanecen intactas

### Documentación
- README.md actualizado con ejemplos en español e inglés
- Guía completa de enmascaramiento en `docs/MASKING_GUIDE.md`
- Ejemplos de código en `examples/MaskingExample.cs`
- Ejemplos integrados en `src/Examples/HubbleExamples.cs`

### Próximos Pasos Sugeridos
1. Publicar el paquete actualizado en NuGet
2. Notificar a los usuarios existentes sobre la nueva funcionalidad
3. Considerar agregar tests unitarios para `HubbleMaskingHelper`
4. Posible adición de más opciones de enmascaramiento (por ejemplo, enmascaramiento parcial)

---

**Fecha**: 22 de Diciembre de 2025  
**Versión**: 0.2.24.0  
**Autor**: Gabriel Caraballo

