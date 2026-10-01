# Changelog - Versión 0.2.26

## Nueva Funcionalidad: Multi-targeting y Soporte desde .NET 6 en adelante

### Descripción
Se ha implementado el soporte multi-target en la biblioteca **Hubble** para admitir de manera nativa y robusta proyectos basados en **.NET 6.0, .NET 7.0 y .NET 8.0 (o superiores)**. 

*(Nota: La versión 0.2.25 fue omitida en el canal de distribución debido a pérdida de código de desarrollo intermedio).*

### Mejoras Realizadas
1. **Multi-targeting Nativo**:
   - Se configuraron múltiples frameworks de destino en el archivo del proyecto: `net6.0;net7.0;net8.0`.
   - Las aplicaciones en frameworks superiores (.NET 9 o .NET 10) consumirán automáticamente y sin problemas la compilación orientada a .NET 8.0.

2. **Modernización de Dependencias de ASP.NET Core**:
   - Se eliminaron las dependencias obsoletas e individuales de la era de .NET Core 2.2 (`Microsoft.AspNetCore.Http`, `Microsoft.AspNetCore.Http.Abstractions` y `Microsoft.AspNetCore.Routing`).
   - En su lugar, se adoptó el estándar moderno `<FrameworkReference Include="Microsoft.AspNetCore.App" />`. Esto resuelve advertencias de nulabilidad, problemas de resolución de tipos de ASP.NET Core en tiempo de ejecución y conflictos de dependencias transitivas.

3. **Referencias de Paquetes Condicionales**:
   - Se implementaron referencias condicionales por framework para **Entity Framework Core**, utilizando las últimas versiones de parche oficiales de cada rama de framework, asegurando máxima estabilidad y rendimiento:
     - **.NET 6.0**: EF Core `6.0.36`, DependencyInjection.Abstractions y Hosting.Abstractions `6.0.0`.
     - **.NET 7.0**: EF Core `7.0.20`, DependencyInjection.Abstractions y Hosting.Abstractions `7.0.0`.
     - **.NET 8.0**: EF Core `8.0.11`, DependencyInjection.Abstractions `8.0.2` y Hosting.Abstractions `8.0.0`.

### Archivos Modificados

1. **`src/src.csproj`**
   - Se actualizó la versión de paquete de `0.2.25.1` / `0.2.24` a `0.2.26`.
   - Configuración de `<TargetFrameworks>net6.0;net7.0;net8.0</TargetFrameworks>`.
   - Reemplazo de dependencias por `<FrameworkReference>` e `ItemGroup` condicionales.

2. **`README.md`**
   - Se actualizó la documentación indicando que el soporte mínimo es .NET 6 en adelante.

### Breaking Changes
Ninguno. Esta actualización mantiene la compatibilidad de firmas de API existentes, permitiendo una migración transparente.

### Testing
- ✅ Compilación exitosa sin errores para todos los frameworks (`net6.0`, `net7.0`, `net8.0`).
- ✅ Generación del paquete NuGet conteniendo todos los binarios específicos.

---

**Fecha**: 1 de Junio de 2026  
**Versión**: 0.2.26  
**Autor**: Gabriel Caraballo
