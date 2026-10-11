using AdopPlant.Analytics.Data;
using AdopPlant.Analytics.Security;

namespace AdopPlant.Analytics.Endpoints;

public static class MetricasEndpoints
{
    public static IEndpointRouteBuilder MapMetricasEndpoints(this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup("/api/metricas")
            .RequireAuthorization(Politicas.EsAdministrador)
            .WithTags("Métricas");

        grupo.MapGet("/resumen", async (MetricasRepository repo, CancellationToken ct) =>
                Results.Ok(await repo.ObtenerResumenAsync(ct)))
            .WithName("ResumenGeneral")
            .WithSummary("Indicadores principales: usuarios, plantas, solicitudes, adopciones y reportes.");

        grupo.MapGet("/evolucion", async (
                MetricasRepository repo, CancellationToken ct, int meses = 12) =>
            {
                meses = Math.Clamp(meses, 1, 24);
                return Results.Ok(await repo.ObtenerEvolucionMensualAsync(meses, ct));
            })
            .WithName("EvolucionMensual")
            .WithSummary("Publicaciones, solicitudes, adopciones completadas y usuarios nuevos por mes.");

        grupo.MapGet("/plantas-por-categoria", async (MetricasRepository repo, CancellationToken ct) =>
                Results.Ok(await repo.ObtenerPlantasPorCategoriaAsync(ct)))
            .WithName("PlantasPorCategoria")
            .WithSummary("Publicaciones y adopciones por categoría, con porcentaje de adopción.");

        grupo.MapGet("/embudo", async (MetricasRepository repo, CancellationToken ct) =>
                Results.Ok(await repo.ObtenerEmbudoAsync(ct)))
            .WithName("EmbudoConversion")
            .WithSummary("Embudo de conversión: publicadas → con solicitud → adoptadas.");

        grupo.MapGet("/usuarios", async (
                MetricasRepository repo, CancellationToken ct, int limite = 5) =>
            {
                limite = Math.Clamp(limite, 1, 20);
                return Results.Ok(await repo.ObtenerUsuariosDestacadosAsync(limite, ct));
            })
            .WithName("UsuariosDestacados")
            .WithSummary("Usuarios con más donaciones y más adopciones completadas.");

        grupo.MapGet("/reportes", async (
                MetricasRepository repo, CancellationToken ct, int limite = 5) =>
            {
                limite = Math.Clamp(limite, 1, 20);
                return Results.Ok(await repo.ObtenerReportesAsync(limite, ct));
            })
            .WithName("ReportesMetricas")
            .WithSummary("Reportes por estado y usuarios más reportados.");

        return app;
    }
}
