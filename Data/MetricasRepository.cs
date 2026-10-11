using AdopPlant.Analytics.Models;
using Dapper;

namespace AdopPlant.Analytics.Data;

/// <summary>
/// Consultas de solo lectura para el panel de métricas del administrador.
/// Todas las consultas respetan el esquema real de AdopPlant (PostgreSQL 18).
/// </summary>
public sealed class MetricasRepository
{
    private const string SqlResumen = """
        SELECT
            (SELECT COUNT(*) FROM public.usuario)::int                                        AS TotalUsuarios,
            (SELECT COUNT(*) FROM public.usuario WHERE estado = 'ACTIVO')::int                AS UsuariosActivos,
            (SELECT COUNT(*) FROM public.usuario WHERE estado = 'BLOQUEADO')::int            AS UsuariosBloqueados,
            (SELECT COUNT(*) FROM public.usuario
              WHERE fecha_registro >= now() - interval '30 days')::int                        AS UsuariosNuevos30Dias,

            (SELECT COUNT(*) FROM public.planta)::int                                         AS TotalPlantas,
            (SELECT COUNT(*) FROM public.planta
              WHERE NOT eliminada AND visible AND estado = 'DISPONIBLE')::int                 AS PlantasDisponibles,
            (SELECT COUNT(*) FROM public.planta
              WHERE NOT eliminada AND visible AND estado = 'SOLICITADA')::int                 AS PlantasSolicitadas,
            (SELECT COUNT(*) FROM public.planta
              WHERE NOT eliminada AND estado = 'ADOPTADA')::int                                AS PlantasAdoptadas,
            (SELECT COUNT(*) FROM public.planta
              WHERE NOT visible AND NOT eliminada)::int                                        AS PlantasOcultas,
            (SELECT COUNT(*) FROM public.planta WHERE eliminada)::int                         AS PlantasEliminadas,

            (SELECT COUNT(*) FROM public.solicitud_adopcion)::int                             AS TotalSolicitudes,
            (SELECT COUNT(*) FROM public.solicitud_adopcion
              WHERE estado = 'PENDIENTE')::int                                                 AS SolicitudesPendientes,
            (SELECT COUNT(*) FROM public.solicitud_adopcion
              WHERE estado = 'ACEPTADA')::int                                                  AS SolicitudesAceptadas,
            (SELECT COUNT(*) FROM public.solicitud_adopcion
              WHERE estado = 'RECHAZADA')::int                                                 AS SolicitudesRechazadas,

            (SELECT COUNT(*) FROM public.adopcion)::int                                       AS TotalAdopciones,
            (SELECT COUNT(*) FROM public.adopcion WHERE estado = 'EN_PROCESO')::int           AS AdopcionesEnProceso,
            (SELECT COUNT(*) FROM public.adopcion WHERE estado = 'COMPLETADA')::int           AS AdopcionesCompletadas,

            (SELECT COUNT(*) FROM public.reporte)::int                                        AS TotalReportes,
            (SELECT COUNT(*) FROM public.reporte WHERE estado = 'EN_REVISION')::int           AS ReportesEnRevision,
            (SELECT COUNT(*) FROM public.reporte WHERE estado = 'RESUELTO')::int              AS ReportesResueltos,

            (SELECT COUNT(*) FROM public.categoria)::int                                      AS TotalCategorias,
            (SELECT COUNT(*) FROM public.categoria WHERE estado = 'ACTIVA')::int              AS CategoriasActivas
        """;

