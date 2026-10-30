using System.Net;
using System.Net.Http.Json;
using AdopPlant.Analytics.Models;
using AdopPlant.Analytics.Tests.Infra;
using Xunit;

namespace AdopPlant.Analytics.Tests;

/// <summary>
/// SCRUM-107 · Pruebas unitarias de los endpoints de sesión (C# Analytics).
/// Casos C-107-01 y C-107-02 del ticket.
/// </summary>
public class SesionEndpointsTests : IClassFixture<FabricaAnalitica>
{
    private readonly FabricaAnalitica _fabrica;

    public SesionEndpointsTests(FabricaAnalitica fabrica) => _fabrica = fabrica;

    [Fact]
    public async Task C107_01_LoginConCredencialesValidas_Devuelve200ConToken()
    {
        using var cliente = _fabrica.CreateClient();

        var respuesta = await cliente.PostAsJsonAsync(
            "/api/sesion",
            new LoginSolicitud("admin.panel@adopplant.com", FabricaAnalitica.ContrasenaValida));

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);

        var sesion = await respuesta.Content.ReadFromJsonAsync<SesionRespuesta>();

        Assert.NotNull(sesion);
        Assert.Equal(FabricaAnalitica.TokenFalso, sesion!.AccessToken);
        Assert.Equal("bearer", sesion.TokenType);
    }

    [Fact]
    public async Task C107_02_LoginConCredencialesInvalidas_Devuelve401()
    {
        using var cliente = _fabrica.CreateClient();

        var respuesta = await cliente.PostAsJsonAsync(
            "/api/sesion",
            new LoginSolicitud("admin.panel@adopplant.com", "contrasena-incorrecta"));

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
        Assert.Empty(await respuesta.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task C107_12b_LoginNoExponeErrores500()
    {
        using var cliente = _fabrica.CreateClient();

        var respuesta = await cliente.PostAsJsonAsync(
            "/api/sesion",
            new LoginSolicitud("", ""));

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
    }
}
