"use strict";

// Panel de administración de AdopPlant.
// Flujo: login contra /api/sesion (delegado a FastAPI) -> token guardado ->
// llamadas a /api/metricas/* con Authorization: Bearer <token>.

const CLAVE_TOKEN = "adopplant_analytics_token";

let graficaEvolucion = null;
let graficaCategorias = null;

const $ = (id) => document.getElementById(id);

document.addEventListener("DOMContentLoaded", () => {
  $("form-login").addEventListener("submit", alEnviarLogin);
  $("btn-salir").addEventListener("click", cerrarSesion);
  $("btn-actualizar").addEventListener("click", () => cargarTodo(true));

  const token = sessionStorage.getItem(CLAVE_TOKEN);
  if (token) {
    mostrarPanel();
    cargarTodo(false);
  }
});

function obtenerToken() {
  return sessionStorage.getItem(CLAVE_TOKEN);
}

function mostrarErrorLogin(mensaje) {
  const p = $("error-login");
  p.textContent = mensaje;
  p.hidden = false;
}

function mostrarErrorPanel(mensaje) {
  const p = $("error-panel");
  p.textContent = mensaje;
  p.hidden = !mensaje;
}

async function alEnviarLogin(evento) {
  evento.preventDefault();
  mostrarErrorLogin("");
  $("error-login").hidden = true;

  const identificador = $("identificador").value.trim();
  const contrasena = $("contrasena").value;

  try {
    const respuesta = await fetch("/api/sesion", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ identificador, contrasena }),
    });

    if (respuesta.status === 401) {
      mostrarErrorLogin("Credenciales inválidas o cuenta bloqueada.");
      return;
    }

    if (!respuesta.ok) {
      mostrarErrorLogin("No se pudo iniciar sesión (error " + respuesta.status + ").");
      return;
    }

    const datos = await respuesta.json();
    // FastAPI devuelve { access_token, token_type }; la .NET lo normaliza
    // a { accessToken } en camelCase.
    const token = datos.accessToken || datos.access_token;
    if (!token) {
      mostrarErrorLogin("Respuesta de sesión inesperada.");
      return;
    }

    sessionStorage.setItem(CLAVE_TOKEN, token);
    $("contrasena").value = "";
    mostrarPanel();
    await cargarTodo(false);
  } catch (error) {
    mostrarErrorLogin("No se pudo contactar al servidor: " + error.message);
  }
}

function mostrarPanel() {
  $("vista-login").hidden = true;
  $("vista-panel").hidden = false;
  $("btn-salir").hidden = false;
  $("estado-sesion").textContent = "Sesión iniciada";
}

function cerrarSesion() {
  sessionStorage.removeItem(CLAVE_TOKEN);
  $("vista-login").hidden = false;
  $("vista-panel").hidden = true;
  $("btn-salir").hidden = true;
  $("estado-sesion").textContent = "Sin sesión";
}

async function llamarMetricas(ruta) {
  const respuesta = await fetch(ruta, {
    headers: { Authorization: "Bearer " + obtenerToken() },
  });

  if (respuesta.status === 401) {
    cerrarSesion();
    throw new Error("La sesión venció. Vuelve a iniciar sesión.");
  }

  if (respuesta.status === 403) {
    throw new Error("Tu cuenta no es administradora.");
  }

  if (!respuesta.ok) {
    throw new Error("Error " + respuesta.status + " al pedir " + ruta);
  }

  return respuesta.json();
}

async function cargarTodo(forzar) {
  mostrarErrorPanel("");
  $("estado-carga").textContent = "Cargando…";
  if (forzar) {
    $("btn-actualizar").disabled = true;
  }

  try {
    const meses = $("meses").value;
    const [resumen, evolucion, categorias, embudo, usuarios, reportes] = await Promise.all([
      llamarMetricas("/api/metricas/resumen"),
      llamarMetricas("/api/metricas/evolucion?meses=" + encodeURIComponent(meses)),
      llamarMetricas("/api/metricas/plantas-por-categoria"),
      llamarMetricas("/api/metricas/embudo"),
      llamarMetricas("/api/metricas/usuarios?limite=5"),
      llamarMetricas("/api/metricas/reportes?limite=5"),
    ]);

    pintarKpis(resumen);
    pintarEvolucion(evolucion);
    pintarCategorias(categorias);
    pintarEmbudo(embudo);
    pintarUsuarios(usuarios);
    pintarReportes(reportes);

    $("estado-carga").textContent =
      "Actualizado a las " + new Date().toLocaleTimeString("es-ES");
  } catch (error) {
    mostrarErrorPanel(error.message);
    $("estado-carga").textContent = "";
  } finally {
    $("btn-actualizar").disabled = false;
  }
}

