namespace Gabonet.Hubble.Tests.Support;

using Xunit;

/// <summary>
/// Test que necesita un MongoDB real. Se omite (skipped) si la variable de entorno HUBBLE_TEST_MONGODB no está definida.
/// Ejemplo: HUBBLE_TEST_MONGODB=mongodb://localhost:27017
/// </summary>
public sealed class MongoFactAttribute : FactAttribute
{
    public const string ConnectionStringVariable = "HUBBLE_TEST_MONGODB";

    public MongoFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(ConnectionString))
        {
            Skip = $"Define {ConnectionStringVariable} con la cadena de conexión de un MongoDB de pruebas para ejecutar este test.";
        }
    }

    public static string? ConnectionString => Environment.GetEnvironmentVariable(ConnectionStringVariable);
}
