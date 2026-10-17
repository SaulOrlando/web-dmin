# Pruebas unitarias .NET — primer avance

Proyecto: `AdopPlant.Analytics.Tests/AdopPlant.Analytics.Tests.csproj`.
Herramientas: xUnit, Microsoft.NET.Test.Sdk, Coverlet y ReportGenerator.

Desde la raiz del repositorio, ejecutar en PowerShell:

```powershell
.\probar.ps1
```

Requiere SDK .NET 8 o superior y acceso a NuGet para restaurar paquetes.
El comando permite utilizar un runtime superior si no esta instalado el runtime
web de .NET 8. En esta PC se verifico con SDK/runtime 10, manteniendo net8.0 como
destino. Conviene usar el runtime 8 en los equipos que ya lo tengan instalado.
No necesita PostgreSQL, Python, Render, Mailjet, claves ni cuentas reales.
El HTTP simulado nunca envia las credenciales ficticias a la red.

Cada ejecucion genera resultados TRX y Cobertura XML en una carpeta distinta de
`TestResults`. El informe legible de la ultima ejecucion esta en `coverage/index.html`.
Estos resultados no se versionan; guardar las evidencias de la ejecucion elegida
para Classroom junto con el commit que se evaluo.

## Alcance inicial

- JwtSettings: rechaza claves ausentes, cortas y placeholders; acepta longitud
  permitida y comprueba que el error no exponga la clave.
- SesionService: valida destino, metodo y cuerpo del login delegado; comprueba
  token recibido, respuestas 401/403/500, error de red y respuesta JSON null.
- No se modifica el comportamiento funcional de la API ni el esquema de datos.

Resultado inicial del 16-10-2026: 15 aprobadas, 0 fallidas, 0 omitidas.
Cobertura de toda la API: 38/404 lineas (9,4 %).
El resumen XML de Coverlet indica 10/68 ramas (14,7 %); ReportGenerator muestra
10/54 (18,5 %) al agregar los datos por clase. No mezclar estos denominadores:
para el PDF usar de forma consistente las metricas del informe HTML adjunto.
No se excluyeron clases de negocio para aumentar el porcentaje. Tener cobertura
total en las dos clases probadas no demuestra que todos sus posibles casos esten
resueltos. Faltan permisos, consultas de metricas, endpoints y otros casos limite.
La suite tampoco reemplaza las pruebas de integracion o E2E.

## Trazabilidad

Esta suite corresponde a [SCRUM-102 — Pruebas unitarias y cobertura de la API .NET](https://jdavidg555-1788882610120.atlassian.net/browse/SCRUM-102),
dentro de la epica SCRUM-88 y asignada a David Galicia.
SCRUM-86 conserva su alcance de integracion con servicios y base real;
SCRUM-87 corresponde a unitarias de Python. Este avance no completa esos casos
ni marca automaticamente ejecuciones como PASS en AgileTest.

Referencia de configuracion: https://learn.microsoft.com/dotnet/core/testing/unit-testing-code-coverage
