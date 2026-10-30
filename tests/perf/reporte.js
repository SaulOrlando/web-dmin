// Reportes reutilizables de las pruebas k6.
//
// Cada script llama a construirReportes(data, nombre) desde handleSummary y k6
// escribe, al terminar, tres artefactos dentro de REPORTES_DIR (por defecto
// `tests/perf/reports`, relativo a la carpeta desde donde se lanza k6):
//
//   <nombre>-resumen.json   volcado completo de metricas (evidencia cruda)
//   <nombre>-reporte.html   tabla legible para adjuntar en Jira
//   stdout                  resumen corto en consola
//
// Se ejecuta k6 desde la raiz del proyecto (AdopPlant.Analytics/) para que la
// carpeta de reportes exista. Se puede cambiar con:
//   k6 run -e REPORTES_DIR=mi/carpeta script.js

const DIR = __ENV.REPORTES_DIR || 'tests/perf/reports';

export function construirReportes(data, nombre) {
  return {
    [`${DIR}/${nombre}-resumen.json`]: JSON.stringify(data, null, 2),
    [`${DIR}/${nombre}-reporte.html`]: html(data, nombre),
    stdout: texto(data, nombre),
  };
}

function valor(metricas, clave) {
  const m = metricas[clave];
  return m && m.values ? m.values : {};
}

function numero(n, decimales = 2) {
  return typeof n === 'number' && Number.isFinite(n) ? n.toFixed(decimales) : '-';
}

function escapar(texto) {
  return String(texto)
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;');
}

function filasMetricas(metricas) {
  return Object.entries(metricas)
    .filter(([, m]) => m && m.values)
    .map(([nombre, m]) => {
      const v = m.values;
      return {
        nombre,
        tipo: m.type,
        promedio: numero(v.avg),
        mediana: numero(v.med),
        p95: numero(v['p(95)']),
        maximo: numero(v.max),
        tasa: typeof v.rate === 'number' ? `${numero(v.rate * 100)} %` : '-',
        total: typeof v.count === 'number' ? v.count : '-',
      };
    })
    .sort((a, b) => a.nombre.localeCompare(b.nombre));
}

function html(data, nombre) {
  const filas = filasMetricas(data.metrics)
    .map(
      (f) => `      <tr>
        <td>${escapar(f.nombre)}</td>
        <td>${escapar(f.tipo)}</td>
        <td>${f.promedio}</td>
        <td>${f.mediana}</td>
        <td>${f.p95}</td>
        <td>${f.maximo}</td>
        <td>${f.tasa}</td>
        <td>${f.total}</td>
      </tr>`,
    )
    .join('\n');

  const checks = valor(data.metrics, 'checks');
  const pases = checks.passes ?? 0;
  const fallos = checks.fails ?? 0;
  const total = pases + fallos;
  const veredicto = fallos === 0 ? 'APROBADO (0 checks fallidos)' : `CON FALLOS (${fallos} checks fallidos)`;

  return `<!DOCTYPE html>
<html lang="es">
<head>
  <meta charset="utf-8">
  <title>${escapar(nombre)} - Reporte k6</title>
  <style>
    body { font-family: Segoe UI, Arial, sans-serif; margin: 24px; color: #1f2933; }
    h1 { font-size: 20px; }
    h2 { font-size: 16px; margin-top: 24px; }
    table { border-collapse: collapse; width: 100%; font-size: 13px; }
    th, td { border: 1px solid #cbd2d9; padding: 6px 8px; text-align: left; }
    th { background: #e4e7eb; }
    tr:nth-child(even) td { background: #f5f7fa; }
    .ok { color: #0b6b3a; font-weight: 600; }
    .mal { color: #a61b1b; font-weight: 600; }
  </style>
</head>
<body>
  <h1>${escapar(nombre)} - Reporte de prueba de carga (k6)</h1>
  <p>Generado: ${escapar(new Date().toISOString())} ·
     Duracion total del test: ${numero(valor(data.metrics, 'http_req_duration').avg, 2)} ms de promedio por peticion.</p>
  <p class="${fallos === 0 ? 'ok' : 'mal'}">${escapar(veredicto)} ·
     Checks: ${pases}/${total} correctos.</p>

  <h2>Metricas</h2>
  <table>
    <thead>
      <tr>
        <th>Metrica</th><th>Tipo</th><th>Promedio</th><th>Mediana</th>
        <th>p95</th><th>Maximo</th><th>Tasa</th><th>Muestras</th>
      </tr>
    </thead>
    <tbody>
${filas}
    </tbody>
  </table>

  <h2>Thresholds configurados</h2>
  <p>Si algun threshold falla, k6 termina con codigo de salida distinto de cero y
     el resumen de consola lo marca como <em>FAILED</em>. Este HTML se genera
     igualmente para conservar la evidencia.</p>
</body>
</html>
`;
}

function texto(data, nombre) {
  const lineas = [`Resumen ${nombre}`];
  const metricas = data.metrics || {};

  for (const [clave, m] of Object.entries(metricas)) {
    if (!m || !m.values) continue;
    if (!/^(http_req_duration|http_req_failed|checks|iterations|errores_http|latencia_)/.test(clave)) continue;
    const v = m.values;
    const partes = [];
    if (v.avg !== undefined) partes.push(`avg=${numero(v.avg)}ms`);
    if (v['p(95)'] !== undefined) partes.push(`p95=${numero(v['p(95)'])}ms`);
    if (v.rate !== undefined) partes.push(`rate=${numero(v.rate * 100)}%`);
    if (v.count !== undefined) partes.push(`count=${v.count}`);
    if (v.passes !== undefined) partes.push(`passes=${v.passes}`);
    if (v.fails !== undefined) partes.push(`fails=${v.fails}`);
    lineas.push(`  ${clave}: ${partes.join(' ')}`);
  }

  lineas.push(`  Reportes: ${DIR}/${nombre}-resumen.json y ${DIR}/${nombre}-reporte.html`);
  return `${lineas.join('\n')}\n`;
}
