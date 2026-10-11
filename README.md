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

## Pruebas rápidas

```powershell
# 1) Token de administrador (desde el login de la API FastAPI)
$token = "<JWT_DEL_ADMIN>"

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
