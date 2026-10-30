# Pruebas de AdopPlant Analytics

Proyecto de pruebas: `AdopPlant.Analytics.Tests/AdopPlant.Analytics.Tests.csproj`.
Herramientas: xUnit, Microsoft.NET.Test.Sdk, Coverlet, ReportGenerator,
`Microsoft.AspNetCore.Mvc.Testing` (SCRUM-107) y k6 (SCRUM-96).

Todo lo de esta carpeta se ejecuta desde la raíz del repositorio
(`AdopPlant.Analytics/`), que es donde viven `probar.ps1` y `coverage.runsettings`.

| Suite | Ticket | Comando | Artefactos |
|---|---|---|---|
| Unitarias + cobertura | SCRUM-102 / SCRUM-107 | `.\probar.ps1` | `TestResults/<guid>/`, `coverage/index.html` |
| Solo endpoints | SCRUM-107 | `dotnet test ... --filter ...` | TRX en `TestResults/` |
| Carga 50 VU / 2 min | SCRUM-96 | `k6 run ... tests/perf/k6-analytics-load.js` | `tests/perf/reports/` |
| Checklist UAT | SCRUM-91 | `tests/uat/verificar-uat.ps1` | `tests/uat/evidencias/` |

## 1. Preparar el entorno

```powershell
cd "C:\Users\sault\Documents\AdopPlant.Analytics"
dotnet restore AdopPlant.Analytics.csproj
dotnet restore tests/AdopPlant.Analytics.Tests/AdopPlant.Analytics.Tests.csproj
```

Requiere SDK .NET 8 o superior y acceso a NuGet para restaurar paquetes. El comando
permite usar un runtime superior si no está instalado el runtime web de .NET 8: en este
equipo se verificó con SDK/runtime 10 manteniendo `net8.0` como destino. Conviene usar el
runtime 8 en los equipos que ya lo tengan instalado.

Las **pruebas unitarias y de endpoints no necesitan** PostgreSQL, Python, Render, Mailjet,
claves ni cuentas reales. El HTTP simulado nunca envía credenciales ficticias a la red.

Las pruebas de **k6** y la **UAT** sí necesitan el stack completo (ver §3 y §4).

## 2. Pruebas unitarias y de endpoints (SCRUM-102 y SCRUM-107)

### Ejecutar todo con cobertura

```powershell
.\probar.ps1
```

El script ejecuta la suite completa, colecta cobertura con `coverage.runsettings`,
restaura `dotnet-reportgenerator-globaltool` y regenera el informe HTML.

También sirve el comando directo:

```bash
dotnet test tests/AdopPlant.Analytics.Tests/AdopPlant.Analytics.Tests.csproj --configuration Debug
```

### Ejecutar solo los endpoints (SCRUM-107)

```powershell
dotnet test tests/AdopPlant.Analytics.Tests/AdopPlant.Analytics.Tests.csproj `
  --filter "FullyQualifiedName~MetricasEndpointsTests|FullyQualifiedName~SesionEndpointsTests"
