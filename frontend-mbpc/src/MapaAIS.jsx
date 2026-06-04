import { useEffect, useRef, useState, useCallback } from "react";
import "@arcgis/core/assets/esri/themes/light/main.css";
import apiClient from "./axiosClient";
import { useTransferirJurisdiccion } from "./hooks/useTransferirJurisdiccion";

// ─────────────────────────────────────────────────────────────────────────────
// MapaAIS.jsx  (v3 — Hito 14: Polígonos Dinámicos vía GeoJSONLayer + Geofencing)
//
// Cambios respecto a v2:
//   • Eliminada la constante COSTERAS_POLIGONOS (hardcodeada).
//   • Se agrega GeoJSONLayer dinámico cargado desde /api/costeras/limites.
//   • Blob Hack: el JWT de Axios se usa para descargar el GeoJSON y convertirlo
//     en una URL de objeto para que GeoJSONLayer pueda consumirlo sin CORS.
//   • costerasGeometriesRef reemplaza la lista estática para el motor Geofencing.
//   • GeoJSONLayer se inserta DEBAJO del FeatureLayer de buques.
// ─────────────────────────────────────────────────────────────────────────────

// ── Paleta de estados ────────────────────────────────────────────────────────
const ESTADO_COLOR = {
  Navegando: [24,  144, 255],
  Amarrado : [82,  196, 26],
  Fondeado : [250, 173, 20],
  default  : [150, 150, 150],
};

function colorPorEstado(estadoStr) {
  if (!estadoStr || estadoStr === "N/A") return ESTADO_COLOR.default;
  const e = estadoStr.toLowerCase();
  if (e.includes("amarr"))                                         return ESTADO_COLOR.Amarrado;
  if (e.includes("fonde") || e.includes("ancla"))                  return ESTADO_COLOR.Fondeado;
  if (
    e.includes("navegando") || e.includes("transitando") ||
    e.includes("salio")     || e.includes("entro")       ||
    e.includes("pesca")     || e.includes("exploracion") ||
    e.includes("reanuda")   || e.includes("paso inocente")
  )                                                                return ESTADO_COLOR.Navegando;
  return ESTADO_COLOR.default;
}

// ── Configuración de clustering ──────────────────────────────────────────────
const FEATURE_REDUCTION_CLUSTER = {
  type         : "cluster",
  clusterRadius: "80px",
  popupTemplate: {
    title  : "Agrupación de {cluster_count} buques",
    content: "{cluster_count} buques en esta área. Acercate para ver los detalles individuales.",
  },
  clusterMinSize  : "28px",
  clusterMaxSize  : "52px",
  labelingInfo    : [
    {
      deconflictionStrategy: "none",
      labelExpressionInfo  : { expression: "Text($feature.cluster_count, '#,###')" },
      symbol: {
        type     : "text",
        color    : "#ffffff",
        font     : { weight: "bold", family: "Noto Sans", size: "12px" },
        haloColor: "rgba(0,36,84,0.4)",
        haloSize : "1px",
      },
      labelPlacement: "center-center",
    },
  ],
};

// ── Schema de campos para el FeatureLayer client-side ────────────────────────
const FIELDS_BUQUE = [
  { name: "ObjectID",           alias: "ObjectID",               type: "oid"    },
  { name: "id",                 alias: "ID MongoDB",              type: "string" },
  { name: "nombreBuque",        alias: "Nombre del Buque",        type: "string" },
  { name: "mmsi",               alias: "MMSI",                    type: "string" },
  { name: "imo",                alias: "IMO",                     type: "string" },
  { name: "estadoNav",          alias: "Estado de Navegación",    type: "string" },
  { name: "velocidad",          alias: "Velocidad (kn)",          type: "double" },
  { name: "rumbo",              alias: "Rumbo (°)",               type: "double" },
  { name: "origen",             alias: "Origen",                  type: "string" },
  { name: "destino",            alias: "Destino",                 type: "string" },
  { name: "cantidadBarcazas",   alias: "Barcazas",                type: "integer"},
  { name: "remolcador",         alias: "Remolcador",              type: "string" },
  { name: "ultimaActualizacion",alias: "Última Posición",         type: "string" },
  { name: "latitud",            alias: "Latitud",                 type: "double" },
  { name: "longitud",           alias: "Longitud",                type: "double" },
];

const getOperatorCosteraId = () => {
  const token = localStorage.getItem("mbpc_token");
  if (!token) return null;
  try {
    const payload = JSON.parse(atob(token.split('.')[1]));
    return Number(payload.CosteraId || payload.costeraId || 0);
  } catch (e) {
    return null;
  }
};

