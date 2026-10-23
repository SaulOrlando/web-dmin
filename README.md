# AdopPlant Analytics API (.NET)

API auxiliar de **métricas administrativas** para el proyecto **AdopPlant**.
No forma parte del monolito FastAPI: es un servicio de solo lectura que aporta
el panel de analítica que hoy no existe en el backend.

- **Tecnología:** ASP.NET Core 8 Web API (C#), Dapper + Npgsql, Swagger.
- **Base de datos:** la **misma** PostgreSQL de AdopPlant (no crea ni modifica tablas).
- **Autenticación:** valida el **mismo JWT (HS256)** que emite FastAPI y exige que
  el usuario sea **administrador activo** (tabla `public.administrador`).

> Va separada de `Adoplant/` y `PlantHaven/` para no mezclar repositorios ni
> romper la arquitectura monolítica acordada en `Adoplant/docs/guia_tecnica.md`.
> Si el docente pide justificarlo, se presenta como *servicio de reportes* que
> consume la misma base y los mismos tokens.

---

## Requisitos

- [.NET SDK 8.0](https://dotnet.microsoft.com/download/dotnet/8.0) o superior.
- Acceso a la base `adopplant` (PostgreSQL 18) que ya usa el backend Python.
- El valor de `JWT_SECRET_KEY` del archivo `Adoplant/.env`.

## Configuración

1. Copia los valores reales en `appsettings.json` **o**, mejor, usa variables de
   entorno para no subir secretos a Git:

   ```powershell
   $env:ConnectionStrings__AdopPlant = "Host=localhost;Port=5432;Database=adopplant;Username=postgres;Password=TU_PASSWORD"
   $env:Jwt__Secret = "EL_MISMO_JWT_SECRET_KEY_DEL_BACKEND_PYTHON"
   ```

   | Clave de configuración        | Equivale a                       |
   |-------------------------------|----------------------------------|
   | `ConnectionStrings:AdopPlant` | conexión a PostgreSQL            |
   | `Jwt:Secret`                  | `JWT_SECRET_KEY` de FastAPI      |
   | `FastApi:BaseUrl`             | URL de FastAPI (por defecto `http://127.0.0.1:8000`) |
   | `Cors:Origenes`               | URLs de la web/móvil que consumen|

2. `Jwt:Secret` debe tener **64 caracteres o más** y ser idéntico al del backend.

## Ejecutar

```powershell
dotnet restore
dotnet run
```

- Swagger: <http://localhost:5080/swagger>
- Panel admin: <http://localhost:5080/> (login con cuenta de administrador)
- Salud:   <http://localhost:5080/health>
- Info API: <http://localhost:5080/api>

En Swagger pulsa **Authorize** y pega el token que devuelve el login de AdopPlant
(sin la palabra `Bearer`).

> **Panel web incluido:** al abrir <http://localhost:5080/> verás el login del
> panel. Escribe el correo/teléfono y contraseña de un **administrador** y la
> página hace `POST /api/sesion` (que delega en FastAPI) y luego consulta
> `GET /api/metricas/*` con el token. Todo lo que se publique desde la web o
> desde PlantHaven aparece al instante porque las tres apps leen la **misma
> base de datos**. No hay que sincronizar nada.

---

## Endpoints

Todos requieren `Authorization: Bearer <token>` de un administrador.

| Método | Ruta | Descripción |
|--------|------|-------------|
| POST | `/api/sesion` | Login delegado a FastAPI (`{identificador, contrasena}` → `{accessToken}`). Lo usa el panel web. |
| GET | `/api/metricas/resumen` | KPIs: usuarios, plantas, solicitudes, adopciones, reportes y categorías. |
| GET | `/api/metricas/evolucion?meses=12` | Serie mensual: usuarios nuevos, publicaciones, solicitudes y adopciones completadas (1–24 meses). |
| GET | `/api/metricas/plantas-por-categoria` | Ranking de categorías con cantidad publicada, adoptada y % de adopción. |
| GET | `/api/metricas/embudo` | Embudo publicadas → con solicitud → adoptadas y tasa de conversión. |
| GET | `/api/metricas/usuarios?limite=5` | Usuarios con más donaciones y más adopciones completadas. |
| GET | `/api/metricas/reportes?limite=5` | Reportes por estado y usuarios más reportados. |
| GET | `/health` | Estado del servicio y conexión a la base (público). |
| GET | `/` | Panel web de administración (HTML + Chart.js, funciona sin internet). |

### Ejemplo (`resumen`)

```json
{
  "usuarios": { "total": 120, "activos": 110, "bloqueados": 10, "nuevosUltimos30Dias": 15 },
  "plantas":   { "total": 80, "disponibles": 40, "solicitadas": 12, "adoptadas": 20, "ocultas": 8, "eliminadas": 3 },
  "solicitudes": { "total": 95, "pendientes": 20, "aceptadas": 55, "rechazadas": 20 },
  "adopciones": { "total": 40, "enProceso": 20, "completadas": 20 },
  "reportes": { "total": 6, "enRevision": 2, "resueltos": 4 },
  "categorias": { "total": 9, "activas": 8 }
}
```

Consulta `sql/metricas.sql` para ver el SQL exacto de cada endpoint ejecutable en psql.

---

## Cómo encaja en los proyectos

### Web Adopland (panel administrador)

En la vista del panel admin haz `fetch` a la API .NET y pinta los datos (por
ejemplo con Chart.js):

```javascript
const respuesta = await fetch("http://localhost:5080/api/metricas/resumen", {
  headers: { Authorization: `Bearer ${tokenGuardado}` },
});
const datos = await respuesta.json();
```

### Adoplant (FastAPI) y PlantHaven (Flutter)

No requieren cambios: comparten base de datos y el token de sesión. Si quieres
que el móvil solo consuma la .NET, agrega su URL a `Cors:Origenes` y a la
configuración del cliente.

## Cuentas y datos de prueba

La API .NET **no crea datos**: lee la base de AdopPlant. Para que el panel y las
métricas tengan contenido, ejecutar el sembrado del repositorio Python (crea las
cuentas y las plantas de ejemplo):

| Rol | Usuario | Contraseña |
|---|---|---|
| Administrador | `admin.panel@adopplant.com` | `Admin2026panel` |
| Donante | `donante.pruebas@adopplant.com` | `Donante2026!` |
| Adoptante | `adoptante.pruebas@adopplant.com` | `Adoptante2026!` |

```powershell
# Con la pila de contenedores (desde la raíz del workspace)
docker compose run --rm seed

# Contra la base local de Windows
cd "C:\Users\sault\Documents\para mientras\Adoplant"
.\.venv\Scripts\python.exe -m scripts.seed_pruebas
```

> Las cuentas son **solo para desarrollo y QA**.

## Pruebas rápidas

```powershell
# 1) Login delegado: pide el token a FastAPI
$cuerpo = '{"identificador":"admin.panel@adopplant.com","contrasena":"Admin2026panel"}'
$sesion = Invoke-RestMethod -Uri http://localhost:5080/api/sesion -Method Post `
  -ContentType "application/json" -Body $cuerpo
$token = $sesion.accessToken

# 2) Resumen
Invoke-RestMethod -Uri http://localhost:5080/api/metricas/resumen `
  -Headers @{ Authorization = "Bearer $token" }

# 3) Evolución
Invoke-RestMethod -Uri "http://localhost:5080/api/metricas/evolucion?meses=6" `
  -Headers @{ Authorization = "Bearer $token" }
```

## Seguridad

- La API es **de solo lectura**: solo ejecuta `SELECT`.
- Un cliente sin token recibe `401`; un usuario no administrador, `403`.
- No subir `appsettings` con secretos reales: usa variables de entorno.

## Publicación (opcional, Render)

- Build: `dotnet publish -c Release`
- Start: `dotnet AdopPlant.Analytics.dll`
- Variables de entorno en Render: `ConnectionStrings__AdopPlant` y `Jwt__Secret`.

## Contenedores

Este servicio **no** es Python: es ASP.NET Core 8, así que se apoya en las
imágenes oficiales de .NET (`sdk:8.0` para compilar y `aspnet:8.0` para
ejecutar). No hace falta ninguna imagen base propia.

```powershell
# Pila completa (PostgreSQL 18 + API FastAPI + esta API .NET)
cd "C:\Users\sault\Documents\para mientras"
docker compose up -d --build
docker compose ps
docker compose run --rm seed        # datos y cuentas de prueba
```

```powershell
# Desde esta carpeta (el compose.yaml incluye el de la raíz)
cd "C:\Users\sault\Documents\para mientras\AdopPlant.Analytics"
docker compose up -d --build
```

```powershell
# Solo esta API
cd "C:\Users\sault\Documents\para mientras\AdopPlant.Analytics"
docker build -t adopplant-analytics:local .

# Pruebas unitarias dentro de un contenedor (no requieren base de datos)
# --artifacts-path deja bin/ y obj/ DENTRO del contenedor: la carpeta de
# Windows no se ensucia con archivos propiedad de root.
docker run --rm -v "${PWD}:/src" -w /src mcr.microsoft.com/dotnet/sdk:8.0 `
  dotnet test tests/AdopPlant.Analytics.Tests/AdopPlant.Analytics.Tests.csproj `
  -p:RollForward=Major --artifacts-path /tmp/artifacts
```

| Recurso | URL |
|---|---|
| Panel de administración | <http://localhost:5080/> |
| Swagger | <http://localhost:5080/swagger> |
| Health | <http://localhost:5080/health> |
| OpenAPI | <http://localhost:5080/swagger/v1/swagger.json> |

Además hay un **devcontainer** en `.devcontainer/` (VS Code → "Dev Containers:
Reopen in Container") con el SDK de .NET 8, para trabajar sin instalar nada en
la máquina. Trae valores por defecto para `Jwt__Secret` y la conexión (los del
`.env` local); si se cambian las credenciales, exportar `JWT_SECRET_KEY` y
`DB_PASSWORD` antes de abrirlo. Detalle de toda la pila en `..\CONTENEDORES.md`.

> `bin/` y `obj/` del devcontainer viven en **volúmenes con nombre**, no en la
> carpeta de Windows. Docker Desktop declara todo archivo del bind mount como
> `root:root` y MSBuild no puede fijar la fecha de modificación de un archivo
> que no es suyo: `error MSB3374 ... Access to the path ... is denied`.
> Con los volúmenes, la compilación y las pruebas funcionan como `vscode`.

## Pruebas automatizadas

| Suite | Herramienta | Ticket |
|---|---|---|
| Rendimiento (50 VU/2 min sobre `/api/metricas/*`) | k6 | **SCRUM-96** |
| Integración (login delegado + siete endpoints) | Newman | **SCRUM-86** |
| Seguridad de la API | OWASP ZAP | **SCRUM-120** |

```powershell
# Desde la raíz del workspace
docker compose -f docker-compose.yml -f compose.k6-analytics.yaml run --rm k6-analytics
docker compose -f docker-compose.yml -f compose.newman.yaml       run --rm newman
docker compose -f docker-compose.yml -f compose.zap.yaml          run --rm zap-analytics
```

- Guion k6: `tests/perf/k6-analytics-load.js`.
- Colección Newman: `postman/AdopPlant_Analytics.postman_collection.json`.
- Reportes: `..\tests\reports\{k6,newman,zap}`.
- Comandos, criterios y solución de problemas: **`..\tests\README.md`**.

