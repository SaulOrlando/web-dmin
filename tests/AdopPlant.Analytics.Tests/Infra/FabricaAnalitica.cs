using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using AdopPlant.Analytics.Data;
using AdopPlant.Analytics.Models;
using AdopPlant.Analytics.Security;
using AdopPlant.Analytics.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AdopPlant.Analytics.Tests.Infra;

/// <summary>
/// Infraestructura de las pruebas de endpoints (SCRUM-107).
/// Monta la API real en memoria, sustituye PostgreSQL por un repositorio falso
/// y reemplaza el JWT de Adoplant por un esquema de autenticación de prueba.
/// </summary>
public sealed class FabricaAnalitica : WebApplicationFactory<Program>
{
    public const string CabeceraUsuario = "X-Prueba-Usuario";
    public const string CabeceraRol = "X-Prueba-Rol";
    public const string Esquema = "Prueba";
    public const string ContrasenaValida = "ClaveCorrecta2026!";
    public const string TokenFalso = "token-falso-de-prueba";

    public RepositorioFalso Repositorio { get; } = new();

    static FabricaAnalitica()
    {
        // La configuración se lee al arrancar Program, antes de ConfigureWebHost.
        Environment.SetEnvironmentVariable("Jwt__Secret", new string('s', 64));
        Environment.SetEnvironmentVariable(
            "ConnectionStrings__AdopPlant",
            "Host=127.0.0.1;Port=5432;Database=plant;Username=postgres;Password=pruebas");
        Environment.SetEnvironmentVariable("FastApi__BaseUrl", "http://127.0.0.1:8000");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseContentRoot(RutaProyecto());

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IMetricasRepository>();
            services.AddSingleton<IMetricasRepository>(Repositorio);

            services.RemoveAll<SesionService>();
            services.AddScoped(_ => new SesionService(
                new HttpClient(new ManejadorLoginFalso())
                {
                    BaseAddress = new Uri("http://analytics.pruebas"),
                }));

            // El handler real consulta PostgreSQL; en estas pruebas se sustituye
            // por uno que lee el rol de la cabecera X-Prueba-Rol.
            services.RemoveAll<IAuthorizationHandler>();
            services.AddSingleton<IAuthorizationHandler, AutorizacionAdministradorDePrueba>();

            services.AddAuthentication(Esquema)
                .AddScheme<AuthenticationSchemeOptions, AutenticacionDePrueba>(Esquema, _ => { });
        });
    }

    /// <summary>Cliente autenticado como administrador activo.</summary>
    public HttpClient ComoAdministrador(int idUsuario = 7)
    {
        var cliente = CreateClient();
        cliente.DefaultRequestHeaders.Add(CabeceraUsuario, idUsuario.ToString());
        cliente.DefaultRequestHeaders.Add(CabeceraRol, "ADMIN");
        return cliente;
    }

    /// <summary>Cliente autenticado como usuario normal (no administrador).</summary>
    public HttpClient ComoUsuario(int idUsuario = 8)
    {
        var cliente = CreateClient();
        cliente.DefaultRequestHeaders.Add(CabeceraUsuario, idUsuario.ToString());
        cliente.DefaultRequestHeaders.Add(CabeceraRol, "USUARIO");
        return cliente;
    }

    private static string RutaProyecto()
    {
        var directorio = new DirectoryInfo(AppContext.BaseDirectory);
        while (directorio is not null)
        {
            if (File.Exists(Path.Combine(directorio.FullName, "AdopPlant.Analytics.csproj")))
            {
                return directorio.FullName;
            }

            directorio = directorio.Parent;
        }

        throw new InvalidOperationException(
            "No se encontró la carpeta del proyecto AdopPlant.Analytics desde " +
            AppContext.BaseDirectory);
    }
}

/// <summary>Repositorio de métricas en memoria: no abre PostgreSQL.</summary>
public sealed class RepositorioFalso : IMetricasRepository
{
    public int? UltimoMeses { get; set; }

    public int? UltimoLimiteUsuarios { get; set; }

    public int? UltimoLimiteReportes { get; set; }

