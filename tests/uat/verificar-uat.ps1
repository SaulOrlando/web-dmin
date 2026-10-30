# SCRUM-91 - UAT del modulo de Analytics.
#
# Comprueba, contra la base PostgreSQL real, que los seis reportes del panel de
# Analytics devuelven exactamente los mismos valores y que el panel carga.
# Genera un checklist (JSON + HTML) en tests/uat/evidencias/ para adjuntar a Jira.
#
# Requisitos:
#   - Adoplant (:8000), Analytics (:5080) y PostgreSQL en ejecucion.
#   - Datos sembrados: python -m scripts.seed_pruebas (en Adoplant).
#   - psql disponible (se detecta en Program Files o en el PATH).
#
# Uso, desde la carpeta AdopPlant.Analytics/:
#   powershell -NoProfile -ExecutionPolicy Bypass -File tests/uat/verificar-uat.ps1
#
# Parametros utiles:
#   -BaseUrl http://localhost:5080
#   -Usuario admin.panel@adopplant.com -Contrasena Admin2026panel
#   -DbPassword javarberl
param(
    [string]$BaseUrl = 'http://localhost:5080',
    [string]$Usuario = 'admin.panel@adopplant.com',
    [string]$Contrasena = 'Admin2026panel',
    [string]$DbHost = 'localhost',
    [int]$DbPort = 5432,
    [string]$DbName = 'plant',
    [string]$DbUser = 'postgres',
    [string]$DbPassword = 'javarberl',
    [string]$Psql = ''
)

$ErrorActionPreference = 'Stop'
$BaseUrl = $BaseUrl.TrimEnd('/')
$cultura = [Globalization.CultureInfo]::InvariantCulture

function Buscar-Psql {
    if ($Psql -and (Test-Path $Psql)) { return $Psql }
    $enPath = Get-Command psql -ErrorAction SilentlyContinue
    if ($enPath) { return $enPath.Source }
    $candidatos = Get-ChildItem 'C:\Program Files\PostgreSQL\*\bin\psql.exe' -ErrorAction SilentlyContinue |
        Sort-Object FullName -Descending
    if ($candidatos) { return $candidatos[0].FullName }
    throw 'No se encontro psql. Instala el cliente de PostgreSQL o pasa -Psql <ruta>.'
}

$psqlExe = Buscar-Psql
$env:PGPASSWORD = $DbPassword

function Sql([string]$consulta) {
    $salida = & $psqlExe -h $DbHost -p $DbPort -U $DbUser -d $DbName -t -A -F '|' -v ON_ERROR_STOP=1 -c $consulta
    if ($LASTEXITCODE -ne 0) { throw "psql fallo al ejecutar: $consulta" }
    $lineas = @($salida | Where-Object { $_ -ne '' })
    # La coma evita que PowerShell desenrolle un resultado de una sola linea
    # (asi los llamadores siempre pueden usar [0], .Count o foreach).
    return , $lineas
}

function Entero($valor) { return [int][double]::Parse([string]$valor, $cultura) }

function Decimal($valor) { return [double]::Parse([string]$valor, $cultura) }

function Comprobar([string]$nombre, $esperado, $obtenido, [double]$tolerancia = 0.0) {
    $ok = $false
    if ($esperado -is [string] -or $obtenido -is [string]) {
        $ok = [string]$esperado -eq [string]$obtenido
    }
    elseif ($esperado -is [double] -or $obtenido -is [double]) {
        $ok = [Math]::Abs(([double]$esperado) - ([double]$obtenido)) -le $tolerancia
    }
    else {
        $ok = [double]$esperado -eq [double]$obtenido
    }

    return [pscustomobject]@{
        Comprobacion = $nombre
        Esperado     = $esperado
        Obtenido     = $obtenido
        Ok           = $ok
    }
}

function Detalle($comprobaciones) {
    return @($comprobaciones | ForEach-Object {
            [pscustomobject]@{
                comprobacion = $_.Comprobacion
                esperado     = $_.Esperado
                obtenido     = $_.Obtenido
                resultado    = if ($_.Ok) { 'PASS' } else { 'FALLO' }
            }
        })
}