// ─────────────────────────────────────────────────────────────────────────────
export default function MapaAIS() {
  const mapDiv            = useRef(null);
  const viewRef           = useRef(null);
  const featureLayerRef   = useRef(null);  // FeatureLayer client-side (buques + clustering)
  const routeLayerRef     = useRef(null);  // GraphicsLayer (rutas)
  const highlightLayerRef = useRef(null);  // GraphicsLayer temporal (anillo de viboreo)
  const arcgisRef         = useRef(null);
  const oidCounter        = useRef(1);     // ObjectID auto-incremental para el FeatureLayer
  const jurisdiccionPreviaRef = useRef({});
  const alertasMostradasRef = useRef({});

  // ── Ref de geometrías de costeras para el motor de Geofencing ──────────────
  // Cada entrada: { id: number, nombre: string, poligonoArcgis: Polygon[] }
  // (puede haber múltiples Polygon por entrada si el tipo es MultiPolygon)
  const costerasGeometriesRef = useRef([]);

  const { mutate: transferirJurisdiccion } = useTransferirJurisdiccion();

  const [buques,         setBuques]        = useState([]);
  const [filtroTexto,    setFiltroTexto]   = useState("");
  const [buqueSeleccion, setBuqueSeleccion]= useState(null);
  const [cargando,       setCargando]      = useState(true);
  const [error,          setError]         = useState(null);
  const [panelAbierto,   setPanelAbierto]  = useState(true);
  const [transferenciaPendiente, setTransferenciaPendiente] = useState(null);

  const handleConfirmarTransferencia = () => {
    if (transferenciaPendiente) {
      const { viajeId, nuevaCosteraId } = transferenciaPendiente;
      jurisdiccionPreviaRef.current[viajeId] = nuevaCosteraId;
      transferirJurisdiccion({ viajeId, nuevaCosteraId }, {
        onSuccess: () => {
          setTransferenciaPendiente(null);
          delete alertasMostradasRef.current[viajeId];
        },
        onError: () => {
          delete alertasMostradasRef.current[viajeId];
          setTransferenciaPendiente(null);
        }
      });
    }
  };

  const handleCancelarTransferencia = () => {
    if (transferenciaPendiente) {
      const { viajeId, previoId } = transferenciaPendiente;
      jurisdiccionPreviaRef.current[viajeId] = previoId;
      delete alertasMostradasRef.current[viajeId];
      setTransferenciaPendiente(null);
    }
  };

  // ── 1. Inicializar ArcGIS ──────────────────────────────────────────────────
  useEffect(() => {
    let vista;
    let blobUrl = null; // se revoca en el cleanup para evitar memory leaks

    async function init() {
      console.info("[MapaAIS] Iniciando carga de módulos ArcGIS...");

      const [
        { default: Map              },
        { default: MapView          },
        { default: GraphicsLayer    },
        { default: FeatureLayer     },
        { default: GeoJSONLayer     },
        { default: Graphic          },
        { default: Point            },
        { default: SimpleMarkerSymbol },
        { default: SimpleLineSymbol },
        { default: Polyline         },
        { default: esriConfig       },
        geometryEngine,
        { default: Polygon          },
      ] = await Promise.all([
        import("@arcgis/core/Map.js"),
        import("@arcgis/core/views/MapView.js"),
        import("@arcgis/core/layers/GraphicsLayer.js"),
        import("@arcgis/core/layers/FeatureLayer.js"),
        import("@arcgis/core/layers/GeoJSONLayer.js"),
        import("@arcgis/core/Graphic.js"),
        import("@arcgis/core/geometry/Point.js"),
        import("@arcgis/core/symbols/SimpleMarkerSymbol.js"),
        import("@arcgis/core/symbols/SimpleLineSymbol.js"),
        import("@arcgis/core/geometry/Polyline.js"),
        import("@arcgis/core/config.js"),
        import("@arcgis/core/geometry/geometryEngine.js"),
        import("@arcgis/core/geometry/Polygon.js"),
      ]);

      console.info("[MapaAIS] Módulos ArcGIS cargados correctamente.");
      esriConfig.apiKey = "";

      arcgisRef.current = {
        Graphic, Point, SimpleMarkerSymbol, SimpleLineSymbol, Polyline, FeatureLayer, geometryEngine, Polygon
      };

      // ── Capas base siempre presentes ──────────────────────────────────────
      const routeLayer = new GraphicsLayer({ id: "rutas" });
      routeLayerRef.current = routeLayer;

      const highlightLayer = new GraphicsLayer({ id: "highlight", listMode: "hide" });
      highlightLayerRef.current = highlightLayer;

      const buqueFeatureLayer = new FeatureLayer({
        id             : "buques",
        source         : [],
        fields         : FIELDS_BUQUE,
        objectIdField  : "ObjectID",
        geometryType   : "point",
        spatialReference: { wkid: 4326 },
        renderer       : buildRenderer(SimpleMarkerSymbol),
        popupTemplate  : buildPopupTemplate(),
        featureReduction: FEATURE_REDUCTION_CLUSTER,
      });
      featureLayerRef.current = buqueFeatureLayer;

      // ── Intentar carga de jurisdicciones (Blob Hack) ──────────────────────
      // Si falla, el mapa igual se levanta con el basemap y los buques.
      try {
        blobUrl = await cargarPoligonosJurisdicciones(Polygon);
      } catch (errCosteras) {
        // cargarPoligonosJurisdicciones ya loguea el error internamente;
        // aquí solo nos aseguramos de que blobUrl quede en null.
        console.warn("[MapaAIS] Se omite la capa de jurisdicciones por error en la carga.");
        blobUrl = null;
      }

      // ── Armar el stack de capas del mapa ──────────────────────────────────
      // GeoJSONLayer solo se agrega si blobUrl es válido.
      const capasBase = [routeLayer, buqueFeatureLayer, highlightLayer];

      if (blobUrl) {
        console.info("[MapaAIS] GeoJSONLayer de jurisdicciones inicializado con Blob URL:", blobUrl);
        const geoJsonLayer = new GeoJSONLayer({
          id   : "costeras",
          url  : blobUrl,
          title: "Límites Jurisdiccionales",
          renderer: {
            type  : "simple",
            symbol: {
              type   : "simple-line",
              color  : [0, 36, 84, 0.8],
              width  : 2
            },
          },
          popupTemplate: {
            title  : "Jurisdicción: {nombre}",
            content: [
              {
                type      : "fields",
                fieldInfos: [
                  { fieldName: "costeraId", label: "ID Costera" },
                  { fieldName: "nombre",    label: "Nombre"     },
                ],
              },
            ],
          },
          effect: "drop-shadow(0px 0px 6px rgba(0,170,220,0.45))",
        });
        // Insertar costeras como primera capa (fondo del mapa)
        capasBase.unshift(geoJsonLayer);
      } else {
        console.warn(
          "[MapaAIS] blobUrl es null — GeoJSONLayer de jurisdicciones NO fue agregado al mapa. " +
          "El motor de Geofencing estará inactivo hasta que el endpoint /api/costeras/limites responda."
        );
      }

      const map = new Map({ basemap: "osm", layers: capasBase });

      vista = new MapView({
        container: mapDiv.current,
        map,
        center: [-58.4, -34.6],
        zoom  : 6,
        ui    : { components: ["zoom", "compass"] },
        popup : { dockEnabled: false, dockOptions: { buttonEnabled: false } },
      });

      viewRef.current = vista;
      await vista.when();
      console.info("[MapaAIS] MapView listo. Cargando buques...");
      await fetchYRenderizar();
    }

    init().catch(err => {
      console.error("[MapaAIS] Error crítico en la inicialización del mapa:", err);
      setError("No se pudo inicializar el mapa. Revisá la consola para más detalles.");
      setCargando(false);
    });

    return () => {
      vista?.destroy();
      if (blobUrl) {
        URL.revokeObjectURL(blobUrl);
        console.info("[MapaAIS] Blob URL de jurisdicciones revocada correctamente.");
      }
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  // ── Carga GeoJSON desde el backend con JWT y convierte a Blob URL ──────────
  // Retorna la Blob URL si tiene éxito, o null si falla (el mapa sigue cargando).
  // ── Carga GeoJSON desde el backend con JWT y convierte a Blob URL ──────────
  // Retorna la Blob URL si tiene éxito, o null si falla (el mapa sigue cargando).
  async function cargarPoligonosJurisdicciones(Polygon) {
    try {
      const res = await apiClient.get("/costeras/limites", { params: { todos: true } });
      const geoJsonData = res.data;

      if (!geoJsonData || !geoJsonData.features) {
        console.warn("[Costeras] El payload de límites llegó vacío o sin features.");
        return null;
      }

      // 1. Filtrado Defensivo
      geoJsonData.features = geoJsonData.features.filter(f => 
        f.geometry && f.geometry.coordinates && f.geometry.coordinates.length > 0
      );

      const features = geoJsonData.features;

      // 2. Poblar costerasGeometriesRef para el motor de Geofencing
      const geometrias = [];
      for (const feature of features) {
        const { costeraId, nombre } = feature.properties ?? {};
        const geom = feature.geometry;

        if (!geom || !costeraId) continue;

        const poligonosArcgis = [];
        const { Polygon: ArcGISPolygon, Polyline: ArcGISPolyline } = arcgisRef.current ?? {};

        if (geom.type === "Polygon") {
          poligonosArcgis.push(new (ArcGISPolygon || Polygon)({
            rings: geom.coordinates,
            spatialReference: { wkid: 4326 },
          }));
        } else if (geom.type === "LineString") {
          if (ArcGISPolyline) {
            poligonosArcgis.push(new ArcGISPolyline({
              paths: [geom.coordinates],
              spatialReference: { wkid: 4326 }
            }));
          } else {
            console.warn("[Costeras] ArcGISPolyline no cargado aún para LineString.");
          }
        } else if (geom.type === "MultiPolygon") {
          for (const ring of geom.coordinates) {
            poligonosArcgis.push(new (ArcGISPolygon || Polygon)({
              rings: ring,
              spatialReference: { wkid: 4326 },
            }));
          }
        }

        if (poligonosArcgis.length > 0) {
          geometrias.push({ id: costeraId, nombre, poligonosArcgis });
        }
      }

      costerasGeometriesRef.current = geometrias;
      console.info(`[Geofencing] ${geometrias.length} jurisdicciones cargadas en memoria.`);

      // 3. Filtro del operador relajado para permitir pruebas
      const operatorCosteraId = getOperatorCosteraId();
      let visualGeoJsonData = { ...geoJsonData };
      if (operatorCosteraId > 0 && operatorCosteraId <= 31) {
        visualGeoJsonData.features = features.filter(
          f => f.properties && Number(f.properties.costeraId) === operatorCosteraId
        );
      }

      const blob = new Blob([JSON.stringify(visualGeoJsonData)], { type: "application/json" });
      const blobUrl = URL.createObjectURL(blob);
      return blobUrl;
    } catch (err) {
      console.error("[Geofencing] Error cargando límites jurisdiccionales:", err);
      return null;
    }
  }

  // ── 2. Renderer para el FeatureLayer ──────────────────────────────────────
  function buildRenderer(SimpleMarkerSymbol) {
    // UniqueValueRenderer basado en el campo estadoNav
    return {
      type          : "unique-value",
      field         : "estadoNav",
      defaultSymbol : {
        type   : "simple-marker",
        style  : "triangle",
        color  : [...ESTADO_COLOR.default, 210],
        size   : "14px",
        outline: { color: [255, 255, 255, 200], width: 1.5 },
      },
      uniqueValueInfos: [
        {
          value : "Navegando",
          symbol: {
            type   : "simple-marker",
            style  : "triangle",
            color  : [...ESTADO_COLOR.Navegando, 230],
            size   : "14px",
            outline: { color: [255, 255, 255, 200], width: 1.5 },
          },
        },
        {
          value : "Amarrado",
          symbol: {
            type   : "simple-marker",
            style  : "triangle",
            color  : [...ESTADO_COLOR.Amarrado, 230],
            size   : "13px",
            outline: { color: [255, 255, 255, 200], width: 1.5 },
          },
        },
        {
          value : "Fondeado",
          symbol: {
            type   : "simple-marker",
            style  : "triangle",
            color  : [...ESTADO_COLOR.Fondeado, 230],
            size   : "13px",
            outline: { color: [255, 255, 255, 200], width: 1.5 },
          },
        },
      ],
      visualVariables: [
        {
          type : "rotation",
          field: "rumbo",
          rotationType: "geographic",
        },
      ],
    };
  }

  // ── 3. PopupTemplate institucional PNA ───────────────────────────────────
  function buildPopupTemplate() {
    return {
      title  : "{nombreBuque}",
      // content como función que retorna un HTMLElement institucional
      content: (feature) => {
        const a = feature?.graphic?.attributes ?? {};

        const estadoColor = (() => {
          const rgb = colorPorEstado(a.estadoNav);
          return `rgb(${rgb.join(",")})`;
        })();

        const wrapper = document.createElement("div");
        wrapper.style.cssText = [
          "font-family:'Segoe UI',system-ui,sans-serif",
          "font-size:13px",
          "min-width:240px",
          "border-radius:8px",
          "overflow:hidden",
          "box-shadow:0 2px 12px rgba(0,36,84,.15)",
        ].join(";");

        wrapper.innerHTML = `
          <!-- Header azul institucional -->
          <div style="
            background:#002454;
            color:#fff;
            padding:10px 14px;
            display:flex;
            align-items:center;
            gap:10px;
          ">
            <div style="
              width:32px;height:32px;
              background:#104a8e;
              border-radius:50%;
              display:flex;align-items:center;justify-content:center;
              font-size:11px;font-weight:700;color:#fff;
              border:1px solid rgba(255,255,255,.25);
              flex-shrink:0;
            ">PNA</div>
            <div>
              <div style="font-weight:700;font-size:14px;line-height:1.2">${a.nombreBuque ?? "—"}</div>
              <div style="font-size:11px;color:#93c5fd;margin-top:1px">
                ${a.mmsi ? `MMSI ${a.mmsi}` : ""}${a.imo ? `  ·  IMO ${a.imo}` : ""}
              </div>
            </div>
          </div>

          <!-- Estado badge -->
          <div style="padding:8px 14px 0">
            <span style="
              display:inline-block;
              background:${estadoColor}22;
              color:${estadoColor};
              border:1px solid ${estadoColor}55;
              border-radius:99px;
              padding:2px 10px;
              font-size:11px;
              font-weight:600;
            ">${a.estadoNav ?? "N/A"}</span>
          </div>

          <!-- Tabla de datos -->
          <div style="padding:8px 14px 12px">
            ${fila("🧭", "Rumbo",       a.rumbo      != null ? `${a.rumbo}°` : "—")}
            ${fila("⚡", "Velocidad",   a.velocidad  != null ? `${a.velocidad} kn` : "—")}
            ${fila("🛫", "Origen",      a.origen     ?? "Sin datos")}
            ${fila("🛬", "Destino",     a.destino    ?? "Sin datos")}
            ${fila("📍", "Coordenadas", a.latitud != null
              ? `${(+a.latitud).toFixed(5)}, ${(+a.longitud).toFixed(5)}`
              : "—")}
            ${fila("🕐", "Última pos.", a.ultimaActualizacion ?? "—")}
            ${a.cantidadBarcazas > 0
              ? fila("⚓", "Barcazas", `${a.cantidadBarcazas}${a.remolcador ? ` · ${a.remolcador}` : ""}`)
              : ""}
          </div>
        `;

        return wrapper;

        function fila(icon, label, value) {
          return `
            <div style="display:flex;gap:6px;align-items:baseline;margin-top:5px">
              <span style="width:18px;text-align:center;font-size:12px;flex-shrink:0">${icon}</span>
              <span style="color:#6b7280;font-size:11px;width:82px;flex-shrink:0">${label}</span>
              <span style="color:#111827;font-size:12px;font-weight:500;word-break:break-word">${value}</span>
            </div>
          `;
        }
      },
      overwriteActions: true,
    };
  }

  // ── 4. Fetch + renderización via applyEdits ────────────────────────────────
  const fetchYRenderizar = useCallback(async (filtro = {}) => {
    setCargando(true);
    setError(null);

    try {
      const params = {};
      if (filtro.mmsi)        params.mmsi        = filtro.mmsi;
      if (filtro.nombreBuque) params.nombreBuque = filtro.nombreBuque;

      const res  = await apiClient.get("/viajes/mapa", { params });
      const data = res.data;

      setBuques(data);
      await renderizarFeatures(data);
    } catch (e) {
      const mensaje = e?.response?.data?.mensaje ?? e.message ?? "Error al consultar el mapa.";
      setError(mensaje);
    } finally {
      setCargando(false);
    }
  }, []);

  // ── 3. Actualizar el FeatureLayer via applyEdits y evaluar Geofencing
  async function renderizarFeatures(datos) {
    if (!featureLayerRef.current || !arcgisRef.current) return;
    const { Graphic, Point, geometryEngine, Polygon } = arcgisRef.current;
    
    // Limpiar rutas anteriores de la otra capa
    routeLayerRef.current?.removeAll();

    // Dibujar los polígonos de prueba/jurisdicciones en el mapa para que sean visibles
    for (const costera of costerasGeometriesRef.current) {
      for (const poly of costera.poligonosArcgis) {
        const isPolygon = poly.type === "polygon";
        routeLayerRef.current.add(new Graphic({
          geometry: poly,
          symbol: isPolygon ? {
            type: "simple-fill",
            color: [0, 170, 220, 20], // semi-transparent cyan
            outline: {
              color: [0, 170, 220, 150],
              width: 1.5,
              style: "dash"
            }
          } : {
            type: "simple-line",
            color: [0, 170, 220, 150],
            width: 2,
            style: "dash"
          }
        }));
      }
    }
    
    const nuevosGraphics = [];

    // Iteramos sobre los buques que vinieron del backend
    for (const buque of datos) {
      // 🛡️ BLINDAJE DE PROPIEDADES (Soporta PascalCase de .NET y camelCase de Mongo/JS)
      const bId         = buque.id ?? buque.Id ?? buque._id;
      const lon         = buque.longitud ?? buque.longitude ?? buque.Longitude;
      const lat         = buque.latitud ?? buque.latitude ?? buque.Latitude;
      const costeraReal = buque.costeraId ?? buque.CosteraId;

      // Anti-crash para coordenadas nulas o en cero
      if (lon == null || lat == null || (lon === 0 && lat === 0) || !bId) {
         continue; 
      }

      const punto = new Point({
        type: "point",
        longitude: lon,
        latitude : lat,
        spatialReference: { wkid: 4326 }
      });

      // Inicializar la caché geográfica por barco usando la BD como fuente de la verdad
      if (jurisdiccionPreviaRef.current[bId] === undefined) {
         jurisdiccionPreviaRef.current[bId] = costeraReal;
      }

      // ── Detección de Geofencing en el Cliente ────
      if (geometryEngine && costerasGeometriesRef.current.length > 0) {
        for (const costera of costerasGeometriesRef.current) {
          const dentroDeJurisdiccion = costera.poligonosArcgis.some(
            poly => geometryEngine.intersects(geometryEngine.geodesicBuffer(poly, 50, "meters"), punto)
          );

          if (dentroDeJurisdiccion) {
            const previoId = jurisdiccionPreviaRef.current[bId];
            
            // ¡Si la jurisdicción espacial calculada difiere de la anterior, se gatilla el Handover!
            if (previoId !== costera.id) {
              if (alertasMostradasRef.current[bId] !== costera.id) {
                alertasMostradasRef.current[bId] = costera.id;
                console.warn(
                  `[Geofencing] ¡CRUCE DE FRONTERA DETECTADO! Buque "${buque.nombreBuque ?? buque.VesselName ?? bId}" → Entrando a Costera ${costera.id} (${costera.nombre}). Esperando confirmación del operador.`
                );
                setTransferenciaPendiente({
                  viajeId: bId.toString(),
                  nombreBuque: buque.nombreBuque ?? buque.VesselName ?? bId,
                  nuevaCosteraId: costera.id,
                  nombreCostera: costera.nombre,
                  previoId: previoId
                });
                notificarHandover(
                  buque.nombreBuque ?? buque.VesselName ?? bId,
                  costera.nombre,
                  punto
                );
              }
            }
            break; 
          }
        }
      }

      // Construimos el gráfico con tolerancia de tipado para los atributos del popup
      nuevosGraphics.push(new Graphic({
        geometry: punto,
        attributes: {
          ObjectID           : oidCounter.current++,
          id                 : bId,
          nombreBuque        : buque.nombreBuque ?? buque.VesselName ?? "DESCONOCIDO",
          mmsi               : buque.mmsi              ?? buque.MMSI ?? "",
          imo                : buque.imo               ?? buque.IMO ?? "",
          estadoNav          : buque.estadoNav         ?? buque.NavegationStatusDesc ?? "N/A",
          velocidad          : buque.velocidad         ?? buque.SpeedOverGroud ?? 0,
          rumbo              : buque.rumbo             ?? buque.CourseOverGround ?? 0,
          origen             : buque.origen            ?? buque.Origin ?? "",
          destino            : buque.destino           ?? buque.Destination ?? "",
          cantidadBarcazas   : buque.cantidadBarcazas  ?? 0,
          remolcador         : buque.remolcador        ?? "",
          ultimaActualizacion: buque.ultimaActualizacion ?? buque.msgTime ?? "",
          latitud            : lat,
          longitud           : lon,
        },
      }));
    }
    
    // 🔥 CONFIGURACIÓN QUIRÚRGICA DE REFRESH PARA CLIENT-SIDE FEATURE LAYER
    // Consultamos todos los gráficos viejos que están dibujados actualmente en el mapa
    const featureQuery = await featureLayerRef.current.queryFeatures();
    
    // Ejecutamos applyEdits de forma atómica: Borra lo viejo y mete lo nuevo en un solo viaje
    await featureLayerRef.current.applyEdits({
      deleteFeatures: featureQuery.features,
      addFeatures: nuevosGraphics
    });
  }

  // Función blindada para ejecutar la Alerta y el Popup
  const notificarHandover = (buqueNombre, nuevaCosteraNombre, puntoBuque) => {
    const view = viewRef.current;
    
    // Blindaje de ciclo de vida: Si la vista no está lista o el popup fue destruido, abortamos.
    if (!view || !view.popup || typeof view.popup.open !== 'function') {
      console.warn("[Geofencing] La vista o el widget Popup de ArcGIS no están inicializados.");
      return;
    }

    view.popup.open({
      title: "⚠️ Traspaso Operativo Detectado",
      content: `El buque <b class="text-[#002454]">${buqueNombre}</b> ingresó al sector de control de <b>${nuevaCosteraNombre}</b>. Iniciando el handover automático...`,
      location: puntoBuque
    });
  };

  // ── 6. Zoom + Highlight / Viboreo ─────────────────────────────────────────
  function zoomABuque(buque) {
    setBuqueSeleccion(buque.id);
    if (!viewRef.current || !arcgisRef.current) return;

    const { Point, Graphic, SimpleMarkerSymbol } = arcgisRef.current;

    const punto = new Point({ longitude: buque.longitud, latitude: buque.latitud });

    // Zoom con animación suave
    viewRef.current.goTo(
      { center: punto, zoom: 10 },
      { duration: 800, easing: "ease-in-out" }
    );

    // Abrir popup del feature correspondiente
    const fl = featureLayerRef.current;
    if (fl) {
      fl.queryFeatures({
        where         : `id = '${buque.id}'`,
        returnGeometry: true,
        outFields     : ["*"],
      }).then(async (result) => {
        if (result.features.length > 0 && viewRef.current) {
          const view = viewRef.current;
          await view.when();
          if (view.popup && typeof view.popup.open === "function") {
            view.popup.open({
              features: result.features,
              location: punto,
            });
          } else if (view.popup) {
            console.warn("[ArcGIS] view.popup.open no está disponible como función. Usando fallback de propiedades.");
            view.popup.features = result.features;
            view.popup.location = punto;
            view.popup.visible = true;
          }
        }
      });
    }

    // ── Anillo de viboreo (highlight temporal) ──────────────────────────────
    const highlightLayer = highlightLayerRef.current;
    if (!highlightLayer) return;

    // Anillo exterior — cyan pulsante
    const anilloExterior = new Graphic({
      geometry: punto,
      symbol  : new SimpleMarkerSymbol({
        style  : "circle",
        color  : [0, 0, 0, 0],                         // relleno transparente
        size   : "36px",
        outline: { color: [0, 229, 255, 220], width: 3.5 },
      }),
    });

    // Anillo interior — blanco semitransparente
    const anilloInterior = new Graphic({
      geometry: punto,
      symbol  : new SimpleMarkerSymbol({
        style  : "circle",
        color  : [0, 0, 0, 0],
        size   : "22px",
        outline: { color: [255, 255, 255, 160], width: 2 },
      }),
    });

    highlightLayer.addMany([anilloExterior, anilloInterior]);

    // Remover los anillos tras 3 segundos
    setTimeout(() => {
      highlightLayer.removeMany([anilloExterior, anilloInterior]);
    }, 3000);
  }

  // ── 7. Filtrado local + debounce backend ──────────────────────────────────
  const buquesFiltrados = buques.filter(b => {
    if (!filtroTexto) return true;
    const q = filtroTexto.toLowerCase();
    return (
      b.nombreBuque?.toLowerCase().includes(q) ||
      b.mmsi?.includes(q)
    );
  });

  useEffect(() => {
    if (!filtroTexto) {
      fetchYRenderizar();
      return;
    }
    const timer = setTimeout(() => {
      const esMmsi = /^\d+$/.test(filtroTexto.trim());
      fetchYRenderizar(
        esMmsi
          ? { mmsi: filtroTexto.trim() }
          : { nombreBuque: filtroTexto.trim() }
      );
    }, 600);
    return () => clearTimeout(timer);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [filtroTexto]);

  // ── 7.5 Simulación de Geofencing ──────────────────────────────────────────
  const simularCruce = () => {
    if (buques.length === 0) {
      console.warn("No hay buques disponibles para simular el cruce.");
      return;
    }

    // Identificar el buque objetivo (el seleccionado, o el primero de la lista)
    const targetId = buqueSeleccion || buques[0].id;
    const targetBuque = buques.find(b => b.id === targetId);

    if (!targetBuque) {
      console.warn("No se pudo encontrar el buque seleccionado para simular el cruce.");
      return;
    }

    // Buscar una costera de destino cuya ID difiera de la costera actual del buque
    const targetCosteraId = targetBuque.costeraId ?? targetBuque.CosteraId;
    const nuevaCostera = costerasGeometriesRef.current.find(c => c.id !== targetCosteraId);

    if (!nuevaCostera || nuevaCostera.poligonosArcgis.length === 0) {
      console.warn("No se encontró una costera de destino válida para simular el cruce.");
      return;
    }

    const poly = nuevaCostera.poligonosArcgis[0];
    let lonDestino, latDestino;

    if (poly.paths && poly.paths[0] && poly.paths[0][0]) {
      lonDestino = poly.paths[0][0][0];
      latDestino = poly.paths[0][0][1];
    } else if (poly.rings && poly.rings[0] && poly.rings[0][0]) {
      lonDestino = poly.rings[0][0][0];
      latDestino = poly.rings[0][0][1];
    } else {
      console.warn("La costera destino no tiene un formato de geometría (paths/rings) válido.");
      return;
    }

    const nuevosBuques = buques.map((b) => {
      if (b.id === targetId) {
        return {
          ...b,
          latitud: latDestino,
          longitud: lonDestino,
          nombreBuque: `${b.nombreBuque ?? "Buque"} (PASADO A ${nuevaCostera.nombre})`,
        };
      }
      return b;
    });

    console.warn(`Simulando paso de jurisdicción para el buque ${targetBuque.nombreBuque ?? targetId} a ${nuevaCostera.nombre} (latitud ${latDestino}, longitud ${lonDestino})`);
    setBuques(nuevosBuques);
    renderizarFeatures(nuevosBuques);
  };

  // ── 8. Render JSX ─────────────────────────────────────────────────────────
  return (
    <div style={styles.wrapper}>

      {/* Panel lateral */}
      <aside style={{ ...styles.panel, width: panelAbierto ? 300 : 44 }}>

        {/* Botón colapsar */}
        <button
          style={styles.collapseBtn}
          onClick={() => setPanelAbierto(v => !v)}
          title={panelAbierto ? "Ocultar panel" : "Mostrar panel"}
        >
          {panelAbierto ? "◀" : "▶"}
        </button>

        {panelAbierto && (
          <>
            {/* Header institucional PNA */}
            <div style={styles.panelHeader}>
              <div style={{ display: "flex", alignItems: "center", gap: 8 }}>
                <div style={styles.pnaEscudo}>PNA</div>
                <span style={styles.panelTitulo}>Buques AIS</span>
              </div>
              <span style={styles.panelConteo}>{buquesFiltrados.length} buque(s)</span>
            </div>

            {/* Buscador */}
            <div style={styles.buscadorWrap}>
              <input
                type="text"
                placeholder="Buscar por nombre o MMSI…"
                value={filtroTexto}
                onChange={e => setFiltroTexto(e.target.value)}
                style={styles.buscador}
              />
              {filtroTexto && (
                <button style={styles.clearBtn} onClick={() => setFiltroTexto("")}>✕</button>
              )}
            </div>

            {/* Estado */}
            {cargando && <p style={styles.msgEstado}>Cargando datos AIS…</p>}
            {error    && <p style={{ ...styles.msgEstado, color: "#ff4d4f" }}>{error}</p>}

            {/* Lista de buques */}
            <ul style={styles.listaBuques}>
              {buquesFiltrados.map(b => (
                <li
                  key={b.id}
                  style={{
                    ...styles.itemBuque,
                    background: buqueSeleccion === b.id
                      ? "rgba(16,74,142,.08)"
                      : "transparent",
                    borderLeft: buqueSeleccion === b.id
                      ? "3px solid #104a8e"
                      : "3px solid transparent",
                  }}
                  onClick={() => zoomABuque(b)}
                >
                  <div style={styles.itemTop}>
                    <span style={styles.nombreBuque}>{b.nombreBuque}</span>
                    <EstadoChip estado={b.estadoNav} />
                  </div>
                  <div style={styles.itemBot}>
                    {b.origen && b.destino
                      ? `${b.origen} ➔ ${b.destino}`
                      : b.origen ?? b.destino ?? "Ruta desconocida"}
                  </div>
                  {b.cantidadBarcazas > 0 && (
                    <div style={styles.itemMeta}>
                      ⚓ {b.cantidadBarcazas} barcaza(s)
                      {b.remolcador ? ` · 🚤 ${b.remolcador}` : ""}
                    </div>
                  )}
                </li>
              ))}
              {!cargando && buquesFiltrados.length === 0 && (
                <li style={styles.msgVacio}>Sin resultados.</li>
              )}
            </ul>

            {/* Leyenda */}
            <div style={styles.leyenda}>
              {Object.entries(ESTADO_COLOR)
                .filter(([k]) => k !== "default")
                .map(([estado, rgb]) => (
                  <span key={estado} style={styles.leyendaItem}>
                    <span style={{ ...styles.leyendaDot, background: `rgb(${rgb.join(",")})` }} />
                    {estado}
                  </span>
                ))}
            </div>
          </>
        )}
      </aside>

      {/* Mapa ArcGIS */}
      <div ref={mapDiv} style={styles.mapa} />

      {/* Botón de simulación de cruce temporal */}
      <button
        style={styles.simularBtn}
        onClick={simularCruce}
        title="Simular Cruce de Buque para Geofencing"
      >
        🐞 Simular Cruce
      </button>

      {/* Botón refresh flotante */}
      <button
        style={{
          ...styles.refreshBtn,
          background: cargando ? "#6b7280" : "#104a8e",
        }}
        onClick={() => fetchYRenderizar(
          filtroTexto
            ? (/^\d+$/.test(filtroTexto) ? { mmsi: filtroTexto } : { nombreBuque: filtroTexto })
            : {}
        )}
        title="Actualizar posiciones"
        disabled={cargando}
      >
        {cargando ? "⟳" : "↻"}
      </button>

      {/* Modal de Confirmación de Handover de Jurisdicción */}
      {transferenciaPendiente && (
        <div className="fixed inset-0 z-[9999] flex items-center justify-center bg-black/60 backdrop-blur-sm">
          <div className="bg-white border border-gray-200 rounded-2xl shadow-2xl w-full max-w-[480px] overflow-hidden">
            {/* Encabezado */}
            <div className="bg-[#002454] text-white px-6 py-4 flex items-center gap-3">
              <div className="w-10 h-10 bg-[#104a8e] rounded-full flex items-center justify-center text-white text-lg font-bold shrink-0">
                ⚠️
              </div>
              <div>
                <h3 className="font-bold text-base leading-tight">Handover Automático Detectado</h3>
                <p className="text-xs text-blue-200 mt-0.5">Control de Tránsito Marítimo (MBPC)</p>
              </div>
            </div>
            
            {/* Contenido */}
            <div className="p-6 space-y-4">
              <p className="text-gray-700 text-sm leading-relaxed">
                El motor de Geofencing ha detectado que el buque <strong className="text-[#002454] font-semibold">{transferenciaPendiente.nombreBuque}</strong> ha salido de su jurisdicción actual e ingresó al polígono de la costera:
              </p>
              
              <div className="bg-gray-50 border border-gray-200 rounded-xl p-4 flex flex-col gap-2">
                <div className="flex justify-between items-center text-xs">
                  <span className="text-gray-500">Destino de Transferencia</span>
                  <span className="font-bold text-emerald-600 bg-emerald-50 px-2 py-0.5 rounded border border-emerald-200">
                    {transferenciaPendiente.nombreCostera} ({transferenciaPendiente.nuevaCosteraId})
                  </span>
                </div>
                <div className="h-px bg-gray-200 w-full" />
                <div className="flex justify-between items-center text-xs">
                  <span className="text-gray-500">ID de Viaje</span>
                  <span className="font-mono text-gray-700">{transferenciaPendiente.viajeId}</span>
                </div>
              </div>

              <p className="text-xs text-gray-400 italic">
                * Confirmar esta acción actualizará el registro regulatorio en Oracle y MongoDB. El buque dejará de ser visible en su panel operativo para asignarse a la costera de destino.
              </p>
            </div>
            
            {/* Botones */}
            <div className="bg-gray-50 px-6 py-4 flex items-center justify-end gap-3 border-t border-gray-150">
              <button
                type="button"
                onClick={handleCancelarTransferencia}
                className="px-4 py-2 border border-gray-300 text-gray-700 rounded-lg text-xs font-semibold hover:bg-gray-100 transition duration-150"
              >
                Rechazar Handover
              </button>
              <button
                type="button"
                onClick={handleConfirmarTransferencia}
                className="px-5 py-2 bg-[#104a8e] hover:bg-[#002454] text-white rounded-lg text-xs font-semibold shadow-md transition duration-150 flex items-center gap-1.5"
              >
                Confirmar y Traspasar
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}

// ── Sub-componente: chip de estado ───────────────────────────────────────────
function EstadoChip({ estado }) {
  const [r, g, b] = colorPorEstado(estado);
  return (
    <span style={{
      fontSize    : 10,
      fontWeight  : 600,
      padding     : "2px 7px",
      borderRadius: 99,
      background  : `rgba(${r},${g},${b},0.14)`,
      color       : `rgb(${r},${g},${b})`,
      border      : `1px solid rgba(${r},${g},${b},0.3)`,
      whiteSpace  : "nowrap",
    }}>
      {estado ?? "N/A"}
    </span>
  );
}

// ── Estilos CSS-in-JS ────────────────────────────────────────────────────────
const styles = {
  wrapper: {
    display   : "flex",
    height    : "100%",
    width     : "100%",
    position  : "relative",
    fontFamily: "'Segoe UI', system-ui, sans-serif",
    fontSize  : 13,
  },
  panel: {
    position     : "relative",
    height       : "100%",
    background   : "#fff",
    borderRight  : "1px solid #e4e7ef",
    display      : "flex",
    flexDirection: "column",
    overflow     : "hidden",
    transition   : "width .25s ease",
    flexShrink   : 0,
    zIndex       : 10,
    boxShadow    : "2px 0 12px rgba(0,36,84,.08)",
  },
  collapseBtn: {
    position  : "absolute",
    top       : 12,
    right     : 8,
    background: "none",
    border    : "none",
    cursor    : "pointer",
    fontSize  : 14,
    color     : "#888",
    padding   : "2px 4px",
    zIndex    : 1,
  },
  pnaEscudo: {
    width       : 28,
    height      : 28,
    borderRadius: "50%",
    background  : "#104a8e",
    color       : "#fff",
    display     : "flex",
    alignItems  : "center",
    justifyContent: "center",
    fontSize    : 9,
    fontWeight  : 700,
    flexShrink  : 0,
    border      : "1px solid rgba(255,255,255,.2)",
    boxShadow   : "0 1px 4px rgba(0,36,84,.3)",
  },
  panelHeader: {
    padding      : "14px 16px 10px",
    borderBottom : "1px solid #eef0f5",
    display      : "flex",
    justifyContent: "space-between",
    alignItems   : "center",
    background   : "#002454",
  },
  panelTitulo: {
    fontWeight: 600,
    fontSize  : 14,
    color     : "#ffffff",
  },
  panelConteo: {
    fontSize : 11,
    color    : "#93c5fd",
    marginRight: 24,
  },
  buscadorWrap: {
    position: "relative",
    padding : "8px 12px",
  },
  buscador: {
    width       : "100%",
    padding     : "7px 28px 7px 10px",
    border      : "1px solid #d1d5db",
    borderRadius: 7,
    fontSize    : 12,
    outline     : "none",
    boxSizing   : "border-box",
    background  : "#f9fafb",
    color       : "#1f2937",
  },
  clearBtn: {
    position : "absolute",
    right    : 20,
    top      : "50%",
    transform: "translateY(-50%)",
    background: "none",
    border   : "none",
    cursor   : "pointer",
    color    : "#aaa",
    fontSize : 12,
  },
  msgEstado: {
    padding : "8px 16px",
    margin  : 0,
    color   : "#9ca3af",
    fontSize: 12,
  },
  listaBuques: {
    listStyle: "none",
    margin   : 0,
    padding  : "0 0 60px",
    overflowY: "auto",
    flex     : 1,
  },
  itemBuque: {
    padding      : "10px 16px",
    cursor       : "pointer",
    borderBottom : "1px solid #f3f4f6",
    transition   : "background .15s, border-left .15s",
    paddingLeft  : 13,
  },
  itemTop: {
    display       : "flex",
    justifyContent: "space-between",
    alignItems    : "center",
    marginBottom  : 3,
  },
  nombreBuque: {
    fontWeight: 600,
    color     : "#1e3a5f",
    fontSize  : 13,
  },
  itemBot: {
    color       : "#6b7280",
    fontSize    : 11,
    overflow    : "hidden",
    textOverflow: "ellipsis",
    whiteSpace  : "nowrap",
  },
  itemMeta: {
    color    : "#9ca3af",
    fontSize : 10,
    marginTop: 2,
  },
  msgVacio: {
    padding  : "16px",
    color    : "#aaa",
    fontSize : 12,
    textAlign: "center",
  },
  leyenda: {
    position  : "absolute",
    bottom    : 0,
    left      : 0,
    right     : 0,
    background: "#fff",
    borderTop : "1px solid #eef0f5",
    padding   : "8px 16px",
    display   : "flex",
    gap       : 12,
    flexWrap  : "wrap",
  },
  leyendaItem: {
    display   : "flex",
    alignItems: "center",
    gap       : 4,
    fontSize  : 11,
    color     : "#4b5563",
  },
  leyendaDot: {
    width       : 8,
    height      : 8,
    borderRadius: "50%",
    display     : "inline-block",
  },
  mapa: {
    flex  : 1,
    height: "100%",
  },
  refreshBtn: {
    position      : "absolute",
    bottom        : 24,
    right         : 24,
    width         : 44,
    height        : 44,
    borderRadius  : "50%",
    color         : "#fff",
    border        : "none",
    cursor        : "pointer",
    fontSize      : 22,
    display       : "flex",
    alignItems    : "center",
    justifyContent: "center",
    boxShadow     : "0 2px 10px rgba(0,36,84,.35)",
    zIndex        : 20,
    transition    : "background .2s, opacity .2s",
  },
  simularBtn: {
    position      : "absolute",
    top           : 24,
    right         : 24,
    background    : "#ff4d4f",
    color         : "#fff",
    border        : "none",
    borderRadius  : 6,
    padding       : "8px 14px",
    fontWeight    : "bold",
    cursor        : "pointer",
    boxShadow     : "0 2px 8px rgba(255,77,79,0.35)",
    zIndex        : 25,
    transition    : "background .2s",
  },
};
