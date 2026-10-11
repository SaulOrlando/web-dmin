using Npgsql;

namespace AdopPlant.Analytics.Data;

/// <summary>
/// Crea conexiones a la MISMA base PostgreSQL que usa la API FastAPI.
/// La API de métricas es de solo lectura: nunca escribe.
/// </summary>
public sealed class DatabaseConnectionFactory
{
    private readonly string _cadenaConexion;

    public DatabaseConnectionFactory(IConfiguration configuration)
    {
        _cadenaConexion = configuration.GetConnectionString("AdopPlant")
            ?? throw new InvalidOperationException(
                "Falta ConnectionStrings:AdopPlant en la configuración.");
    }

    public NpgsqlConnection Crear() => new(_cadenaConexion);
}
