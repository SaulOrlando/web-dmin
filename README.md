# AdopPlant Analytics

API auxiliar de métricas administrativas para el proyecto AdopPlant. Proyecto .NET 8 (ASP.NET Core MVC/API).

## Requisitos

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

## Ejecutar tests

Los tests unitarios **no dependen** de la API FastAPI ni de PostgreSQL. Todos los comandos se ejecutan desde la carpeta raíz de este proyecto (`AdopPlant.Analytics/`).

```bash
# Desde la carpeta AdopPlant.Analytics/
dotnet test tests/AdopPlant.Analytics.Tests/AdopPlant.Analytics.Tests.csproj --configuration Debug
```

Si prefieres restaurar paquetes explícitamente:

```bash
dotnet restore AdopPlant.Analytics.csproj
dotnet restore tests/AdopPlant.Analytics.Tests/AdopPlant.Analytics.Tests.csproj
dotnet test tests/AdopPlant.Analytics.Tests/AdopPlant.Analytics.Tests.csproj --configuration Debug
```

### SCRUM-102 · Servicios de sesión y configuración JWT

15 pruebas de `JwtSettings` y `SesionService` (archivos `JwtSettingsTests.cs` y
`SesionServiceTests.cs`). No requieren red ni base de datos.

### SCRUM-107 · Endpoints de sesión y métricas

Cubre los casos **C-107-01 a C-107-12** del ticket: login delegado (200 con token y
401 sin sesión), `401` sin token, `403` con usuario no administrador y el ajuste de
`meses` (1–24) y `limite` (1–20) de los seis reportes.

Archivos:

- `tests/AdopPlant.Analytics.Tests/MetricasEndpointsTests.cs`
- `tests/AdopPlant.Analytics.Tests/SesionEndpointsTests.cs`
- `tests/AdopPlant.Analytics.Tests/Infra/FabricaAnalitica.cs`

La API se monta **en memoria** con `WebApplicationFactory<Program>`; el repositorio de
métricas se sustituye por un doble (`IMetricasRepository`) y el login por un
`HttpMessageHandler` falso, así que **no se abre PostgreSQL ni se llama a FastAPI**.
`Program.cs` expone `public partial class Program` para que la fábrica de pruebas
pueda arrancar la aplicación.

```powershell
# Solo la suite de endpoints (SCRUM-107)
dotnet test tests/AdopPlant.Analytics.Tests/AdopPlant.Analytics.Tests.csproj `
  --filter "FullyQualifiedName~MetricasEndpointsTests|FullyQualifiedName~SesionEndpointsTests"
```

**Total de tests:** 32 (15 de SCRUM-102 + 17 de SCRUM-107).

### Ejecutar con más detalle

```bash
# Ver resultados detallados
dotnet test tests/AdopPlant.Analytics.Tests/AdopPlant.Analytics.Tests.csproj --logger "console;verbosity=detailed"
```

## Ver reportes

La forma recomendada es el script del proyecto, que ejecuta toda la suite con cobertura
y genera el informe HTML:

```powershell
cd "C:\Users\sault\Documents\AdopPlant.Analytics"
.\probar.ps1
```

Cada ejecución deja:

- Resultados TRX en `TestResults/<guid>/unitarias.trx`
- Cobertura XML en `TestResults/<guid>/<guid>/coverage.cobertura.xml`
- Informe legible en `coverage/index.html` y resumen en `coverage/Summary.txt`

```powershell
Start-Process coverage/index.html
Get-Content coverage/Summary.txt
```

Para generar solo el TRX de la suite, sin cobertura:

```powershell
dotnet test tests/AdopPlant.Analytics.Tests/AdopPlant.Analytics.Tests.csproj `
  --results-directory TestResults/scrum-107 `
  --logger "trx;LogFileName=unitarias-scrum-107.trx"
```

Resultado de la última ejecución (30-oct-2026): **32 aprobadas, 0 fallidas, 0 omitidas**;
`MetricasEndpoints` y `SesionEndpoints` con **100 % de cobertura de líneas**.

## Pruebas de rendimiento y carga (SCRUM-96)

La prueba de carga de la API C# está en `tests/perf/k6-analytics-load.js`: **50 usuarios
concurrentes durante 2 minutos** contra `GET /api/metricas/resumen` y
`GET /api/metricas/plantas-por-categoria`, con el JWT de administrador.

### Preparar el entorno

1. Adoplant en `http://127.0.0.1:8000` y PostgreSQL con datos sembrados
   (`Adoplant/scripts/seed_pruebas.py`), porque Analytics delega el login y lee la misma base.
