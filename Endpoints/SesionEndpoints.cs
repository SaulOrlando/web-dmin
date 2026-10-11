using AdopPlant.Analytics.Models;
using AdopPlant.Analytics.Security;
using AdopPlant.Analytics.Services;

namespace AdopPlant.Analytics.Endpoints;

public static class SesionEndpoints
{
    public static IEndpointRouteBuilder MapSesionEndpoints(this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup("/api/sesion")
            .WithTags("Sesión");

        grupo.MapPost("", async (LoginSolicitud solicitud, SesionService sesion, CancellationToken ct) =>
        {
            var resultado = await sesion.IniciarSesionAsync(solicitud, ct);

            if (resultado is null)
            {
                return Results.Unauthorized();
            }

            return Results.Ok(resultado);
        })
        .WithName("LoginDelegado")
        .WithSummary("Delega el inicio de sesión a la API FastAPI (AdopPlant).");

        return app;
    }
}