function pintarKpis(r) {
  const tarjetas = [
    ["Usuarios", r.usuarios.total],
    ["Usuarios activos", r.usuarios.activos],
    ["Nuevos (30 días)", r.usuarios.nuevosUltimos30Dias],
    ["Plantas", r.plantas.total],
    ["Disponibles", r.plantas.disponibles],
    ["Solicitadas", r.plantas.solicitadas],
    ["Adoptadas", r.plantas.adoptadas],
    ["Solicitudes", r.solicitudes.total],
    ["Pendientes", r.solicitudes.pendientes],
    ["Aceptadas", r.solicitudes.aceptadas],
    ["Adopciones completadas", r.adopciones.completadas],
    ["Reportes en revisión", r.reportes.enRevision],
  ];

  $("kpis").innerHTML = tarjetas
    .map(([etiqueta, valor]) =>
      '<div class="kpi"><div class="valor">' + valor + "</div>" +
      '<div class="etiqueta">' + etiqueta + "</div></div>")
    .join("");
}

function pintarEvolucion(puntos) {
  const etiquetas = puntos.map((p) => p.periodo);
  const datos = {
    labels: etiquetas,
    datasets: [
      { label: "Publicaciones", data: puntos.map((p) => p.publicaciones) },
      { label: "Solicitudes", data: puntos.map((p) => p.solicitudes) },
      { label: "Adopciones", data: puntos.map((p) => p.adopcionesCompletadas) },
      { label: "Usuarios nuevos", data: puntos.map((p) => p.usuariosNuevos) },
    ],
  };

  if (graficaEvolucion) {
    graficaEvolucion.data = datos;
    graficaEvolucion.update();
    return;
  }

  graficaEvolucion = new Chart($("grafica-evolucion"), {
    type: "line",
    data: datos,
    options: { responsive: true, tension: 0.25 },
  });
}

function pintarCategorias(filas) {
  const datos = {
    labels: filas.map((f) => f.categoria),
    datasets: [
      { label: "Publicadas", data: filas.map((f) => f.publicadas) },
      { label: "Adoptadas", data: filas.map((f) => f.adoptadas) },
    ],
  };

  if (graficaCategorias) {
    graficaCategorias.data = datos;
    graficaCategorias.update();
    return;
  }

  graficaCategorias = new Chart($("grafica-categorias"), {
    type: "bar",
    data: datos,
    options: { responsive: true, indexAxis: "y" },
  });
}

function pintarEmbudo(e) {
  $("embudo").innerHTML =
    paso("Publicadas", e.publicadas) +
    paso("Con al menos una solicitud", e.conSolicitud) +
    paso("Adoptadas", e.adoptadas) +
    '<div class="paso"><strong>Tasa de conversión</strong><strong>' +
    (e.tasaConversion ?? e.porcentajeAdopcion) + "%</strong></div>";

  function paso(etiqueta, valor) {
    return '<div class="paso"><span>' + etiqueta + "</span><strong>" + valor + "</strong></div>";
  }
}

function pintarUsuarios(u) {
  $("top-donantes").innerHTML = lista(u.topDonantes);
  $("top-adoptantes").innerHTML = lista(u.topAdoptantes);

  function lista(items) {
    if (!items || items.length === 0) return "<li>Sin datos todavía</li>";
    return items.map((x) => "<li>" + escapar(x.nombre) + " — " + x.total + "</li>").join("");
  }
}

function pintarReportes(r) {
  $("reportes-estado").innerHTML = (r.porEstado || [])
    .map((x) => "<li>" + escapar(x.estado) + ": <strong>" + x.total + "</strong></li>")
    .join("") || "<li>Sin reportes</li>";

  $("reportes-usuarios").innerHTML = (r.masReportados || [])
    .map((x) => "<li>" + escapar(x.nombre) + " — " + x.total + "</li>")
    .join("") || "<li>Sin reportes</li>";
}

function escapar(texto) {
  return String(texto)
    .replaceAll("&", "&amp;")
    .replaceAll("<", "&lt;")
    .replaceAll(">", "&gt;");
}