$resultados = [System.Collections.Generic.List[object]]::new()

function Registrar([string]$id, [string]$criterio, $comprobaciones) {
    $detalle = Detalle $comprobaciones
    $fallos = @($detalle | Where-Object { $_.resultado -eq 'FALLO' }).Count
    $resultados.Add([pscustomobject]@{
            id        = $id
            criterio  = $criterio
            resultado = if ($fallos -eq 0) { 'ACEPTADO' } else { 'OBSERVADO' }
            fallos    = $fallos
            detalle   = $detalle
        })
}

Write-Host "SCRUM-91 · UAT de Analytics contra $BaseUrl" -ForegroundColor Cyan
Write-Host "Base de datos: ${DbHost}:${DbPort}/${DbName}" -ForegroundColor DarkGray

# ---------------------------------------------------------------------------
# UAT-01 · El panel carga y exige autenticacion
# ---------------------------------------------------------------------------
$panel = Invoke-WebRequest -Uri "$BaseUrl/" -UseBasicParsing -TimeoutSec 30
$loginInvalido = $null
try {
    Invoke-RestMethod -Method Post -Uri "$BaseUrl/api/sesion" -TimeoutSec 30 `
        -ContentType 'application/json' `
        -Body (@{ identificador = $Usuario; contrasena = 'incorrecta' } | ConvertTo-Json) | Out-Null
    $loginInvalido = 200
}
catch {
    $loginInvalido = [int]$_.Exception.Response.StatusCode
}

Registrar 'UAT-01' 'El panel carga y rechaza credenciales invalidas' @(
    (Comprobar 'El panel responde 200' 200 $panel.StatusCode),
    (Comprobar 'El panel sirve el HTML del panel' 'si' $(if ($panel.Content -match 'AdopPlant|Analytics') { 'si' } else { 'no' })),
    (Comprobar 'Login con contrasena incorrecta devuelve 401' 401 $loginInvalido)
)

$sesion = Invoke-RestMethod -Method Post -Uri "$BaseUrl/api/sesion" -TimeoutSec 30 `
    -ContentType 'application/json' `
    -Body (@{ identificador = $Usuario; contrasena = $Contrasena } | ConvertTo-Json)

$token = $sesion.accessToken
if (-not $token) { throw 'El login de administrador no devolvio accessToken.' }
$cabeceras = @{ Authorization = "Bearer $token" }

function ApiGet([string]$ruta) {
    return Invoke-RestMethod -Uri "$BaseUrl$ruta" -Headers $cabeceras -TimeoutSec 30
}

