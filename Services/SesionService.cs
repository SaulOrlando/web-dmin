using System.Net.Http.Json;
using System.Text.Json.Serialization;
using AdopPlant.Analytics.Models;

namespace AdopPlant.Analytics.Services;

/// <summary>
/// Reenvía el login al backend FastAPI (mismos usuarios y lógica de validación).
/// </summary>
public sealed class SesionService
{
    private readonly HttpClient _http;

    public SesionService(HttpClient http)
    {
        _http = http;
    }

    public async Task<SesionRespuesta?> IniciarSesionAsync(LoginSolicitud solicitud, CancellationToken ct)
    {
        HttpResponseMessage response;
        try
        {
            response = await _http.PostAsJsonAsync("/api/auth/login", solicitud, ct);
        }
        catch (HttpRequestException)
        {
            // FastAPI apagado o inalcanzable: se informa como login fallido.
            return null;
        }

        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var resultado = await response.Content.ReadFromJsonAsync<TokenFastApi>(
            cancellationToken: ct);

        return resultado is not null
            ? new SesionRespuesta(resultado.AccessToken)
            : null;
    }

    private sealed record TokenFastApi(
        [property: JsonPropertyName("access_token")] string AccessToken,
        [property: JsonPropertyName("token_type")] string TokenType);
}
