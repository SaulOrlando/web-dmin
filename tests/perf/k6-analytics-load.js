// SCRUM-96 - Prueba de rendimiento de la API C# (AdopPlant Analytics, .NET 8).
//
// Escenario exigido por el ticket:
//   50 usuarios concurrentes durante 2 minutos enviando el JWT de administrador
//   contra GET /api/metricas/resumen y GET /api/metricas/plantas-por-categoria.
// Criterio: 200 OK, tiempo promedio < 2000 ms y 0 % de errores.
//
// Uso local:
//   k6 run -e BASE_URL=http://localhost:5080 AdopPlant.Analytics/tests/perf/k6-analytics-load.js
//
// Uso con Docker (desde la raiz del workspace):
//   docker compose -f docker-compose.yml -f compose.k6-analytics.yaml run --rm k6-analytics

import http from 'k6/http';
import { check, sleep } from 'k6';
import { Rate, Trend } from 'k6/metrics';

const BASE_URL = (__ENV.BASE_URL || 'http://analytics:8080').replace(/\/+$/, '');
const ADMIN_USER = __ENV.ADMIN_USER || 'admin.panel@adopplant.com';
const ADMIN_PASSWORD = __ENV.ADMIN_PASSWORD || 'Admin2026panel';

const erroresHttp = new Rate('errores_http');
const latenciaResumen = new Trend('latencia_resumen');
const latenciaCategorias = new Trend('latencia_categorias');

export const options = {
  scenarios: {
    cincuenta_usuarios: {
      executor: 'constant-vus',
      vus: 50,
      duration: '2m',
      tags: { escenario: '50vu' },
    },
  },
  thresholds: {
    'http_req_failed{escenario:50vu}': ['rate==0'],
    'http_req_duration{escenario:50vu,endpoint:resumen}': ['p(95)<2000', 'avg<2000'],
    'http_req_duration{escenario:50vu,endpoint:categorias}': ['p(95)<2000', 'avg<2000'],
    errores_http: ['rate==0'],
  },
};

export function setup() {
  const login = http.post(
    `${BASE_URL}/api/sesion`,
    JSON.stringify({ identificador: ADMIN_USER, contrasena: ADMIN_PASSWORD }),
    {
      headers: { 'Content-Type': 'application/json' },
      tags: { endpoint: 'login' },
    },
  );

  check(login, {
    'POST /api/sesion responde 200': (r) => r.status === 200,
    'POST /api/sesion devuelve accessToken': (r) => Boolean(r.json('accessToken')),
  });

  return { token: login.json('accessToken') };
}

export default function (data) {
  const cabeceras = { Authorization: `Bearer ${data.token}` };

  const resumen = http.get(`${BASE_URL}/api/metricas/resumen`, {
    headers: cabeceras,
    tags: { endpoint: 'resumen' },
  });
  latenciaResumen.add(resumen.timings.duration);
  check(resumen, {
    'GET /api/metricas/resumen responde 200': (r) => r.status === 200,
    'el resumen trae los seis bloques': (r) => {
      if (r.status !== 200) return false;
      const cuerpo = r.json();
      return Boolean(
        cuerpo &&
          cuerpo.usuarios &&
          cuerpo.plantas &&
          cuerpo.solicitudes &&
          cuerpo.adopciones &&
          cuerpo.reportes &&
          cuerpo.categorias,
      );
    },
  });
  erroresHttp.add(resumen.status !== 200);

  const categorias = http.get(
    `${BASE_URL}/api/metricas/plantas-por-categoria`,
    { headers: cabeceras, tags: { endpoint: 'categorias' } },
  );
  latenciaCategorias.add(categorias.timings.duration);
  check(categorias, {
    'GET /api/metricas/plantas-por-categoria responde 200': (r) => r.status === 200,
    'devuelve un arreglo': (r) => Array.isArray(r.json()),
  });
  erroresHttp.add(categorias.status !== 200);

  sleep(0.5);
}
