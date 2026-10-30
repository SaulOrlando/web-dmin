using System.Net;
using System.Net.Http.Json;
using AdopPlant.Analytics.Models;
using AdopPlant.Analytics.Tests.Infra;
using Xunit;

namespace AdopPlant.Analytics.Tests;

/// <summary>
/// SCRUM-107 · Pruebas unitarias de los endpoints de métricas (C# Analytics).
/// Casos C-107-03 a C-107-12 del ticket: autorización y ajuste de parámetros.
/// </summary>
public class MetricasEndpointsTests : IClassFixture<FabricaAnalitica>
{
    private readonly FabricaAnalitica _fabrica;

    public MetricasEndpointsTests(FabricaAnalitica fabrica) => _fabrica = fabrica;

    [Fact]
    public async Task C107_03_ResumenSinToken_Devuelve401()
    {
        using var cliente = _fabrica.CreateClient();

        var respuesta = await cliente.GetAsync("/api/metricas/resumen");

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
    }

    [Fact]
    public async Task C107_04_ResumenConUsuarioNoAdministrador_Devuelve403()
    {
        using var cliente = _fabrica.ComoUsuario();

        var respuesta = await cliente.GetAsync("/api/metricas/resumen");

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
    }

    [Fact]
    public async Task C107_05_EvolucionSinMeses_UsaDocePorDefecto()
    {
        using var cliente = _fabrica.ComoAdministrador();
        _fabrica.Repositorio.UltimoMeses = null;

        var puntos = await cliente.GetFromJsonAsync<List<PuntoMensual>>("/api/metricas/evolucion");

        Assert.Equal(12, _fabrica.Repositorio.UltimoMeses);
        Assert.NotNull(puntos);
        Assert.Equal(12, puntos!.Count);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-5, 1)]
    [InlineData(99, 24)]
    [InlineData(25, 24)]
    public async Task C107_06_07_EvolucionAjustaMesesAlRango1a24(int pedido, int esperado)
    {
        using var cliente = _fabrica.ComoAdministrador();

        var puntos = await cliente.GetFromJsonAsync<List<PuntoMensual>>(
            $"/api/metricas/evolucion?meses={pedido}");

        Assert.Equal(esperado, _fabrica.Repositorio.UltimoMeses);
        Assert.NotNull(puntos);
        Assert.Equal(esperado, puntos!.Count);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-3, 1)]
    [InlineData(999, 20)]
    public async Task C107_08_09_UsuariosAjustaLimiteAlRango1a20(int pedido, int esperado)
    {
        using var cliente = _fabrica.ComoAdministrador();

        var usuarios = await cliente.GetFromJsonAsync<UsuariosMetricas>(
            $"/api/metricas/usuarios?limite={pedido}");

        Assert.Equal(esperado, _fabrica.Repositorio.UltimoLimiteUsuarios);
        Assert.NotNull(usuarios);
        Assert.Equal(esperado, usuarios!.TopDonantes.Count);
        Assert.Equal(esperado, usuarios.TopAdoptantes.Count);
    }

    [Fact]
    public async Task C107_10_ReportesAjustaLimite999A20()
    {
        using var cliente = _fabrica.ComoAdministrador();

        var reportes = await cliente.GetFromJsonAsync<ReportesMetricas>(
            "/api/metricas/reportes?limite=999");

        Assert.Equal(20, _fabrica.Repositorio.UltimoLimiteReportes);
        Assert.NotNull(reportes);
        Assert.Equal(20, reportes!.MasReportados.Count);
    }

    [Fact]
    public async Task C107_11_ReportesSinLimite_UsaCincoPorDefecto()
    {
        using var cliente = _fabrica.ComoAdministrador();
        _fabrica.Repositorio.UltimoLimiteReportes = null;

        var reportes = await cliente.GetFromJsonAsync<ReportesMetricas>("/api/metricas/reportes");

        Assert.Equal(5, _fabrica.Repositorio.UltimoLimiteReportes);
        Assert.NotNull(reportes);
        Assert.Equal(5, reportes!.MasReportados.Count);
    }

    [Fact]
    public async Task C107_12_LasSeisRutasDelGrupoResponden200ConLosDtoEsperados()
    {
        using var cliente = _fabrica.ComoAdministrador();

        var resumen = await cliente.GetFromJsonAsync<ResumenMetricas>("/api/metricas/resumen");
        var evolucion = await cliente.GetFromJsonAsync<List<PuntoMensual>>("/api/metricas/evolucion");
        var categorias = await cliente.GetFromJsonAsync<List<CategoriaMetrica>>(
            "/api/metricas/plantas-por-categoria");
        var embudo = await cliente.GetFromJsonAsync<EmbudoMetrica>("/api/metricas/embudo");
        var usuarios = await cliente.GetFromJsonAsync<UsuariosMetricas>("/api/metricas/usuarios");
        var reportes = await cliente.GetFromJsonAsync<ReportesMetricas>("/api/metricas/reportes");

        Assert.NotNull(resumen);
        Assert.Equal(18, resumen!.Usuarios.Total);
        Assert.NotNull(evolucion);
        Assert.NotEmpty(evolucion!);
        Assert.NotNull(categorias);
        Assert.Equal(2, categorias!.Count);
        Assert.NotNull(embudo);
        Assert.Equal(25.0, embudo!.TasaConversion);
        Assert.NotNull(usuarios);
        Assert.NotEmpty(usuarios!.TopDonantes);
        Assert.NotNull(reportes);
        Assert.NotEmpty(reportes!.PorEstado);
    }

    [Fact]
    public async Task C107_12b_RutaDesconocidaDelGrupoDevuelve404()
    {
        using var cliente = _fabrica.ComoAdministrador();

        var respuesta = await cliente.GetAsync("/api/metricas/no-existe");

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
    }
}
