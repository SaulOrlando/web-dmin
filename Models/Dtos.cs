namespace AdopPlant.Analytics.Models;

// ---------------------------------------------------------------------------
// Resumen general
// ---------------------------------------------------------------------------
public sealed record ResumenUsuarios(
    int Total, int Activos, int Bloqueados, int NuevosUltimos30Dias);

public sealed record ResumenPlantas(
    int Total, int Disponibles, int Solicitadas, int Adoptadas, int Ocultas, int Eliminadas);

public sealed record ResumenSolicitudes(
    int Total, int Pendientes, int Aceptadas, int Rechazadas);

public sealed record ResumenAdopciones(
    int Total, int EnProceso, int Completadas);

public sealed record ResumenReportes(
    int Total, int EnRevision, int Resueltos);

public sealed record ResumenCategorias(
    int Total, int Activas);

public sealed record ResumenMetricas(
    ResumenUsuarios Usuarios,
    ResumenPlantas Plantas,
    ResumenSolicitudes Solicitudes,
    ResumenAdopciones Adopciones,
    ResumenReportes Reportes,
    ResumenCategorias Categorias);

/// <summary>Fila plana que devuelve la consulta única del resumen.</summary>
internal sealed record ResumenFila(
    int TotalUsuarios,
    int UsuariosActivos,
    int UsuariosBloqueados,
    int UsuariosNuevos30Dias,
    int TotalPlantas,
    int PlantasDisponibles,
    int PlantasSolicitadas,
    int PlantasAdoptadas,
    int PlantasOcultas,
    int PlantasEliminadas,
    int TotalSolicitudes,
    int SolicitudesPendientes,
    int SolicitudesAceptadas,
    int SolicitudesRechazadas,
    int TotalAdopciones,
    int AdopcionesEnProceso,
    int AdopcionesCompletadas,
    int TotalReportes,
    int ReportesEnRevision,
    int ReportesResueltos,
    int TotalCategorias,
    int CategoriasActivas);

// ---------------------------------------------------------------------------
// Series y rankings
// ---------------------------------------------------------------------------
public sealed record PuntoMensual(
    string Periodo,
    int UsuariosNuevos,
    int Publicaciones,
    int Solicitudes,
    int AdopcionesCompletadas);

public sealed record CategoriaMetrica(
    int IdCategoria,
    string Categoria,
    int Publicadas,
    int Adoptadas,
    double PorcentajeAdopcion);

public sealed record EmbudoMetrica(
    int Publicadas,
    int ConSolicitud,
    int Adoptadas,
    double TasaConversion);

public sealed record UsuarioDestacado(
    int IdUsuario,
    string Nombre,
    int Total);

public sealed record UsuariosMetricas(
    IReadOnlyList<UsuarioDestacado> TopDonantes,
    IReadOnlyList<UsuarioDestacado> TopAdoptantes);

public sealed record EstadoConteo(
    string Estado,
    int Total);

public sealed record ReportesMetricas(
    IReadOnlyList<EstadoConteo> PorEstado,
    IReadOnlyList<UsuarioDestacado> MasReportados);

// ---------------------------------------------------------------------------
// Sesión (login delegado en la API FastAPI)
// ---------------------------------------------------------------------------
public sealed record LoginSolicitud(string Identificador, string Contrasena);

public sealed record SesionRespuesta(string AccessToken, string TokenType = "bearer");
