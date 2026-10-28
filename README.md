# AdopPlant Analytics - README

API auxiliar de métricas administrativas para el proyecto AdopPlant. Proyecto .NET 8 (ASP.NET Core MVC/API).

## Requisitos

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

## Ejecutar tests

Los tests unitarios **no dependen** de la API FastAPI ni de PostgreSQL. Se pueden ejecutar desde esta carpeta.

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

### Ejecutar con más detalle

```bash
# Ver resultados detallados
dotnet test tests/AdopPlant.Analytics.Tests/AdopPlant.Analytics.Tests.csproj --logger "console;verbosity=detailed"
```

**Total de tests:** 15 tests.

## Ver reportes

Cada ejecución genera:

- Resultados TRX en `TestResults/<fecha>/`
- Cobertura XML en `TestResults/<fecha>/coverage.cobertura.xml`
- Informe legible HTML en `coverage/index.html` (ReportGenerator)

Abrir `coverage/index.html` en el navegador para ver el reporte de cobertura detallado.

## Ejecutar la API localmente (opcional)

Si necesitas levantar la API para pruebas manuales:

1. Tener Adopplant corriendo en http://127.0.0.1:8000 (ver ../Adoplant/README.md) porque Analytics delega el login.
2. Configurar variables de entorno:
   - ConnectionStrings__AdopPlant (cadena PostgreSQL)
   - Jwt__Secret (mismo secreto que Adoplant)
   - FastApi__BaseUrl (por defecto http://127.0.0.1:8000)
3. Ejecutar:

```bash
dotnet run --project AdopPlant.Analytics.csproj --profile http
```