    private const string SqlEvolucion = """
        SELECT to_char(mes, 'YYYY-MM') AS Periodo,
               COUNT(DISTINCT u.id_usuario)::int     AS UsuariosNuevos,
               COUNT(DISTINCT p.id_planta)::int      AS Publicaciones,
               COUNT(DISTINCT s.id_solicitud)::int   AS Solicitudes,
               COUNT(DISTINCT a.id_adopcion)::int    AS AdopcionesCompletadas
        FROM generate_series(
                 date_trunc('month', now()) - ((@Meses - 1) * interval '1 month'),
                 date_trunc('month', now()),
                 interval '1 month') AS mes
        LEFT JOIN public.usuario AS u
               ON date_trunc('month', u.fecha_registro) = mes
        LEFT JOIN public.planta AS p
               ON date_trunc('month', p.fecha_publicacion) = mes
        LEFT JOIN public.solicitud_adopcion AS s
               ON date_trunc('month', s.fecha_solicitud) = mes
        LEFT JOIN public.adopcion AS a
               ON a.estado = 'COMPLETADA'
              AND date_trunc('month', a.fecha_recepcion) = mes
        GROUP BY mes
        ORDER BY mes
        """;

    private const string SqlPlantasPorCategoria = """
        SELECT c.id_categoria AS IdCategoria,
               c.nombre       AS Categoria,
               COUNT(p.id_planta)::int AS Publicadas,
               (COUNT(p.id_planta) FILTER (WHERE p.estado = 'ADOPTADA'))::int AS Adoptadas
        FROM public.categoria AS c
        LEFT JOIN public.planta AS p
               ON p.id_categoria = c.id_categoria
              AND NOT p.eliminada
        GROUP BY c.id_categoria, c.nombre
        ORDER BY Publicadas DESC, c.nombre
        """;

    private const string SqlEmbudo = """
        WITH base AS (
            SELECT p.id_planta,
                   p.estado::text AS estado,
                   EXISTS (
                       SELECT 1 FROM public.solicitud_adopcion AS s
                       WHERE s.id_planta = p.id_planta
                   ) AS tiene_solicitud
            FROM public.planta AS p
            WHERE NOT p.eliminada
        )
        SELECT COUNT(*)::int                                        AS Publicadas,
               (COUNT(*) FILTER (WHERE tiene_solicitud))::int        AS ConSolicitud,
               (COUNT(*) FILTER (WHERE estado = 'ADOPTADA'))::int    AS Adoptadas
        FROM base
        """;

    private const string SqlTopDonantes = """
        SELECT u.id_usuario AS IdUsuario,
               (u.nombre || ' ' || u.apellido) AS Nombre,
               COUNT(a.id_adopcion)::int AS Total
        FROM public.adopcion AS a
        JOIN public.usuario AS u ON u.id_usuario = a.id_donante
        WHERE a.estado = 'COMPLETADA'
        GROUP BY u.id_usuario, u.nombre, u.apellido
        ORDER BY Total DESC, Nombre
        LIMIT @Limite
        """;

    private const string SqlTopAdoptantes = """
        SELECT u.id_usuario AS IdUsuario,
               (u.nombre || ' ' || u.apellido) AS Nombre,
               COUNT(a.id_adopcion)::int AS Total
        FROM public.adopcion AS a
        JOIN public.usuario AS u ON u.id_usuario = a.id_adoptante
        WHERE a.estado = 'COMPLETADA'
        GROUP BY u.id_usuario, u.nombre, u.apellido
        ORDER BY Total DESC, Nombre
        LIMIT @Limite
        """;

    private const string SqlReportesPorEstado = """
        SELECT estado::text AS Estado,
               COUNT(*)::int AS Total
        FROM public.reporte
        GROUP BY estado
        ORDER BY Total DESC
        """;

    private const string SqlMasReportados = """
        SELECT u.id_usuario AS IdUsuario,
               (u.nombre || ' ' || u.apellido) AS Nombre,
               COUNT(r.id_reporte)::int AS Total
        FROM public.reporte AS r
        JOIN public.usuario AS u ON u.id_usuario = r.id_reportado
        GROUP BY u.id_usuario, u.nombre, u.apellido
        ORDER BY Total DESC, Nombre
        LIMIT @Limite
        """;

    private readonly DatabaseConnectionFactory _db;

    public MetricasRepository(DatabaseConnectionFactory db) => _db = db;

