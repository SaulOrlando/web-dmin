using System.Text;
using AdopPlant.Analytics.Data;
using AdopPlant.Analytics.Endpoints;
using AdopPlant.Analytics.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------------
// Configuración
// ---------------------------------------------------------------------------
var jwtSettings = builder.Configuration.GetSection("Jwt").Get<JwtSettings>()
    ?? throw new InvalidOperationException(
        "Falta la sección 'Jwt' en la configuración (appsettings o variables de entorno).");

jwtSettings.Validar();

// ---------------------------------------------------------------------------
// Servicios de la aplicación
// ---------------------------------------------------------------------------
builder.Services.AddSingleton(jwtSettings);
builder.Services.AddSingleton<DatabaseConnectionFactory>();
builder.Services.AddScoped<MetricasRepository>();
builder.Services.AddScoped<AdminCheckService>();

// HttpClient para delegar el login a la API FastAPI
var urlFastApi = builder.Configuration.GetValue<string>("FastApi:BaseUrl")
    ?? builder.Configuration.GetValue<string>("AdopPlantApi:BaseUrl")
    ?? "http://127.0.0.1:8000";
builder.Services.AddHttpClient<AdopPlant.Analytics.Services.SesionService>(c =>
{
    c.BaseAddress = new Uri(urlFastApi);
    c.Timeout = TimeSpan.FromSeconds(30);
});

// ---------------------------------------------------------------------------
// Autenticación: valida el MISMO JWT que emite la API de FastAPI (HS256).
// El token solo trae sub/iat/exp; el rol de administrador se consulta en la BD.
// ---------------------------------------------------------------------------
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false; // conservar el claim "sub" tal cual
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtSettings.Secret)),
            NameClaimType = "sub",
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(Politicas.EsAdministrador, policy =>
        policy.Requirements.Add(new EsAdministradorRequirement()));
});
builder.Services.AddScoped<IAuthorizationHandler, EsAdministradorHandler>();

// ---------------------------------------------------------------------------
// CORS para los clientes (web Adoplant y app PlantHaven)
// ---------------------------------------------------------------------------
builder.Services.AddCors(options =>
{
    options.AddPolicy("Clientes", policy =>
    {
        var origenes = builder.Configuration.GetSection("Cors:Origenes").Get<string[]>()
            ?? Array.Empty<string>();

        if (origenes.Length == 0)
        {
            policy.AllowAnyOrigin();
        }
        else
        {
            policy.WithOrigins(origenes);
        }

        policy.AllowAnyHeader().AllowAnyMethod();
    });
});

// ---------------------------------------------------------------------------
// Swagger con soporte de Bearer
// ---------------------------------------------------------------------------
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "AdopPlant Analytics API",
        Version = "v1",
        Description =
            "API auxiliar de métricas administrativas para AdopPlant. " +
            "Lee la misma base PostgreSQL y valida el mismo JWT que la API FastAPI.",
    });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Pega aquí el JWT que entrega el login de AdopPlant (FastAPI).",
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer",
                },
            },
            Array.Empty<string>()
        },
    });
});

var app = builder.Build();

// ---------------------------------------------------------------------------
// Pipeline
// ---------------------------------------------------------------------------
app.UseSwagger();
app.UseSwaggerUI();

app.UseCors("Clientes");
app.UseAuthentication();
app.UseAuthorization();

app.UseDefaultFiles();
app.UseStaticFiles();

// Endpoints informativos (públicos)
app.MapGet("/api", () => Results.Ok(new
{
    servicio = "AdopPlant Analytics API",
    version = "v1",
    panel = "/index.html",
    documentacion = "/swagger",
    salud = "/health",
})).WithTags("General");

app.MapGet("/health", async (DatabaseConnectionFactory db, CancellationToken ct) =>
{
    await using var conexion = db.Crear();
    await conexion.OpenAsync(ct);
    await using var comando = conexion.CreateCommand();
    comando.CommandText = "SELECT 1";
    await comando.ExecuteScalarAsync(ct);

    return Results.Ok(new
    {
        estado = "ok",
        baseDatos = "conectada",
        momento = DateTimeOffset.UtcNow,
    });
}).WithTags("General");

app.MapMetricasEndpoints();
app.MapSesionEndpoints();

app.Run();
