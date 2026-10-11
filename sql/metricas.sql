-- ===========================================================================
-- AdopPlant Analytics API - Consultas de métricas (solo lectura)
-- Sirven para verificar los endpoints con psql o DBeaver.
-- Reemplaza :meses por el número de meses deseados.
-- ===========================================================================

-- 1) Resumen general --------------------------------------------------------
SELECT
    (SELECT COUNT(*) FROM public.usuario)::int                                        AS total_usuarios,
    (SELECT COUNT(*) FROM public.usuario WHERE estado = 'ACTIVO')::int                AS usuarios_activos,
    (SELECT COUNT(*) FROM public.usuario WHERE estado = 'BLOQUEADO')::int             AS usuarios_bloqueados,
    (SELECT COUNT(*) FROM public.usuario
      WHERE fecha_registro >= now() - interval '30 days')::int                        AS usuarios_nuevos_30dias,
    (SELECT COUNT(*) FROM public.planta)::int                                         AS total_plantas,
    (SELECT COUNT(*) FROM public.planta
      WHERE NOT eliminada AND visible AND estado = 'DISPONIBLE')::int                 AS disponibles,
    (SELECT COUNT(*) FROM public.planta
      WHERE NOT eliminada AND estado = 'ADOPTADA')::int                               AS adoptadas,
    (SELECT COUNT(*) FROM public.solicitud_adopcion)::int                             AS total_solicitudes,
    (SELECT COUNT(*) FROM public.solicitud_adopcion WHERE estado = 'PENDIENTE')::int  AS pendientes,
    (SELECT COUNT(*) FROM public.adopcion WHERE estado = 'COMPLETADA')::int           AS adopciones_completadas,
    (SELECT COUNT(*) FROM public.reporte WHERE estado = 'EN_REVISION')::int           AS reportes_en_revision,
    (SELECT COUNT(*) FROM public.categoria WHERE estado = 'ACTIVA')::int              AS categorias_activas;


-- 2) Evolución mensual (12 meses) -------------------------------------------
SELECT to_char(mes, 'YYYY-MM') AS periodo,
       COUNT(DISTINCT u.id_usuario)::int     AS usuarios_nuevos,
       COUNT(DISTINCT p.id_planta)::int      AS publicaciones,
       COUNT(DISTINCT s.id_solicitud)::int   AS solicitudes,
       COUNT(DISTINCT a.id_adopcion)::int    AS adopciones_completadas
FROM generate_series(
         date_trunc('month', now()) - (11 * interval '1 month'),
         date_trunc('month', now()),
         interval '1 month') AS mes
LEFT JOIN public.usuario AS u
       ON date_trunc('month', u.fecha_registro) = mes
LEFT JOIN public.planta AS p
       ON date_trunc('month', p.fecha_publicacion) = mes
LEFT JOIN public.solicitud_adopcion AS s
       ON date_trunc('month', s.fecha_solicitud) = mes
LEFT JOIN public.adopcion AS a
       ON a.estado = 'COMPLETADA'
      AND date_trunc('month', a.fecha_recepcion) = mes
GROUP BY mes
ORDER BY mes;


-- 3) Plantas por categoría ---------------------------------------------------
SELECT c.id_categoria,
       c.nombre AS categoria,
       COUNT(p.id_planta)::int AS publicadas,
       (COUNT(p.id_planta) FILTER (WHERE p.estado = 'ADOPTADA'))::int AS adoptadas
FROM public.categoria AS c
LEFT JOIN public.planta AS p
       ON p.id_categoria = c.id_categoria AND NOT p.eliminada
GROUP BY c.id_categoria, c.nombre
ORDER BY publicadas DESC, c.nombre;


-- 4) Embudo de conversión ----------------------------------------------------
WITH base AS (
    SELECT p.id_planta,
           p.estado::text AS estado,
           EXISTS (SELECT 1 FROM public.solicitud_adopcion AS s
                   WHERE s.id_planta = p.id_planta) AS tiene_solicitud
    FROM public.planta AS p
    WHERE NOT p.eliminada
)
SELECT COUNT(*)::int                                     AS publicadas,
       (COUNT(*) FILTER (WHERE tiene_solicitud))::int    AS con_solicitud,
       (COUNT(*) FILTER (WHERE estado = 'ADOPTADA'))::int AS adoptadas
FROM base;
