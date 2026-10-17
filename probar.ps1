$ErrorActionPreference = 'Stop'
Push-Location $PSScriptRoot
try {
    $runDirectory = Join-Path 'TestResults' ([guid]::NewGuid().ToString())
    dotnet test tests/AdopPlant.Analytics.Tests/AdopPlant.Analytics.Tests.csproj --collect 'XPlat Code Coverage' --settings coverage.runsettings --results-directory $runDirectory --logger 'trx;LogFileName=unitarias.trx' -p:RollForward=Major
    if ($LASTEXITCODE -ne 0) { throw 'Las pruebas no terminaron correctamente. Revisar la salida.' }
    dotnet tool restore
    if ($LASTEXITCODE -ne 0) { throw 'No se pudo restaurar ReportGenerator.' }
    $report = Get-ChildItem -LiteralPath $runDirectory -Recurse -Filter coverage.cobertura.xml | Select-Object -First 1
    if (-not $report) { throw 'No se genero el archivo de cobertura.' }
    dotnet tool run reportgenerator "-reports:$($report.FullName)" '-targetdir:coverage' '-reporttypes:Html;TextSummary'
    if ($LASTEXITCODE -ne 0) { throw 'No se pudo generar el informe.' }
    Write-Host 'Informe disponible en coverage/index.html'
} finally {
    Pop-Location
}