# ---------------------------------------------------------------------------
# UAT-02 · Resumen general: los 22 totales coinciden con la base
# ---------------------------------------------------------------------------
$resumen = ApiGet '/api/metricas/resumen'
$fila = (Sql @'
SELECT (SELECT count(*) FROM public.usuario),
       (SELECT count(*) FROM public.usuario WHERE estado = 'ACTIVO'),
       (SELECT count(*) FROM public.usuario WHERE estado = 'BLOQUEADO'),
       (SELECT count(*) FROM public.usuario WHERE fecha_registro >= now() - interval '30 days'),
       (SELECT count(*) FROM public.planta),
       (SELECT count(*) FROM public.planta WHERE NOT eliminada AND visible AND estado = 'DISPONIBLE'),
       (SELECT count(*) FROM public.planta WHERE NOT eliminada AND visible AND estado = 'SOLICITADA'),
       (SELECT count(*) FROM public.planta WHERE NOT eliminada AND estado = 'ADOPTADA'),
       (SELECT count(*) FROM public.planta WHERE NOT visible AND NOT eliminada),
       (SELECT count(*) FROM public.planta WHERE eliminada),
       (SELECT count(*) FROM public.solicitud_adopcion),
       (SELECT count(*) FROM public.solicitud_adopcion WHERE estado = 'PENDIENTE'),
       (SELECT count(*) FROM public.solicitud_adopcion WHERE estado = 'ACEPTADA'),
       (SELECT count(*) FROM public.solicitud_adopcion WHERE estado = 'RECHAZADA'),
       (SELECT count(*) FROM public.adopcion),
       (SELECT count(*) FROM public.adopcion WHERE estado = 'EN_PROCESO'),
       (SELECT count(*) FROM public.adopcion WHERE estado = 'COMPLETADA'),
       (SELECT count(*) FROM public.reporte),
       (SELECT count(*) FROM public.reporte WHERE estado = 'EN_REVISION'),
       (SELECT count(*) FROM public.reporte WHERE estado = 'RESUELTO'),
       (SELECT count(*) FROM public.categoria),
       (SELECT count(*) FROM public.categoria WHERE estado = 'ACTIVA')
'@)[0].Split('|')

$comparaciones = @(
    (Comprobar 'Usuarios total' (Entero $fila[0]) $resumen.usuarios.total),
    (Comprobar 'Usuarios activos' (Entero $fila[1]) $resumen.usuarios.activos),
    (Comprobar 'Usuarios bloqueados' (Entero $fila[2]) $resumen.usuarios.bloqueados),
    (Comprobar 'Usuarios nuevos 30 dias' (Entero $fila[3]) $resumen.usuarios.nuevosUltimos30Dias),
    (Comprobar 'Plantas total' (Entero $fila[4]) $resumen.plantas.total),
    (Comprobar 'Plantas disponibles' (Entero $fila[5]) $resumen.plantas.disponibles),
    (Comprobar 'Plantas solicitadas' (Entero $fila[6]) $resumen.plantas.solicitadas),
    (Comprobar 'Plantas adoptadas' (Entero $fila[7]) $resumen.plantas.adoptadas),
    (Comprobar 'Plantas ocultas' (Entero $fila[8]) $resumen.plantas.ocultas),
    (Comprobar 'Plantas eliminadas' (Entero $fila[9]) $resumen.plantas.eliminadas),
    (Comprobar 'Solicitudes total' (Entero $fila[10]) $resumen.solicitudes.total),
    (Comprobar 'Solicitudes pendientes' (Entero $fila[11]) $resumen.solicitudes.pendientes),
    (Comprobar 'Solicitudes aceptadas' (Entero $fila[12]) $resumen.solicitudes.aceptadas),
    (Comprobar 'Solicitudes rechazadas' (Entero $fila[13]) $resumen.solicitudes.rechazadas),
    (Comprobar 'Adopciones total' (Entero $fila[14]) $resumen.adopciones.total),
    (Comprobar 'Adopciones en proceso' (Entero $fila[15]) $resumen.adopciones.enProceso),
    (Comprobar 'Adopciones completadas' (Entero $fila[16]) $resumen.adopciones.completadas),
    (Comprobar 'Reportes total' (Entero $fila[17]) $resumen.reportes.total),
    (Comprobar 'Reportes en revision' (Entero $fila[18]) $resumen.reportes.enRevision),
    (Comprobar 'Reportes resueltos' (Entero $fila[19]) $resumen.reportes.resueltos),
    (Comprobar 'Categorias total' (Entero $fila[20]) $resumen.categorias.total),
    (Comprobar 'Categorias activas' (Entero $fila[21]) $resumen.categorias.activas)
)
Registrar 'UAT-02' 'El resumen general coincide con la base de datos' $comparaciones

# ---------------------------------------------------------------------------
# UAT-03 · Evolucion mensual: 12 puntos y el mes actual coincide
# ---------------------------------------------------------------------------
$evolucion = ApiGet '/api/metricas/evolucion'
$periodoActual = (Get-Date).ToString('yyyy-MM')
$puntoActual = $evolucion | Where-Object { $_.periodo -eq $periodoActual } | Select-Object -First 1
$publicadasMes = Entero (Sql "SELECT count(*) FROM public.planta WHERE date_trunc('month', fecha_publicacion) = date_trunc('month', now())")[0]
$solicitudesMes = Entero (Sql "SELECT count(*) FROM public.solicitud_adopcion WHERE date_trunc('month', fecha_solicitud) = date_trunc('month', now())")[0]

Registrar 'UAT-03' 'La evolucion mensual trae 12 meses y el mes actual coincide' @(
    (Comprobar 'Puntos devueltos' 12 @($evolucion).Count),
    (Comprobar 'Existe el periodo actual' 'si' $(if ($puntoActual) { 'si' } else { 'no' })),
    (Comprobar 'Publicaciones del mes actual' $publicadasMes $puntoActual.publicaciones),
    (Comprobar 'Solicitudes del mes actual' $solicitudesMes $puntoActual.solicitudes)
)

# ---------------------------------------------------------------------------
# UAT-04 · Plantas por categoria con porcentaje de adopcion
# ---------------------------------------------------------------------------
$categorias = ApiGet '/api/metricas/plantas-por-categoria'
$filasCategorias = Sql @'
SELECT c.nombre || '|' ||
       count(p.id_planta) || '|' ||
       count(p.id_planta) FILTER (WHERE p.estado = 'ADOPTADA')
FROM public.categoria AS c
LEFT JOIN public.planta AS p
       ON p.id_categoria = c.id_categoria AND NOT p.eliminada
GROUP BY c.id_categoria, c.nombre
ORDER BY count(p.id_planta) DESC, c.nombre
'@

$comparaciones = @((Comprobar 'Categorias devueltas' $filasCategorias.Count $categorias.Count))
for ($i = 0; $i -lt [Math]::Min($filasCategorias.Count, $categorias.Count); $i++) {
    $esperado = $filasCategorias[$i].Split('|')
    $obtenido = $categorias[$i]
    $porcentaje = if ((Entero $esperado[1]) -eq 0) { 0.0 } else {
        [Math]::Round((Entero $esperado[2]) * 100.0 / (Entero $esperado[1]), 2)
    }
    $comparaciones += Comprobar "Categoria $($esperado[0]) publicadas" (Entero $esperado[1]) $obtenido.publicadas
    $comparaciones += Comprobar "Categoria $($esperado[0]) adoptadas" (Entero $esperado[2]) $obtenido.adoptadas
    $comparaciones += Comprobar "Categoria $($esperado[0]) porcentaje" $porcentaje $obtenido.porcentajeAdopcion 0.01
}
Registrar 'UAT-04' 'Plantas por categoria y porcentaje de adopcion correctos' $comparaciones

# ---------------------------------------------------------------------------
# UAT-05 · Embudo de conversion
# ---------------------------------------------------------------------------
$embudo = ApiGet '/api/metricas/embudo'
$filaEmbudo = (Sql @'
WITH base AS (
    SELECT p.id_planta,
           p.estado::text AS estado,
           EXISTS (SELECT 1 FROM public.solicitud_adopcion AS s WHERE s.id_planta = p.id_planta) AS tiene_solicitud
    FROM public.planta AS p
    WHERE NOT p.eliminada
)
SELECT count(*), count(*) FILTER (WHERE tiene_solicitud), count(*) FILTER (WHERE estado = 'ADOPTADA')
FROM base
'@)[0].Split('|')
$publicadasEmbudo = Entero $filaEmbudo[0]
$tasaEsperada = if ($publicadasEmbudo -eq 0) { 0.0 } else {
    [Math]::Round((Entero $filaEmbudo[2]) * 100.0 / $publicadasEmbudo, 2)
}

Registrar 'UAT-05' 'El embudo de conversion coincide con la base' @(
    (Comprobar 'Publicadas' $publicadasEmbudo $embudo.publicadas),
    (Comprobar 'Con solicitud' (Entero $filaEmbudo[1]) $embudo.conSolicitud),
    (Comprobar 'Adoptadas' (Entero $filaEmbudo[2]) $embudo.adoptadas),
    (Comprobar 'Tasa de conversion' $tasaEsperada $embudo.tasaConversion 0.01)
)

# ---------------------------------------------------------------------------
# UAT-06 · Usuarios destacados (top donantes y adoptantes)
# ---------------------------------------------------------------------------
$usuarios = ApiGet '/api/metricas/usuarios?limite=5'
$topDonantes = Sql @'
SELECT count(*) FROM (
    SELECT u.id_usuario
    FROM public.adopcion AS a
    JOIN public.usuario AS u ON u.id_usuario = a.id_donante
    WHERE a.estado = 'COMPLETADA'
    GROUP BY u.id_usuario, u.nombre, u.apellido
    LIMIT 5
) AS t
'@
$topAdoptantes = Sql @'
SELECT count(*) FROM (
    SELECT u.id_usuario
    FROM public.adopcion AS a
    JOIN public.usuario AS u ON u.id_usuario = a.id_adoptante
    WHERE a.estado = 'COMPLETADA'
    GROUP BY u.id_usuario, u.nombre, u.apellido
    LIMIT 5
) AS t
'@

Registrar 'UAT-06' 'Los usuarios destacados respetan el limite y la base' @(
    (Comprobar 'Top donantes devueltos' (Entero $topDonantes[0]) @($usuarios.topDonantes).Count),
    (Comprobar 'Top adoptantes devueltos' (Entero $topAdoptantes[0]) @($usuarios.topAdoptantes).Count)
)

# ---------------------------------------------------------------------------
# UAT-07 · Reportes por estado y usuarios mas reportados
# ---------------------------------------------------------------------------
$reportes = ApiGet '/api/metricas/reportes'
$porEstado = Sql @'
SELECT estado::text || '|' || count(*)
FROM public.reporte
GROUP BY estado
ORDER BY count(*) DESC
'@
$masReportados = Entero (Sql @'
SELECT count(*) FROM (
    SELECT u.id_usuario
    FROM public.reporte AS r
    JOIN public.usuario AS u ON u.id_usuario = r.id_reportado
    GROUP BY u.id_usuario, u.nombre, u.apellido
    LIMIT 5
) AS t
'@)[0]

$esperadoPorEstado = @{}
foreach ($linea in $porEstado) {
    $partes = $linea.Split('|')
    $esperadoPorEstado[$partes[0]] = Entero $partes[1]
}
$obtenidoPorEstado = @{}
foreach ($item in @($reportes.porEstado)) { $obtenidoPorEstado[$item.estado] = [int]$item.total }

$estadosOk = $esperadoPorEstado.Count -eq $obtenidoPorEstado.Count
foreach ($clave in $esperadoPorEstado.Keys) {
    if (-not $obtenidoPorEstado.ContainsKey($clave) -or $obtenidoPorEstado[$clave] -ne $esperadoPorEstado[$clave]) {
        $estadosOk = $false
    }
}

Registrar 'UAT-07' 'Los reportes por estado y mas reportados coinciden' @(
    (Comprobar 'Estados devueltos' $esperadoPorEstado.Count $obtenidoPorEstado.Count),
    (Comprobar 'Totales por estado correctos' 'si' $(if ($estadosOk) { 'si' } else { 'no' })),
    (Comprobar 'Usuarios mas reportados' $masReportados @($reportes.masReportados).Count)
)

# ---------------------------------------------------------------------------
# UAT-08 · Los seis reportes responden y no hay errores del servidor
# ---------------------------------------------------------------------------
$rutas = '/resumen', '/evolucion', '/plantas-por-categoria', '/embudo', '/usuarios', '/reportes'
$comparaciones = foreach ($ruta in $rutas) {
    $respuesta = Invoke-WebRequest -Uri "$BaseUrl/api/metricas$ruta" -Headers $cabeceras -UseBasicParsing -TimeoutSec 30
    Comprobar "GET /api/metricas$ruta" 200 $respuesta.StatusCode
}
Registrar 'UAT-08' 'Los seis reportes responden 200 a un administrador' @($comparaciones)

# ---------------------------------------------------------------------------
# Evidencias
# ---------------------------------------------------------------------------
$total = $resultados.Count
$aceptados = @($resultados | Where-Object { $_.resultado -eq 'ACEPTADO' }).Count
$veredicto = if ($aceptados -eq $total) { 'ACEPTADO' } else { 'OBSERVADO' }

$evidencias = Join-Path $PSScriptRoot 'evidencias'
New-Item -ItemType Directory -Force -Path $evidencias | Out-Null
$sello = Get-Date -Format 'yyyyMMdd-HHmmss'

$resumenEjecucion = [pscustomobject]@{
    ticket       = 'SCRUM-91'
    tipo         = 'TestSession (UAT)'
    fecha        = (Get-Date).ToString('s')
    entorno      = $BaseUrl
    baseDatos    = "${DbHost}:${DbPort}/${DbName}"
    usuario      = $Usuario
    criterios    = $total
    aceptados    = $aceptados
    veredicto    = $veredicto
    firmaPO      = 'Pendiente de firma del Product Owner'
    resultados   = $resultados
}

$json = Join-Path $evidencias "scrum-91-uat-$sello.json"
$resumenEjecucion | ConvertTo-Json -Depth 8 | Set-Content -Path $json -Encoding UTF8

$filasHtml = foreach ($resultado in $resultados) {
    $clase = if ($resultado.resultado -eq 'ACEPTADO') { 'ok' } else { 'mal' }
    $detalle = ($resultado.detalle | ForEach-Object {
            $marca = if ($_.resultado -eq 'PASS') { 'PASS' } else { 'FALLO' }
            "$($_.comprobacion): esperado=$($_.esperado) obtenido=$($_.obtenido) [$marca]"
        }) -join '<br>'
    "    <tr><td>$($resultado.id)</td><td>$($resultado.criterio)</td><td class=""$clase"">$($resultado.resultado)</td><td>$detalle</td></tr>"
}

$html = @"
<!DOCTYPE html>
<html lang="es">
<head>
  <meta charset="utf-8">
  <title>SCRUM-91 - Checklist UAT Analytics</title>
  <style>
    body { font-family: Segoe UI, Arial, sans-serif; margin: 24px; color: #1f2933; }
    table { border-collapse: collapse; width: 100%; font-size: 13px; }
    th, td { border: 1px solid #cbd2d9; padding: 6px 8px; text-align: left; vertical-align: top; }
    th { background: #e4e7eb; }
    .ok { color: #0b6b3a; font-weight: 600; }
    .mal { color: #a61b1b; font-weight: 600; }
  </style>
</head>
<body>
  <h1>SCRUM-91 - Checklist UAT del modulo de Analytics</h1>
  <p>Fecha: $($resumenEjecucion.fecha) - Entorno: $BaseUrl - Base: $($resumenEjecucion.baseDatos)</p>
  <p>Resultado: <strong class="$(if ($veredicto -eq 'ACEPTADO') { 'ok' } else { 'mal' })">$veredicto</strong>
     ($aceptados de $total criterios aceptados). Firma del PO: pendiente.</p>
  <table>
    <thead><tr><th>ID</th><th>Criterio</th><th>Resultado</th><th>Comprobaciones</th></tr></thead>
    <tbody>
$($filasHtml -join "`n")
    </tbody>
  </table>
</body>
</html>
"@

$htmlRuta = Join-Path $evidencias "scrum-91-uat-$sello.html"
$html | Set-Content -Path $htmlRuta -Encoding UTF8

Write-Host ''
foreach ($resultado in $resultados) {
    $color = if ($resultado.resultado -eq 'ACEPTADO') { 'Green' } else { 'Red' }
    Write-Host ("{0} {1,-8} {2}" -f $resultado.id, $resultado.resultado, $resultado.criterio) -ForegroundColor $color
    foreach ($item in $resultado.detalle) {
        if ($item.resultado -ne 'PASS') {
            Write-Host ("     FALLO: {0} (esperado {1}, obtenido {2})" -f $item.comprobacion, $item.esperado, $item.obtenido) -ForegroundColor Yellow
        }
    }
}

Write-Host ''
Write-Host ("Veredicto: {0} ({1}/{2} criterios)" -f $veredicto, $aceptados, $total) -ForegroundColor Cyan
Write-Host "Evidencias: $json"
Write-Host "            $htmlRuta"

if ($aceptados -ne $total) { exit 1 }
