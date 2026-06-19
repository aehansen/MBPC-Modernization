/**
 * Script de prueba de viabilidad: Integración de ArcGIS Feature Server (DependenciasPNA)
 * 
 * Este script simula cómo consumir el Feature Layer de ArcGIS y realizar una validación
 * local de geocercas (Point-in-Polygon) utilizando el algoritmo de Ray-Casting.
 * 
 * Para ejecutar este script en tu terminal:
 * node-v24.14.0-win-x64\node.exe C:\Users\aehansen\.gemini\antigravity\brain\a6062e2a-0cec-4aae-b4b7-fcf8a3533b91\scratch\test_arcgis_integration.js
 */

const https = require('https');

// ============================================================================
// CONFIGURACIÓN:
// Reemplaza esta URL con la "Service URL" que figura en el detalle del Item
// DependenciasPNA en tu portal de ArcGIS (suele terminar en /FeatureServer/0)
// ============================================================================
const ARCGIS_SERVICE_URL = "https://gis.prefecturanaval.gob.ar/server/rest/services/Hosted/DependenciasPNA/FeatureServer";

// Punto de prueba (por ejemplo: Posición de un buque en el Río Paraná frente a Rosario)
const BUQUE_TEST_COORDS = { lat: -32.94682, lng: -60.62744 };

/**
 * Algoritmo Ray-Casting para Point-in-Polygon (PIP)
 * Determina si un punto está dentro de un polígono.
 * 
 * @param {Array} point [lng, lat]
 * @param {Array} polygon Array de anillos de coordenadas [[lng, lat], [lng, lat], ...]
 */
function isPointInPolygon(point, polygon) {
    const x = point[0], y = point[1];
    let inside = false;
    for (let i = 0, j = polygon.length - 1; i < polygon.length; j = i++) {
        const xi = polygon[i][0], yi = polygon[i][1];
        const xj = polygon[j][0], yj = polygon[j][1];
        
        const intersect = ((yi > y) !== (yj > y))
            && (x < (xj - xi) * (y - yi) / (yj - yi) + xi);
        if (intersect) inside = !inside;
    }
    return inside;
}

/**
 * Consulta el servicio de ArcGIS para obtener las dependencias en formato GeoJSON.
 */
function fetchJurisdicciones(serviceUrl) {
    // Construimos la URL de consulta pidiendo GeoJSON y el sistema de coordenadas WGS84 (4326)
    const queryUrl = `${serviceUrl}/query?where=1%3D1&outFields=*&outSR=4326&f=geojson`;
    
    console.log(`[INFO] Consultando ArcGIS Feature Server en: ${serviceUrl}...`);
    
    return new Promise((resolve, reject) => {
        https.get(queryUrl, (res) => {
            let data = '';
            res.on('data', (chunk) => { data += chunk; });
            res.on('end', () => {
                try {
                    const geojson = JSON.parse(data);
                    if (geojson.error) {
                        reject(new Error(geojson.error.message || "Error en el servicio de ArcGIS"));
                    } else {
                        resolve(geojson);
                    }
                } catch (e) {
                    reject(new Error(`Error al parsear respuesta JSON de ArcGIS: ${e.message}`));
                }
            });
        }).on('error', (err) => {
            reject(err);
        });
    });
}

// Ejecución de la prueba
async function runTest() {
    console.log("=== INICIANDO VALIDACIÓN DE INTEGRACIÓN ARCGIS ===");
    console.log(`Posición del buque a probar: Lat ${BUQUE_TEST_COORDS.lat}, Lng ${BUQUE_TEST_COORDS.lng}\n`);

    try {
        // En un escenario real, aquí se consumirá la URL configurada.
        // Si no tienes acceso de red inmediato o la URL es un placeholder, 
        // simulamos un GeoJSON mockeado con una dependencia de ejemplo para comprobar la lógica.
        let geojson;
        if (ARCGIS_SERVICE_URL.includes("tu-portal-arcgis")) {
            console.log("[AVISO] Usando datos mockeados ya que ARCGIS_SERVICE_URL es un placeholder.");
            geojson = getMockGeoJson();
        } else {
            geojson = await fetchJurisdicciones(ARCGIS_SERVICE_URL);
        }

        const features = geojson.features || [];
        console.log(`[OK] Se recuperaron ${features.length} jurisdicciones.`);

        let jurisdiccionEncontrada = null;

        // Evaluamos cada feature (dependencia)
        for (const feature of features) {
            const nombreDependencia = feature.properties.Nombre || feature.properties.NAME || "Dependencia Desconocida";
            const geometry = feature.geometry;

            if (!geometry || (geometry.type !== "Polygon" && geometry.type !== "MultiPolygon")) {
                continue;
            }

            // Para simplificar, asumimos Polygon simple o el primer anillo de MultiPolygon en la prueba
            const polygons = geometry.type === "Polygon" 
                ? [geometry.coordinates] 
                : geometry.coordinates;

            let inThisJurisdiction = false;
            for (const rings of polygons) {
                // El primer anillo representa el límite exterior
                const outerRing = rings[0]; 
                if (isPointInPolygon([BUQUE_TEST_COORDS.lng, BUQUE_TEST_COORDS.lat], outerRing)) {
                    inThisJurisdiction = true;
                    break;
                }
            }

            if (inThisJurisdiction) {
                jurisdiccionEncontrada = nombreDependencia;
                break;
            }
        }

        if (jurisdiccionEncontrada) {
            console.log(`\n[RESULTADO] ¡Alarma disparada exitosamente!`);
            console.log(`El buque se encuentra dentro de la jurisdicción de: "${jurisdiccionEncontrada}"`);
        } else {
            console.log(`\n[RESULTADO] El buque está navegando en aguas internacionales o fuera de las dependencias registradas.`);
        }

    } catch (error) {
        console.error(`\n[ERROR] Falló la validación del servicio:`, error.message);
        console.log("\nRecomendaciones para solucionar este error:");
        console.log("1. Asegúrate de estar conectado a la red/VPN interna de PNA para acceder al portal.");
        console.log("2. Confirma la URL exacta del Feature Server desde el detalle del item en ArcGIS.");
    }
    console.log("\n=== FIN DE LA PRUEBA ===");
}

/**
 * Retorna un GeoJSON mock de prueba simulando la zona de Rosario (para pruebas locales offline).
 */
function getMockGeoJson() {
    return {
        type: "FeatureCollection",
        features: [
            {
                type: "Feature",
                properties: {
                    Nombre: "PREFECTURA ROSARIO",
                    CosteraId: 467
                },
                geometry: {
                    type: "Polygon",
                    coordinates: [[
                        [-60.65, -32.90],
                        [-60.60, -32.90],
                        [-60.60, -32.98],
                        [-60.65, -32.98],
                        [-60.65, -32.90] // Cerrar polígono
                    ]]
                }
            }
        ]
    };
}

runTest();
