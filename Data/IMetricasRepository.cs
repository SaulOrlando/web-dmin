using AdopPlant.Analytics.Models;

namespace AdopPlant.Analytics.Data;

/// <summary>
/// Contrato de las consultas de métricas del panel de administración.
/// Existe para poder sustituir el repositorio por un doble de prueba en las
/// pruebas de endpoints (SCRUM-107) sin abrir conexiones a PostgreSQL.
/// </summary>
public interface IMetricasRepository
{
    Task<ResumenMetricas> ObtenerResumenAsync(CancellationToken ct);

    Task<IReadOnlyList<PuntoMensual>> ObtenerEvolucionMensualAsync(int meses, CancellationToken ct);

    Task<IReadOnlyList<CategoriaMetrica>> ObtenerPlantasPorCategoriaAsync(CancellationToken ct);

    Task<EmbudoMetrica> ObtenerEmbudoAsync(CancellationToken ct);

    Task<UsuariosMetricas> ObtenerUsuariosDestacadosAsync(int limite, CancellationToken ct);

    Task<ReportesMetricas> ObtenerReportesAsync(int limite, CancellationToken ct);
}
