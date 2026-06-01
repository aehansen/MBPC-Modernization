import { useEffect, useRef, useState, useCallback } from "react";
import "@arcgis/core/assets/esri/themes/light/main.css";
import apiClient from "./axiosClient";
import { useTransferirViaje } from "./hooks/useViajes";

// ─────────────────────────────────────────────────────────────────────────────
// MapaAIS.jsx  (v2 — FeatureLayer + Clustering + Popup Institucional + Viboreo)
//
// Cambios respecto a v1:
//   • buqueLayer migrado de GraphicsLayer → FeatureLayer client-side con
//     featureReduction clustering (clusterRadius 80px).
//   • Popup reemplazado por función content() que retorna HTML institucional PNA.
//   • zoomABuque ahora agrega un anillo cyan de "viboreo" sobre un GraphicsLayer
//     temporal y lo remueve a los 3 segundos.
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

// ── Polígonos de Jurisdicciones (Costeras) para Geofencing ───────────────────
const COSTERAS_POLIGONOS = [
  {
    id: 1,
    nombre: "Costera Río de la Plata Norte",
    rings: [
      [
        [-58.5, -34.0],
        [-57.8, -34.0],
        [-57.8, -34.8],
        [-58.5, -34.8],
        [-58.5, -34.0]
      ]
    ]
  },
  {
    id: 2,
    nombre: "Costera Río de la Plata Sur",
    rings: [
      [
        [-57.8, -34.0],
        [-57.0, -34.0],
        [-57.0, -34.8],
        [-57.8, -34.8],
        [-57.8, -34.0]
      ]
    ]
  }
];

