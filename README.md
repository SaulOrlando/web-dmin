# AdopPlant Analytics

API auxiliar de métricas administrativas para el proyecto AdopPlant. Proyecto .NET 8 (ASP.NET Core MVC/API).

## Requisitos

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) instalado, **o**
- [Docker](https://www.docker.com/) para ejecutar sin instalar el SDK.

## Ejecutar tests (sin levantar AdopPlant)

Los tests unitarios **no dependen** de la API FastAPI ni de PostgreSQL. Todos los comandos se ejecutan desde la carpeta raíz de este proyecto (`AdopPlant.Analytics/`).

### Opción 1: .NET SDK local (recomendado)

```bash
dotnet restore AdopPlant.Analytics.csproj
dotnet restore tests/AdopPlant.Analytics.Tests/AdopPlant.Analytics.Tests.csproj
dotnet test tests/AdopPlant.Analytics.Tests/AdopPlant.Analytics.Tests.csproj --configuration Debug
```

Si ya restauraste los paquetes antes, puedes omitir el restore:

```bash
dotnet test tests/AdopPlant.Analytics.Tests/AdopPlant.Analytics.Tests.csproj --no-restore --configuration Debug
```

### Opción 2: Docker (portable: Windows, Linux, macOS, GitHub Codespaces)

Ejecuta el comando desde la carpeta raíz del proyecto. La carpeta actual se monta en el contenedor, por lo que **no hace falta ninguna ruta absoluta**.

#### Linux / macOS / Codespaces (bash/zsh)

```bash
docker run --rm \
  -v "$PWD":/app \
  -w /app \
  mcr.microsoft.com/dotnet/sdk:8.0 \
  dotnet test tests/AdopPlant.Analytics.Tests/AdopPlant.Analytics.Tests.csproj --configuration Debug
```

#### Windows (PowerShell)

```powershell
docker run --rm `
  -v "${PWD}:/app" `
  -w /app `
  mcr.microsoft.com/dotnet/sdk:8.0 `
  dotnet test tests/AdopPlant.Analytics.Tests/AdopPlant.Analytics.Tests.csproj --configuration Debug
```

#### Windows (CMD)

```cmd
docker run --rm -v "%CD%:/app" -w /app mcr.microsoft.com/dotnet/sdk:8.0 dotnet test tests/AdopPlant.Analytics.Tests/AdopPlant.Analytics.Tests.csproj --configuration Debug
```

> Nota: en Docker no se usa `--no-restore`, porque el contenedor empieza limpio y necesita restaurar los paquetes NuGet.

## Ejecutar la API localmente (opcional)

Si necesitas levantar la API para pruebas manuales:

1. Tener AdopPlant corriendo en `http://127.0.0.1:8000` (ver `../Adoplant/README.md`), porque Analytics delega el login.
2. Configurar las variables de entorno:
   - `ConnectionStrings__AdopPlant`: cadena de conexión PostgreSQL
   - `Jwt__Secret`: el mismo secreto que usa AdopPlant
   - `FastApi__BaseUrl`: por defecto `http://127.0.0.1:8000`
3. Ejecutar:

```bash
dotnet run --project AdopPlant.Analytics.csproj --launch-profile http
```

También puedes levantar todo con Docker desde la raíz del workspace completo usando `../docker-compose.yml`. Ver `../INICIAR_PROYECTOS.md` para más detalles.

## DevContainer

Existe `./.devcontainer/` con la configuración para .NET 8. Abre **esta carpeta** en VS Code con la extensión Dev Containers, o úsala en GitHub Codespaces.