2. Analytics en `http://localhost:5080`:

   ```powershell
   $env:ConnectionStrings__AdopPlant = "Host=localhost;Port=5432;Database=plant;Username=postgres;Password=TU_PASSWORD"
   $env:Jwt__Secret = ((Get-Content "..\Adoplant\.env" | Where-Object { $_ -match '^JWT_SECRET_KEY=' }) -replace '^JWT_SECRET_KEY=','').Trim().Trim('"').Trim("'")
   $env:FastApi__BaseUrl = "http://127.0.0.1:8000"
   dotnet run --project AdopPlant.Analytics.csproj --profile http
   ```

3. Comprobar `http://localhost:5080/health` → `estado: ok`, `baseDatos: conectada`.
4. k6 instalado (`k6 version`).

### Ejecutar

```powershell
cd "C:\Users\sault\Documents\AdopPlant.Analytics"
k6 run -e BASE_URL=http://localhost:5080 tests/perf/k6-analytics-load.js   # ~2 min
```

Credenciales por defecto: `admin.panel@adopplant.com` / `Admin2026panel`
(se pueden cambiar con `-e ADMIN_USER=... -e ADMIN_PASSWORD=...`). Si el login del
`setup` falla, el script se detiene: revisar que el seed esté aplicado.

**Criterio de salida:** 200 OK, promedio y p95 < 2000 ms y 0 % de errores. Un
*threshold* incumplido hace que k6 salga con código distinto de cero.

### Ver los reportes

Cada ejecución escribe en `tests/perf/reports/`:

| Archivo | Contenido |
|---|---|
| `scrum-96-analytics-reporte.html` | tabla con promedio, mediana, p95, máximo, tasa y muestras |
| `scrum-96-analytics-resumen.json` | volcado completo de k6 (evidencia para Jira) |

```powershell
Start-Process tests/perf/reports/scrum-96-analytics-reporte.html
```

## Pruebas de aceptación UAT (SCRUM-91)

`tests/uat/verificar-uat.ps1` ejecuta el checklist de aceptación del módulo de
Analytics contra el entorno real y **contrasta cada valor con PostgreSQL**:

| ID | Criterio |
|---|---|
| UAT-01 | El panel carga y rechaza credenciales inválidas |
| UAT-02 | Los 22 totales del resumen coinciden con la base |
| UAT-03 | La evolución mensual trae 12 meses y el mes actual coincide |
| UAT-04 | Plantas por categoría y porcentaje de adopción correctos |
| UAT-05 | Embudo de conversión (publicadas, con solicitud, adoptadas, tasa) |
| UAT-06 | Usuarios destacados (top donantes y adoptantes) |
| UAT-07 | Reportes por estado y usuarios más reportados |
| UAT-08 | Los seis reportes responden 200 a un administrador |

### Ejecutar

Requisitos: Adoplant (:8000), Analytics (:5080), PostgreSQL con datos sembrados y
`psql` instalado.

```powershell
cd "C:\Users\sault\Documents\AdopPlant.Analytics"
powershell -NoProfile -ExecutionPolicy Bypass -File tests/uat/verificar-uat.ps1
```

Parámetros: `-BaseUrl`, `-Usuario`, `-Contrasena`, `-DbHost`, `-DbPort`, `-DbName`,
`-DbUser`, `-DbPassword` y `-Psql` (si `psql` no está en el PATH).

### Ver las evidencias

El script escribe en `tests/uat/evidencias/`:

| Archivo | Contenido |
|---|---|
| `scrum-91-uat-<fecha>.html` | checklist con el veredicto por criterio (adjuntar a Jira) |
| `scrum-91-uat-<fecha>.json` | detalle máquina-legible de cada comprobación |
| `panel-login-<fecha>.png` | captura del panel de Analytics |

El script **termina con código 1** si algún criterio queda observado, y deja
constancia de la firma del Product Owner como *pendiente*: la UAT no se cierra sin
ese visto bueno.

## Ejecutar la API localmente (opcional)

Si necesitas levantar la API para pruebas manuales:

1. Tener AdopPlant corriendo en `http://127.0.0.1:8000` (ver `../Adoplant/README.md`), porque Analytics delega el login.
2. Configurar las variables de entorno:
   - `ConnectionStrings__AdopPlant`: cadena de conexión PostgreSQL
   - `Jwt__Secret`: el mismo secreto que usa AdopPlant
   - `FastApi__BaseUrl`: por defecto `http://127.0.0.1:8000`
3. Ejecutar:

```bash
dotnet run --project AdopPlant.Analytics.csproj --profile http
```
