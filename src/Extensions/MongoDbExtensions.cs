using MongoDB.Bson;
using MongoDB.Driver;
using System.Diagnostics;

namespace Gabonet.Hubble.Extensions;

/// <summary>
/// Extensiones para trabajar con MongoDB en el contexto de Hubble
/// </summary>
public static class MongoDbExtensions
{
    /// <summary>
    /// Obtiene el nombre de la colección a partir de un comando BsonDocument
    /// </summary>
    /// <param name="command">El comando BsonDocument</param>
    /// <returns>El nombre de la colección o "Unknown" si no se puede determinar</returns>
    public static string GetCollectionName(this BsonDocument command)
    {
        try
        {
            if (command.Contains("find"))
                return command["find"].AsString;
            if (command.Contains("insert"))
                return command["insert"].AsString;
            if (command.Contains("update"))
                return command["update"].AsString;
            if (command.Contains("delete"))
                return command["delete"].AsString;
            if (command.Contains("aggregate"))
                return command["aggregate"].AsString;
        }
        catch
        {
            // Ignorar errores al extraer el nombre de la colección
        }
        
        return "Unknown";
    }
    
    /// <summary>
    /// Obtiene el nombre del método que llamó a la operación de base de datos
    /// </summary>
    /// <returns>El nombre del método llamador o "Unknown" si no se puede determinar</returns>
    public static string GetCallerMethod()
    {
        try
        {
            var stackTrace = new StackTrace();
            var frames = stackTrace.GetFrames();
            
            foreach (var frame in frames)
            {
                var method = frame.GetMethod();
                var declaringType = method?.DeclaringType;
                
                if (declaringType != null && 
                    !declaringType.FullName.StartsWith("MongoDB.") &&
                    !declaringType.FullName.Contains("MongoDbContext"))
                {
                    return $"{declaringType.Name}.{method.Name}";
                }
            }
        }
        catch
        {
            // Ignorar errores al obtener el método llamador
        }
        
        return "Unknown";
    }

    /// <summary>
    /// Obtiene una colección de MongoDB por su nombre
    /// </summary>
    /// <typeparam name="T">El tipo de documento de la colección</typeparam>
    /// <param name="database">La base de datos de MongoDB</param>
    /// <param name="collectionName">El nombre de la colección</param>
    /// <returns>La colección de MongoDB</returns>
    public static IMongoCollection<T> GetCollection<T>(this IMongoDatabase database, string collectionName)
    {
        return database.GetCollection<T>(collectionName);
    }
} 