    public async Task<ResumenMetricas> ObtenerResumenAsync(CancellationToken ct)
    {
        await using var conexion = _db.Crear();
        var f = await conexion.QuerySingleAsync<ResumenFila>(
            new CommandDefinition(SqlResumen, cancellationToken: ct));

        return new ResumenMetricas(
            new ResumenUsuarios(
                f.TotalUsuarios, f.UsuariosActivos, f.UsuariosBloqueados, f.UsuariosNuevos30Dias),
            new ResumenPlantas(
                f.TotalPlantas, f.PlantasDisponibles, f.PlantasSolicitadas,
                f.PlantasAdoptadas, f.PlantasOcultas, f.PlantasEliminadas),
            new ResumenSolicitudes(
                f.TotalSolicitudes, f.SolicitudesPendientes,
                f.SolicitudesAceptadas, f.SolicitudesRechazadas),
            new ResumenAdopciones(
                f.TotalAdopciones, f.AdopcionesEnProceso, f.AdopcionesCompletadas),
            new ResumenReportes(
                f.TotalReportes, f.ReportesEnRevision, f.ReportesResueltos),
            new ResumenCategorias(
                f.TotalCategorias, f.CategoriasActivas));
    }

    public async Task<IReadOnlyList<PuntoMensual>> ObtenerEvolucionMensualAsync(
        int meses, CancellationToken ct)
    {
        await using var conexion = _db.Crear();
        var filas = await conexion.QueryAsync<PuntoMensual>(
            new CommandDefinition(SqlEvolucion, new { Meses = meses }, cancellationToken: ct));
        return filas.AsList();
    }

    public async Task<IReadOnlyList<CategoriaMetrica>> ObtenerPlantasPorCategoriaAsync(
        CancellationToken ct)
    {
        await using var conexion = _db.Crear();
        var filas = await conexion.QueryAsync<CategoriaFila>(
            new CommandDefinition(SqlPlantasPorCategoria, cancellationToken: ct));

        return filas.Select(f => new CategoriaMetrica(
            f.IdCategoria,
            f.Categoria,
            f.Publicadas,
            f.Adoptadas,
            f.Publicadas == 0 ? 0 : Math.Round(f.Adoptadas * 100.0 / f.Publicadas, 2)))
            .ToList();
    }

    public async Task<EmbudoMetrica> ObtenerEmbudoAsync(CancellationToken ct)
    {
        await using var conexion = _db.Crear();
        var e = await conexion.QuerySingleAsync<EmbudoFila>(
            new CommandDefinition(SqlEmbudo, cancellationToken: ct));

        var tasa = e.Publicadas == 0 ? 0 : Math.Round(e.Adoptadas * 100.0 / e.Publicadas, 2);

        return new EmbudoMetrica(e.Publicadas, e.ConSolicitud, e.Adoptadas, tasa);
    }

    public async Task<UsuariosMetricas> ObtenerUsuariosDestacadosAsync(
        int limite, CancellationToken ct)
    {
        await using var conexion = _db.Crear();

        var donantes = await conexion.QueryAsync<UsuarioDestacado>(
            new CommandDefinition(SqlTopDonantes, new { Limite = limite }, cancellationToken: ct));
        var adoptantes = await conexion.QueryAsync<UsuarioDestacado>(
            new CommandDefinition(SqlTopAdoptantes, new { Limite = limite }, cancellationToken: ct));

        return new UsuariosMetricas(donantes.AsList(), adoptantes.AsList());
    }

    public async Task<ReportesMetricas> ObtenerReportesAsync(int limite, CancellationToken ct)
    {
        await using var conexion = _db.Crear();

        var porEstado = await conexion.QueryAsync<EstadoConteo>(
            new CommandDefinition(SqlReportesPorEstado, cancellationToken: ct));
        var masReportados = await conexion.QueryAsync<UsuarioDestacado>(
            new CommandDefinition(SqlMasReportados, new { Limite = limite }, cancellationToken: ct));

        return new ReportesMetricas(porEstado.AsList(), masReportados.AsList());
    }

    // Records auxiliares para mapear cada consulta.
    private sealed record CategoriaFila(
        int IdCategoria, string Categoria, int Publicadas, int Adoptadas);

    private sealed record EmbudoFila(
        int Publicadas, int ConSolicitud, int Adoptadas);
}