    public Task<ResumenMetricas> ObtenerResumenAsync(CancellationToken ct) =>
        Task.FromResult(new ResumenMetricas(
            new ResumenUsuarios(18, 16, 1, 3),
            new ResumenPlantas(8, 5, 1, 2, 1, 0),
            new ResumenSolicitudes(6, 2, 3, 1),
            new ResumenAdopciones(4, 1, 3),
            new ResumenReportes(2, 1, 1),
            new ResumenCategorias(4, 4)));

    public Task<IReadOnlyList<PuntoMensual>> ObtenerEvolucionMensualAsync(
        int meses, CancellationToken ct)
    {
        UltimoMeses = meses;
        var puntos = Enumerable.Range(1, meses)
            .Select(mes => new PuntoMensual($"2026-{mes:00}", mes, mes * 2, mes * 3, mes))
            .ToList();

        return Task.FromResult<IReadOnlyList<PuntoMensual>>(puntos);
    }

    public Task<IReadOnlyList<CategoriaMetrica>> ObtenerPlantasPorCategoriaAsync(
        CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<CategoriaMetrica>>(new List<CategoriaMetrica>
        {
            new(1, "Interior", 4, 2, 50.0),
            new(2, "Exterior", 2, 0, 0.0),
        });

    public Task<EmbudoMetrica> ObtenerEmbudoAsync(CancellationToken ct) =>
        Task.FromResult(new EmbudoMetrica(8, 5, 2, 25.0));

    public Task<UsuariosMetricas> ObtenerUsuariosDestacadosAsync(int limite, CancellationToken ct)
    {
        UltimoLimiteUsuarios = limite;
        var donantes = Enumerable.Range(1, limite)
            .Select(i => new UsuarioDestacado(i, $"Donante {i}", i))
            .ToList();
        var adoptantes = Enumerable.Range(1, limite)
            .Select(i => new UsuarioDestacado(100 + i, $"Adoptante {i}", i))
            .ToList();

        return Task.FromResult(new UsuariosMetricas(donantes, adoptantes));
    }

    public Task<ReportesMetricas> ObtenerReportesAsync(int limite, CancellationToken ct)
    {
        UltimoLimiteReportes = limite;
        var masReportados = Enumerable.Range(1, limite)
            .Select(i => new UsuarioDestacado(i, $"Reportado {i}", i))
            .ToList();

        return Task.FromResult(new ReportesMetricas(
            new List<EstadoConteo> { new("EN_REVISION", 1), new("RESUELTO", 1) },
            masReportados));
    }
}

/// <summary>Devuelve 200 con token solo para la contraseña válida; 401 en el resto.</summary>
public sealed class ManejadorLoginFalso : HttpMessageHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var cuerpo = request.Content is null
            ? string.Empty
            : await request.Content.ReadAsStringAsync(cancellationToken);

        if (!cuerpo.Contains(FabricaAnalitica.ContrasenaValida, StringComparison.Ordinal))
        {
            return new HttpResponseMessage(HttpStatusCode.Unauthorized);
        }

        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                $$"""{"access_token":"{{FabricaAnalitica.TokenFalso}}","token_type":"bearer"}""",
                System.Text.Encoding.UTF8,
                "application/json"),
        };
    }
}

/// <summary>Autenticación de prueba basada en cabeceras; no valida JWT reales.</summary>
public sealed class AutenticacionDePrueba : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public AutenticacionDePrueba(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var id = Request.Headers[FabricaAnalitica.CabeceraUsuario].FirstOrDefault();
        var rol = Request.Headers[FabricaAnalitica.CabeceraRol].FirstOrDefault();

        if (id is null || rol is null)
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var identidad = new ClaimsIdentity(
            new[]
            {
                new Claim("sub", id),
                new Claim(ClaimTypes.Role, rol),
            },
            FabricaAnalitica.Esquema);

        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identidad), FabricaAnalitica.Esquema);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}

/// <summary>Satisface la política EsAdministrador cuando el rol de prueba es ADMIN.</summary>
public sealed class AutorizacionAdministradorDePrueba
    : AuthorizationHandler<EsAdministradorRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context, EsAdministradorRequirement requirement)
    {
        var rol = context.User.FindFirst(ClaimTypes.Role)?.Value;

        if (string.Equals(rol, "ADMIN", StringComparison.Ordinal) &&
            int.TryParse(context.User.FindFirst("sub")?.Value, out _))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