```

### Qué cubre cada archivo

- `JwtSettingsTests.cs` — `JwtSettings`: claves ausentes, cortas y placeholders;
  longitud permitida; el error no expone la clave. (SCRUM-102)
- `SesionServiceTests.cs` — `SesionService`: destino, método y cuerpo del login
  delegado; token recibido; respuestas 401/403/500; error de red; JSON `null`. (SCRUM-102)
- `MetricasEndpointsTests.cs` — casos **C-107-03 a C-107-12**: `401` sin token,
  `403` con usuario no administrador, `meses` acotado a 1–24, `limite` acotado a 1–20,
  los seis reportes con sus DTO y `404` en ruta desconocida. (SCRUM-107)
- `SesionEndpointsTests.cs` — casos **C-107-01 y C-107-02**: login válido → `200` con
  token; credenciales inválidas → `401`; el login nunca devuelve `500`. (SCRUM-107)
- `Infra/FabricaAnalitica.cs` — `WebApplicationFactory<Program>` que monta la API **en
  memoria**, sustituye `IMetricasRepository` por un doble, reemplaza el JWT de Adoplant
  por un esquema de cabeceras (`X-Prueba-Usuario`, `X-Prueba-Rol`) y el handler de
  autorización por uno que no consulta la base.

Para poder montar la API en memoria, `Program.cs` termina con
`public partial class Program { }` y los endpoints de métricas dependen de la
interfaz `Data/IMetricasRepository.cs`. **No cambia el comportamiento funcional** de la
API ni el esquema de datos: solo se añadió la abstracción que el ticket daba por
precondición.

### Resultado de la última ejecución (30-oct-2026)

- **32 aprobadas, 0 fallidas, 0 omitidas** (15 de SCRUM-102 + 17 de SCRUM-107).
- Cobertura de líneas de toda la API: **60,8 %** (246/404).
- `Endpoints.MetricasEndpoints`: **100 %**.
- `Endpoints.SesionEndpoints`: **100 %**.
- `Data.MetricasRepository` y `Data.DatabaseConnectionFactory`: 0 % (se sustituyen por
  dobles, por diseño).

### Ver los reportes

| Qué | Dónde |
|---|---|
| Informe HTML de cobertura | `coverage/index.html` |
| Resumen en texto | `coverage/Summary.txt` |
| Resultados TRX | `TestResults/<guid>/unitarias.trx` |
| Cobertura Cobertura XML | `TestResults/<guid>/<guid>/coverage.cobertura.xml` |

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

Los resultados no se versionan: guardar la evidencia de la ejecución elegida (TRX +
`coverage/Summary.txt` o el HTML) junto con el commit evaluado.

## 3. Prueba de carga con k6 (SCRUM-96)

`perf/k6-analytics-load.js` ejecuta **50 usuarios concurrentes durante 2 minutos** contra
`GET /api/metricas/resumen` y `GET /api/metricas/plantas-por-categoria` con el JWT de
administrador. Criterio de salida: **200 OK, promedio y p95 < 2000 ms y 0 % de errores**.

### Requisitos

1. Adoplant en `http://127.0.0.1:8000` y PostgreSQL con datos sembrados:
   `Adoplant` → `python -m scripts.seed_pruebas` (crea `admin.panel@adopplant.com` /
   `Admin2026panel`).
2. Analytics en `http://localhost:5080` con la misma base y el mismo secreto JWT:

   ```powershell
   $env:ConnectionStrings__AdopPlant = "Host=localhost;Port=5432;Database=plant;Username=postgres;Password=TU_PASSWORD"
   $env:Jwt__Secret = ((Get-Content "..\Adoplant\.env" | Where-Object { $_ -match '^JWT_SECRET_KEY=' }) -replace '^JWT_SECRET_KEY=','').Trim().Trim('"').Trim("'")
   $env:FastApi__BaseUrl = "http://127.0.0.1:8000"
   dotnet run --project AdopPlant.Analytics.csproj --profile http
   ```

   Si `dotnet run` está bloqueado por una política de Control de aplicaciones (error
   Win32 4551), arrancar el binario ya compilado:
   `dotnet bin\Debug\net8.0\AdopPlant.Analytics.dll --urls http://localhost:5080`.

3. `http://localhost:5080/health` → `{"estado":"ok","baseDatos":"conectada"}`.
4. k6 instalado (`k6 version`).

### Ejecutar

```powershell
cd "C:\Users\sault\Documents\AdopPlant.Analytics"
k6 run -e BASE_URL=http://localhost:5080 tests/perf/k6-analytics-load.js   # ~2 min
```

Credenciales por defecto: `admin.panel@adopplant.com` / `Admin2026panel`; se cambian con
`-e ADMIN_USER=... -e ADMIN_PASSWORD=...`. Si el login del `setup` falla, el script se
detiene: revisar el seed. Un *threshold* incumplido hace que k6 salga con código distinto
de cero, es decir, la ejecución **no** se aprueba.

