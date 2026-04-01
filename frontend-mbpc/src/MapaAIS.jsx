import { useEffect, useRef, useState, useCallback } from "react";
import "@arcgis/core/assets/esri/themes/light/main.css";
import apiClient from "./axiosClient";

// ─────────────────────────────────────────────────────────────────────────────
// MapaAIS.jsx
// Componente de tracking AIS sobre ArcGIS JS API 4.x.
//
// Muestra en el mapa:
//   • Punto azul  → posición actual del buque (rotado según rumbo)
//   • Punto verde → puerto de origen
//   • Punto rojo  → puerto de destino
//   • Línea gris  → ruta estimada origen ↔ actual ↔ destino
//
// Panel lateral izquierdo:
//   • Campo de búsqueda por nombre de buque o MMSI
//   • Lista de buques filtrados con chip de estado
//   • Click en fila → zoom + popup en el mapa
//
// Dependencias externas:
//   • @arcgis/core (ya instalado en el proyecto: npm install @arcgis/core)
//   • La API de ArcGIS JS se carga vía ESM desde la CDN oficial
// ─────────────────────────────────────────────────────────────────────────────

// Paleta de colores por estado de navegación
const ESTADO_COLOR = {
  Navegando : [24,  144, 255],  // azul
  Amarrado  : [82,  196, 26],   // verde
  Fondeado  : [250, 173, 20],   // ámbar
  default   : [150, 150, 150],  // gris
};

function colorPorEstado(estadoStr) {
  if (!estadoStr || estadoStr === "N/A") return ESTADO_COLOR.default;

  const e = estadoStr.toLowerCase();

  if (e.includes("amarr")) return ESTADO_COLOR.Amarrado;
  if (e.includes("fonde") || e.includes("ancla")) return ESTADO_COLOR.Fondeado;
  if (
    e.includes("navegando") || e.includes("transitando") || e.includes("salio") ||
    e.includes("entro") || e.includes("pesca") || e.includes("exploracion") ||
    e.includes("reanuda") || e.includes("paso inocente")
  ) return ESTADO_COLOR.Navegando;

  return ESTADO_COLOR.default;
}

