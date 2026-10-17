using System.Net;
using System.Text;
using System.Text.Json;
using AdopPlant.Analytics.Models;
using AdopPlant.Analytics.Services;
using Xunit;

namespace AdopPlant.Analytics.Tests;

public class SesionServiceTests
{
    [Fact]
    public async Task IniciarSesion_RespuestaCorrecta_EnviaCredencialesYDevuelveToken()
    {
        // Solo datos ficticios. El handler intercepta HTTP: no hay llamadas reales.
        using var handler = new StubHandler(async request =>
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal("https://python.invalid/api/auth/login", request.RequestUri!.ToString());
            using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync());
            Assert.Equal("prueba@example.com", body.RootElement.GetProperty("identificador").GetString());
            Assert.Equal("SoloPrueba123!", body.RootElement.GetProperty("contrasena").GetString());
            return Json(HttpStatusCode.OK, "{\"access_token\":\"token-ficticio\",\"token_type\":\"bearer\"}");
        });
        using var client = Client(handler);
        var result = await new SesionService(client).IniciarSesionAsync(Credentials(), CancellationToken.None);
        Assert.NotNull(result);
        Assert.Equal("token-ficticio", result.AccessToken);
        Assert.Equal("bearer", result.TokenType);
        Assert.Equal(1, handler.Calls);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task IniciarSesion_PythonRechaza_NoCreaSesion(HttpStatusCode status)
    {
        using var handler = new StubHandler(_ => Task.FromResult(Json(status, "{\"detail\":\"rechazado\"}")));
        using var client = Client(handler);
        var result = await new SesionService(client).IniciarSesionAsync(Credentials(), CancellationToken.None);
        Assert.Null(result);
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task IniciarSesion_ErrorDeRed_NoCreaSesion()
    {
        using var handler = new StubHandler(_ => throw new HttpRequestException("Fallo simulado"));
        using var client = Client(handler);
        Assert.Null(await new SesionService(client).IniciarSesionAsync(Credentials(), CancellationToken.None));
    }

    [Fact]
    public async Task IniciarSesion_RespuestaNula_NoCreaSesion()
    {
        using var handler = new StubHandler(_ => Task.FromResult(Json(HttpStatusCode.OK, "null")));
        using var client = Client(handler);
        Assert.Null(await new SesionService(client).IniciarSesionAsync(Credentials(), CancellationToken.None));
    }

    private static LoginSolicitud Credentials() => new("prueba@example.com", "SoloPrueba123!");
    private static HttpClient Client(HttpMessageHandler handler) => new(handler) { BaseAddress = new Uri("https://python.invalid") };
    private static HttpResponseMessage Json(HttpStatusCode status, string body) => new(status)
    {
        Content = new StringContent(body, Encoding.UTF8, "application/json")
    };

    private sealed class StubHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        public int Calls { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            return respond(request);
        }
    }
}