// ─────────────────────────────────────────────────────────────────────────────
export default function MapaAIS() {
  const mapDiv         = useRef(null);
  const viewRef        = useRef(null);
  const featureLayerRef= useRef(null);  // FeatureLayer client-side (buques + clustering)
  const routeLayerRef  = useRef(null);  // GraphicsLayer (rutas)
  const highlightLayerRef = useRef(null); // GraphicsLayer temporal (anillo de viboreo)
  const arcgisRef      = useRef(null);
  const oidCounter     = useRef(1);     // ObjectID auto-incremental para el FeatureLayer
  const jurisdiccionPreviaRef = useRef({});
  const { mutate: transferirViaje } = useTransferirViaje();

  const [buques,         setBuques]        = useState([]);
  const [filtroTexto,    setFiltroTexto]   = useState("");
  const [buqueSeleccion, setBuqueSeleccion]= useState(null);
  const [cargando,       setCargando]      = useState(true);
  const [error,          setError]         = useState(null);
  const [panelAbierto,   setPanelAbierto]  = useState(true);

  // ── 1. Inicializar ArcGIS ──────────────────────────────────────────────────
  useEffect(() => {
    let vista;

    async function init() {
      const [
        { default: Map              },
        { default: MapView          },
        { default: GraphicsLayer    },
        { default: FeatureLayer     },
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
        import("@arcgis/core/Graphic.js"),
        import("@arcgis/core/geometry/Point.js"),
        import("@arcgis/core/symbols/SimpleMarkerSymbol.js"),
        import("@arcgis/core/symbols/SimpleLineSymbol.js"),
        import("@arcgis/core/geometry/Polyline.js"),
        import("@arcgis/core/config.js"),
        import("@arcgis/core/geometry/geometryEngine.js"),
        import("@arcgis/core/geometry/Polygon.js"),
      ]);

      esriConfig.apiKey = "";

      arcgisRef.current = {
        Graphic, Point, SimpleMarkerSymbol, SimpleLineSymbol, Polyline, FeatureLayer, geometryEngine, Polygon
      };

      // ── Rutas (GraphicsLayer estático) ──
      const routeLayer  = new GraphicsLayer({ id: "rutas" });
      routeLayerRef.current = routeLayer;

      // ── Highlight temporal (GraphicsLayer para el anillo de viboreo) ──
      const highlightLayer = new GraphicsLayer({ id: "highlight", listMode: "hide" });
      highlightLayerRef.current = highlightLayer;

      // ── FeatureLayer client-side para buques (habilita clustering) ──
      const buqueFeatureLayer = new FeatureLayer({
        id             : "buques",
        source         : [],          // Se poblará dinámicamente via applyEdits
        fields         : FIELDS_BUQUE,
        objectIdField  : "ObjectID",
        geometryType   : "point",
        spatialReference: { wkid: 4326 },
        renderer       : buildRenderer(SimpleMarkerSymbol),
        popupTemplate  : buildPopupTemplate(),
        featureReduction: FEATURE_REDUCTION_CLUSTER,
      });

      featureLayerRef.current = buqueFeatureLayer;

      const map = new Map({
        basemap: "osm",
        layers : [routeLayer, buqueFeatureLayer, highlightLayer],
      });

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
      await fetchYRenderizar();
    }

    init().catch(err => {
      console.error("Error cargando ArcGIS:", err);
      setError("No se pudo inicializar el mapa. Verificá la API key de ArcGIS.");
      setCargando(false);
    });

    return () => { vista?.destroy(); };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

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

  // ── 5. Actualizar el FeatureLayer via applyEdits ───────────────────────────
  async function renderizarFeatures(datos) {
    if (!featureLayerRef.current || !arcgisRef.current) return;

    const { Graphic, Point, geometryEngine, Polygon } = arcgisRef.current;

    // Limpiar rutas
    routeLayerRef.current?.removeAll();

    // Construir nuevos Graphics con atributos tipados
    const nuevosGraphics = datos.map(buque => {
      const punto = new Point({
        longitude: buque.longitud,
        latitude : buque.latitud,
        spatialReference: { wkid: 4326 }
      });

      // Detección de Geofencing para la transferencia de jurisdicción
      if (geometryEngine && Polygon) {
        for (const costera of COSTERAS_POLIGONOS) {
          const poligonoArcgis = new Polygon({
            rings: costera.rings,
            spatialReference: { wkid: 4326 }
          });

          if (geometryEngine.intersects(poligonoArcgis, punto)) {
            const previoId = jurisdiccionPreviaRef.current[buque.id];
            if (previoId !== costera.id) {
              jurisdiccionPreviaRef.current[buque.id] = costera.id;
              console.warn(`Transferencia automática disparada para el buque ${buque.nombreBuque ?? buque.id} hacia la Costera ${costera.id}`);
              transferirViaje({ viajeId: buque.id, nuevaCosteraId: costera.id });
            }
            break;
          }
        }
      }

      return new Graphic({
        geometry: punto,
        attributes: {
          ObjectID           : oidCounter.current++,
          id                 : buque.id,
          nombreBuque        : buque.nombreBuque       ?? "DESCONOCIDO",
          mmsi               : buque.mmsi              ?? "",
          imo                : buque.imo               ?? "",
          estadoNav          : buque.estadoNav         ?? "N/A",
          velocidad          : buque.velocidad         ?? 0,
          rumbo              : buque.rumbo             ?? 0,
          origen             : buque.origen            ?? "",
          destino            : buque.destino           ?? "",
          cantidadBarcazas   : buque.cantidadBarcazas  ?? 0,
          remolcador         : buque.remolcador        ?? "",
          ultimaActualizacion: buque.ultimaActualizacion ?? "",
          latitud            : buque.latitud,
          longitud           : buque.longitud,
        },
      });
    });

    // deleteFeatures: eliminar todos los existentes
    const layer = featureLayerRef.current;
    try {
      const existingResult = await layer.queryFeatures({ where: "1=1", returnGeometry: false });
      const deleteEdits    = existingResult.features.length > 0
        ? { deleteFeatures: existingResult.features }
        : {};

      await layer.applyEdits({
        ...deleteEdits,
        addFeatures: nuevosGraphics,
      });
    } catch (err) {
      console.error("applyEdits error:", err);
    }

    // Actualizar rutas en GraphicsLayer separado
    const { Polyline, Graphic: G2, SimpleLineSymbol } = arcgisRef.current;
    datos.forEach(buque => {
      if (buque.origen && buque.destino) {
        routeLayerRef.current.add(new G2({
          geometry: new Polyline({ paths: [[[buque.longitud, buque.latitud]]] }),
          symbol  : new SimpleLineSymbol({
            style: "dash",
            color: [150, 150, 150, 140],
            width: 1.2,
          }),
        }));
      }
    });
  }

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
      }).then(result => {
        if (result.features.length > 0) {
          viewRef.current?.popup.open({
            features: result.features,
            location: punto,
          });
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
    const nuevosBuques = buques.map((b, idx) => {
      if (idx === 0) {
        return {
          ...b,
          latitud: -34.5,
          longitud: -57.5,
          nombreBuque: `${b.nombreBuque ?? "Buque"} (SIMULADO)`,
        };
      }
      return b;
    });

    console.warn(`Simulando posición para el buque ${nuevosBuques[0].nombreBuque} en latitud -34.5, longitud -57.5`);
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
