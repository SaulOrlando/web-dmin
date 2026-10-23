# AdopPlant Analytics - README

API auxiliar de métricas administrativas para el proyecto AdopPlant. Proyecto .NET 8 (ASP.NET Core MVC/API).

## Requisitos

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) instalado, o
- [Docker](https://www.docker.com/) para ejecutar sin instalar SDK

## Ejecutar tests (SIN levantar Adopplant)

Los tests unitarios **no dependen** de la API FastAPI ni de PostgreSQL. Se pueden ejecutar desde esta carpeta.

### Opción 1: Con .NET SDK local (recomendado)

`ash
# Desde la carpeta AdopPlant.Analytics/
dotnet test tests/AdopPlant.Analytics.Tests/AdopPlant.Analytics.Tests.csproj --no-restore --configuration Debug
`

Si prefieres restaurar paquetes explícitamente:

`ash
dotnet restore AdopPlant.Analytics.csproj
dotnet restore tests/AdopPlant.Analytics.Tests/AdopPlant.Analytics.Tests.csproj
dotnet test tests/AdopPlant.Analytics.Tests/AdopPlant.Analytics.Tests.csproj --configuration Debug
`

### Opción 2: Con Docker (100% portable - Windows/Linux/macOS/GitHub Codespaces)

Desde **esta misma carpeta** (AdopPlant.Analytics/):

#### Linux/macOS/Codespaces (bash/zsh)

`ash
docker run --rm \
  -v "C:\Users\sault\Documents\para mientras":/app \
  -w /app \
  mcr.microsoft.com/dotnet/sdk:8.0 \
  dotnet test tests/AdopPlant.Analytics.Tests/AdopPlant.Analytics.Tests.csproj --no-restore --configuration Debug
`

#### Windows (PowerShell)

`powershell
docker run --rm -v "C:\Users\sault\Documents\para mientras:/app" -w /app mcr.microsoft.com/dotnet/sdk:8.0 dotnet test tests/AdopPlant.Analytics.Tests/AdopPlant.Analytics.Tests.csproj --no-restore --configuration Debug
`

#### Windows (CMD)

`cmd
docker run --rm -v "%CD%:/app" -w /app mcr.microsoft.com/dotnet/sdk:8.0 dotnet test tests/AdopPlant.Analytics.Tests/AdopPlant.Analytics.Tests.csproj --no-restore --configuration Debug
`

Si el repo se clona en otra ruta (o se abre como carpeta única en Codespaces), **no se usa ninguna ruta absoluta** - todo se hace con $PWD/%CD%.

## Ejecutar la API localmente (opcional)

Si necesitas levantar la API para pruebas manuales:

1. Tener Adopplant corriendo en http://127.0.0.1:8000 (ver ../Adoplant/README.md) porque Analytics delega el login.
2. Configurar variables de entorno:
   - ConnectionStrings__AdopPlant (cadena PostgreSQL)
   - Jwt__Secret (mismo secreto que Adoplant)
   - FastApi__BaseUrl (por defecto http://127.0.0.1:8000)
3. Ejecutar:

`ash
dotnet run --project AdopPlant.Analytics.csproj --profile http
`

O con Docker desde la raíz del workspace completo usando ../docker-compose.yml. Ver ../INICIAR_PROYECTOS.md para detalles.

## DevContainer

Existe ./.devcontainer/ con configuración para .NET 8. Abre **esta carpeta** en VS Code con la extensión Dev Containers o úsalo en GitHub Codespaces.