### Ver los reportes

Cada ejecución escribe en `perf/reports/` (relativo a la carpeta desde donde se lanza k6):

| Archivo | Contenido |
|---|---|
| `scrum-96-analytics-reporte.html` | tabla con promedio, mediana, p95, máximo, tasa y muestras |
| `scrum-96-analytics-resumen.json` | volcado completo de k6 (evidencia cruda para Jira) |

```powershell
Start-Process tests/perf/reports/scrum-96-analytics-reporte.html
```

El resumen corto se imprime además en la consola. `-e REPORTES_DIR=otra/carpeta` cambia el
destino (la carpeta debe existir).

**Última ejecución (30-oct-2026):** 0 % de errores, `checks` 46078/46078; `resumen`
promedio 13,23 ms / p95 27,11 ms; `plantas-por-categoria` promedio 6,39 ms / p95 15,92 ms.

## 4. Checklist UAT (SCRUM-91)

`uat/verificar-uat.ps1` ejecuta el checklist de aceptación del módulo de Analytics contra
el entorno real y **contrasta cada valor con PostgreSQL** (no hay valores escritos a mano).

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

Requisitos: Adoplant (:8000), Analytics (:5080), PostgreSQL con datos sembrados y `psql`
instalado (se detecta en `Program Files\PostgreSQL\*\bin` o en el `PATH`).

```powershell
cd "C:\Users\sault\Documents\AdopPlant.Analytics"
powershell -NoProfile -ExecutionPolicy Bypass -File tests/uat/verificar-uat.ps1
```

Parámetros: `-BaseUrl`, `-Usuario`, `-Contrasena`, `-DbHost`, `-DbPort`, `-DbName`,
`-DbUser`, `-DbPassword` y `-Psql` (si `psql` no está en el `PATH`).

### Ver las evidencias

El script escribe en `uat/evidencias/`:

| Archivo | Contenido |
|---|---|
| `scrum-91-uat-<fecha>.html` | checklist con el veredicto por criterio (adjuntar a Jira) |
| `scrum-91-uat-<fecha>.json` | detalle máquina-legible de cada comprobación |
| `panel-login-<fecha>.png` | captura del panel de Analytics (evidencia visual) |

**Última ejecución (30-oct-2026):** **ACEPTADO (8/8 criterios)**. El JSON deja constancia
de que la **firma del Product Owner está pendiente**: la UAT no se cierra sin ese visto
bueno. El script sale con código 1 si algún criterio queda observado.

## 5. Trazabilidad

| Ticket | Tipo | Alcance en esta carpeta |
|---|---|---|
| [SCRUM-102](https://jdavidg555-1788882610120.atlassian.net/browse/SCRUM-102) | TestCase | 15 unitarias de `JwtSettings` y `SesionService` |
| [SCRUM-107](https://jdavidg555-1788882610120.atlassian.net/browse/SCRUM-107) | TestCase | 17 pruebas de los endpoints de sesión y métricas |
| [SCRUM-96](https://jdavidg555-1788882610120.atlassian.net/browse/SCRUM-96) | TestScript | Carga 50 VU / 2 min sobre `/api/metricas/*` |
| [SCRUM-91](https://jdavidg555-1788882610120.atlassian.net/browse/SCRUM-91) | TestSession (UAT) | Checklist de aceptación de Analytics |

Los cuatro pertenecen a la épica **SCRUM-88** (Unitarias/Integración) o al Epic UAT
**SCRUM-98**, según el plan de pruebas.

SCRUM-86 conserva su alcance de integración con servicios y base real; SCRUM-87
corresponde a unitarias de Python. Este repositorio no completa esos casos ni marca
automáticamente ejecuciones como PASS en AgileTest.

Referencias: <https://learn.microsoft.com/dotnet/core/testing/unit-testing-code-coverage>
· <https://grafana.com/docs/k6/latest/>