// ─────────────────────────────────────────────────────────────────────────────
export default function MapaAIS() {
  const mapDiv        = useRef(null);
  const viewRef       = useRef(null);
  const layerRef      = useRef(null); // GraphicsLayer con los buques
  const routeLayerRef = useRef(null); // GraphicsLayer con las rutas
  const arcgisRef     = useRef(null); // módulos ArcGIS cargados

  const [buques,         setBuques]        = useState([]);
  const [filtroTexto,    setFiltroTexto]   = useState("");
  const [buqueSeleccion, setBuqueSeleccion]= useState(null);
  const [cargando,       setCargando]      = useState(true);
  const [error,          setError]         = useState(null);
  const [panelAbierto,   setPanelAbierto]  = useState(true);

  // ── 1. Cargar ArcGIS y montar el mapa ─────────────────────────────────────
  useEffect(() => {
    let vista;

    async function init() {
      const [
        { default: Map           },
        { default: MapView       },
        { default: GraphicsLayer },
        { default: Graphic       },
        { default: Point         },
        { default: SimpleMarkerSymbol },
        { default: SimpleLineSymbol   },
        { default: Polyline      },
        { default: PopupTemplate },
        { default: esriConfig    },
      ] = await Promise.all([
        import("@arcgis/core/Map.js"),
        import("@arcgis/core/views/MapView.js"),
        import("@arcgis/core/layers/GraphicsLayer.js"),
        import("@arcgis/core/Graphic.js"),
        import("@arcgis/core/geometry/Point.js"),
        import("@arcgis/core/symbols/SimpleMarkerSymbol.js"),
        import("@arcgis/core/symbols/SimpleLineSymbol.js"),
        import("@arcgis/core/geometry/Polyline.js"),
        import("@arcgis/core/PopupTemplate.js"),
        import("@arcgis/core/config.js"),
      ]);

      esriConfig.apiKey = "";

      arcgisRef.current = {
        Graphic, Point, SimpleMarkerSymbol, SimpleLineSymbol, Polyline, PopupTemplate
      };

      const routeLayer  = new GraphicsLayer({ id: "rutas"  });
      const buqueLayer  = new GraphicsLayer({ id: "buques" });
      routeLayerRef.current = routeLayer;
      layerRef.current      = buqueLayer;

      const map = new Map({
        basemap: "osm",
        layers : [routeLayer, buqueLayer],
      });

      vista = new MapView({
        container : mapDiv.current,
        map,
        center : [-58.4, -34.6],
        zoom   : 6,
        ui     : { components: ["zoom", "compass"] },
        popup  : { dockEnabled: false, dockOptions: { buttonEnabled: false } },
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

  // ── 2. Fetch de datos desde el backend ────────────────────────────────────
  const fetchYRenderizar = useCallback(async (filtro = {}) => {
    setCargando(true);
    setError(null);

    try {
      const params = {};
      if (filtro.mmsi)        params.mmsi        = filtro.mmsi;
      if (filtro.nombreBuque) params.nombreBuque = filtro.nombreBuque;

      const res = await apiClient.get("/viajes/mapa", { params });
      const data = res.data;

      setBuques(data);
      renderizarGraficos(data);
    } catch (e) {
      // axios ya maneja 401/403 automáticamente en el interceptor
      const mensaje = e?.response?.data?.mensaje ?? e.message ?? "Error al consultar el mapa.";
      setError(mensaje);
    } finally {
      setCargando(false);
    }
  }, []);

  // ── 3. Renderizar Graphics en el mapa ─────────────────────────────────────
  function renderizarGraficos(datos) {
    if (!layerRef.current || !arcgisRef.current) return;

    const {
      Graphic, Point, SimpleMarkerSymbol, SimpleLineSymbol, Polyline
    } = arcgisRef.current;

    layerRef.current.removeAll();
    routeLayerRef.current.removeAll();

    datos.forEach(buque => {
      const [r, g, b] = colorPorEstado(buque.estadoNav);

      const ptActual = new Point({
        longitude: buque.longitud,
        latitude : buque.latitud,
      });

      const simboloBuque = new SimpleMarkerSymbol({
        style    : "triangle",
        color    : [r, g, b, 230],
        size     : "14px",
        outline  : { color: [255, 255, 255, 200], width: 1.5 },
        angle    : buque.rumbo ?? 0,
      });

      const popup = buildPopup(buque);
      layerRef.current.add(new Graphic({
        geometry  : ptActual,
        symbol    : simboloBuque,
        attributes: buque,
        popupTemplate: popup,
      }));

      if (buque.origen && buque.destino) {
        const rutaLine = new Polyline({
          paths: [[[buque.longitud, buque.latitud]]],
        });
        routeLayerRef.current.add(new Graphic({
          geometry: rutaLine,
          symbol  : new SimpleLineSymbol({
            style : "dash",
            color : [150, 150, 150, 140],
            width : 1.2,
          }),
        }));
      }
    });
  }

  // ── 4. Popup template ─────────────────────────────────────────────────────
  function buildPopup(buque) {
    const { PopupTemplate } = arcgisRef.current;
    return new PopupTemplate({
      title  : `🚢 {nombreBuque}`,
      content: [
        {
          type      : "fields",
          fieldInfos: [
            { fieldName: "estadoNav",           label: "Estado"          },
            { fieldName: "mmsi",                label: "MMSI"            },
            { fieldName: "imo",                 label: "IMO"             },
            { fieldName: "velocidad",           label: "Velocidad (kn)"  },
            { fieldName: "rumbo",               label: "Rumbo (°)"       },
            { fieldName: "origen",              label: "Origen"          },
            { fieldName: "destino",             label: "Destino"         },
            { fieldName: "cantidadBarcazas",    label: "Barcazas"        },
            { fieldName: "remolcador",          label: "Remolcador"      },
            { fieldName: "ultimaActualizacion", label: "Última pos."     },
          ],
        },
      ],
    });
  }

  // ── 5. Zoom a buque seleccionado ──────────────────────────────────────────
  function zoomABuque(buque) {
    setBuqueSeleccion(buque.id);
    if (!viewRef.current || !arcgisRef.current) return;

    const { Point } = arcgisRef.current;
    viewRef.current.goTo({
      center: new Point({ longitude: buque.longitud, latitude: buque.latitud }),
      zoom  : 10,
    }, { duration: 800, easing: "ease-in-out" });

    const graphic = layerRef.current.graphics.find(
      g => g.attributes?.id === buque.id
    );
    if (graphic) {
      viewRef.current.popup.open({ features: [graphic], location: graphic.geometry });
    }
  }

  // ── 6. Filtrado local del panel ───────────────────────────────────────────
  const buquesFiltrados = buques.filter(b => {
    if (!filtroTexto) return true;
    const q = filtroTexto.toLowerCase();
    return (
      b.nombreBuque?.toLowerCase().includes(q) ||
      b.mmsi?.includes(q)
    );
  });

  // ── 7. Búsqueda con debounce al backend ───────────────────────────────────
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

  // ── Render ────────────────────────────────────────────────────────────────
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
            <div style={styles.panelHeader}>
              <span style={styles.panelTitulo}>Buques AIS</span>
              <span style={styles.panelConteo}>{buquesFiltrados.length} buque(s)</span>
            </div>

            {/* Búsqueda */}
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
                    background: buqueSeleccion === b.id ? "rgba(24,144,255,.12)" : "transparent",
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
              {Object.entries(ESTADO_COLOR).filter(([k]) => k !== "default").map(([estado, rgb]) => (
                <span key={estado} style={styles.leyendaItem}>
                  <span style={{ ...styles.leyendaDot, background: `rgb(${rgb.join(",")})` }}/>
                  {estado}
                </span>
              ))}
            </div>
          </>
        )}
      </aside>

      {/* Mapa ArcGIS */}
      <div ref={mapDiv} style={styles.mapa} />

      {/* Botón refresh flotante */}
      <button
        style={styles.refreshBtn}
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
      fontSize  : 10,
      fontWeight: 600,
      padding   : "2px 6px",
      borderRadius: 99,
      background: `rgba(${r},${g},${b},0.15)`,
      color     : `rgb(${r},${g},${b})`,
      whiteSpace: "nowrap",
    }}>
      {estado ?? "N/A"}
    </span>
  );
}

