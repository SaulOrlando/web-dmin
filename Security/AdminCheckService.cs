using AdopPlant.Analytics.Data;
using Dapper;

namespace AdopPlant.Analytics.Security;

public sealed record UsuarioAdminInfo(
    int IdUsuario,
    string Nombre,
    string Apellido,
    string Estado,
    bool EsAdministrador);

/// <summary>
/// Verifica en la base de datos que el usuario del JWT exista, esté activo
/// y pertenezca a la tabla <c>administrador</c>. Replica la lógica de
/// <c>obtener_administrador_actual</c> de FastAPI.
/// </summary>
public sealed class AdminCheckService
{
    private const string Consulta = """
        SELECT u.id_usuario            AS IdUsuario,
               u.nombre                AS Nombre,
               u.apellido              AS Apellido,
               u.estado::text          AS Estado,
               (a.id_administrador IS NOT NULL) AS EsAdministrador
        FROM public.usuario AS u
        LEFT JOIN public.administrador AS a
               ON a.id_usuario = u.id_usuario
        WHERE u.id_usuario = @IdUsuario
        """;

    private readonly DatabaseConnectionFactory _db;

    public AdminCheckService(DatabaseConnectionFactory db) => _db = db;

    public async Task<UsuarioAdminInfo?> ObtenerAsync(int idUsuario, CancellationToken ct)
    {
        await using var conexion = _db.Crear();
        return await conexion.QuerySingleOrDefaultAsync<UsuarioAdminInfo>(
            new CommandDefinition(Consulta, new { IdUsuario = idUsuario }, cancellationToken: ct));
    }
}
