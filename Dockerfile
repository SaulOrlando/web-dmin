# syntax=docker/dockerfile:1
#
# Imagen de producción de AdopPlant Analytics (ASP.NET Core 8 Web API).
#
# ¿Hace falta una imagen base propia? NO.
# Se usan las imágenes oficiales de .NET: `sdk:8.0` para compilar y `aspnet:8.0`
# para ejecutar. Este servicio no es Python: el backend FastAPI es el repositorio
# Adoplant/, que tiene su propio Dockerfile.
#
# Construcción:  docker build -t adopplant-analytics .
# Ejecución:     docker run --rm -p 5080:8080 \
#                  -e ConnectionStrings__AdopPlant="Host=db;Port=5432;Database=plant;Username=postgres;Password=..." \
#                  -e Jwt__Secret="<64+ caracteres>" \
#                  -e FastApi__BaseUrl="http://api:8000" \
#                  adopplant-analytics

# ---------------------------------------------------------------------------
# Etapa 1: compilación
# ---------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Se copian primero los archivos de proyecto para aprovechar la caché de Docker:
# la restauración de paquetes solo se repite cuando cambia el .csproj.
COPY AdopPlant.Analytics.csproj ./
RUN dotnet restore AdopPlant.Analytics.csproj

COPY . .
RUN dotnet publish AdopPlant.Analytics.csproj \
        --configuration Release \
        --output /app/publish \
        --no-restore

# ---------------------------------------------------------------------------
# Etapa 2: ejecución
# ---------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime

# curl se usa únicamente en el HEALTHCHECK (la imagen oficial no lo incluye).
RUN apt-get update \
 && apt-get install -y --no-install-recommends curl \
 && rm -rf /var/lib/apt/lists/*

ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_EnableDiagnostics=0

WORKDIR /app
COPY --from=build /app/publish .

# Usuario no root que ya existe en las imágenes oficiales de .NET 8 (APP_UID=1654)
USER $APP_UID

EXPOSE 8080

# /health comprueba la API y la conexión a PostgreSQL
HEALTHCHECK --interval=30s --timeout=5s --start-period=20s --retries=3 \
    CMD curl -fsS http://127.0.0.1:8080/health || exit 1

ENTRYPOINT ["dotnet", "AdopPlant.Analytics.dll"]