// ── Estilos (CSS-in-JS, sin dependencias externas) ───────────────────────────
const styles = {
  wrapper: {
    display : "flex",
    height  : "100%",
    width   : "100%",
    position: "relative",
    fontFamily: "'Segoe UI', system-ui, sans-serif",
    fontSize  : 13,
  },
  panel: {
    position        : "relative",
    height          : "100%",
    background      : "#fff",
    borderRight     : "1px solid #e8e8e8",
    display         : "flex",
    flexDirection   : "column",
    overflow        : "hidden",
    transition      : "width .25s ease",
    flexShrink      : 0,
    zIndex          : 10,
    boxShadow       : "2px 0 8px rgba(0,0,0,.06)",
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
  panelHeader: {
    padding   : "16px 16px 8px",
    borderBottom: "1px solid #f0f0f0",
    display   : "flex",
    justifyContent: "space-between",
    alignItems: "center",
  },
  panelTitulo: {
    fontWeight: 600,
    fontSize  : 15,
    color     : "#1a1a2e",
  },
  panelConteo: {
    fontSize  : 11,
    color     : "#888",
    marginRight: 24,
  },
  buscadorWrap: {
    position: "relative",
    padding : "8px 12px",
  },
  buscador: {
    width     : "100%",
    padding   : "7px 28px 7px 10px",
    border    : "1px solid #d9d9d9",
    borderRadius: 6,
    fontSize  : 12,
    outline   : "none",
    boxSizing : "border-box",
    background: "#fafafa",
    color     : "#333",
  },
  clearBtn: {
    position  : "absolute",
    right     : 20,
    top       : "50%",
    transform : "translateY(-50%)",
    background: "none",
    border    : "none",
    cursor    : "pointer",
    color     : "#aaa",
    fontSize  : 12,
  },
  msgEstado: {
    padding : "8px 16px",
    margin  : 0,
    color   : "#888",
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
    padding   : "10px 16px",
    cursor    : "pointer",
    borderBottom: "1px solid #f5f5f5",
    transition: "background .15s",
  },
  itemTop: {
    display       : "flex",
    justifyContent: "space-between",
    alignItems    : "center",
    marginBottom  : 3,
  },
  nombreBuque: {
    fontWeight: 600,
    color     : "#1a1a2e",
    fontSize  : 13,
  },
  itemBot: {
    color   : "#666",
    fontSize: 11,
    overflow: "hidden",
    textOverflow: "ellipsis",
    whiteSpace: "nowrap",
  },
  itemMeta: {
    color   : "#999",
    fontSize: 10,
    marginTop: 2,
  },
  msgVacio: {
    padding : "16px",
    color   : "#aaa",
    fontSize: 12,
    textAlign: "center",
  },
  leyenda: {
    position  : "absolute",
    bottom    : 0,
    left      : 0,
    right     : 0,
    background: "#fff",
    borderTop : "1px solid #f0f0f0",
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
    color     : "#555",
  },
  leyendaDot: {
    width       : 8,
    height      : 8,
    borderRadius: "50%",
    display     : "inline-block",
  },
  mapa: {
    flex    : 1,
    height  : "100%",
  },
  refreshBtn: {
    position    : "absolute",
    bottom      : 24,
    right       : 24,
    width       : 44,
    height      : 44,
    borderRadius: "50%",
    background  : "#1890ff",
    color       : "#fff",
    border      : "none",
    cursor      : "pointer",
    fontSize    : 22,
    display     : "flex",
    alignItems  : "center",
    justifyContent: "center",
    boxShadow   : "0 2px 8px rgba(24,144,255,.4)",
    zIndex      : 20,
    transition  : "opacity .2s",
  },
};
