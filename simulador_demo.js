/**
 * MBPC - PANEL INTERACTIVO DE DEMOSTRACIÓN DE GEOFENCING Y TRASPASOS
 * 
 * Uso:
 * node simulador_demo.js
 */

const readline = require('node:readline/promises');
const { stdin: input, stdout: output } = require('node:process');

const BACKEND_URL = 'http://127.0.0.1:5000';

// API Key de AisStream.io para ingesta real
const AIS_API_KEY = "021d6b3b11b2bca9d624b6a2727b0825cdb72c3e";

// Bounding Box ampliado para Sudamérica (Paraná / Río de la Plata)
const boundingBoxArgentina = [
    [
        [-39.0, -66.0], // Suroeste (Bahía Blanca / San Antonio Oeste)
        [-20.0, -45.0]  // Noreste (Río de Janeiro / Asunción / Iguazú)
    ]
];

// Presets de coordenadas para demostraciones rápidas
const PRESETS = [
    { name: "Rosario (ROSA - Costera 422)", lat: -32.9435, lng: -60.6334 },
    { name: "Gualeguaychú (GYCH - Costera 425)", lat: -33.0160, lng: -58.5058 },
    { name: "San Nicolás (SNIC - Costera 467)", lat: -33.2265, lng: -60.3204 },
    { name: "Tigre (TIGR - Costera 484)", lat: -34.3552, lng: -58.5239 },
    { name: "La Plata (LPLA - Costera 409)", lat: -34.8638, lng: -57.8977 }
];

// Colores para consola
const colors = {
    reset: "\x1b[0m",
    bright: "\x1b[1m",
    green: "\x1b[32m",
    yellow: "\x1b[33m",
    blue: "\x1b[34m",
    cyan: "\x1b[36m",
    red: "\x1b[31m",
    magenta: "\x1b[35m"
};

async function getViajesActivos() {
    try {
        const res = await fetch(`${BACKEND_URL}/api/simulador/viajes-activos`);
        if (!res.ok) return [];
        return await res.json();
    } catch (e) {
        return [];
    }
}

async function menu() {
    const rl = readline.createInterface({ input, output });
    
    while (true) {
        console.clear();
        console.log(`${colors.bright}${colors.blue}================================================================${colors.reset}`);
        console.log(`${colors.bright}${colors.cyan}             MBPC - CONSOLA DE DEMOSTRACIÓN EN VIVO             ${colors.reset}`);
        console.log(`${colors.bright}${colors.blue}================================================================${colors.reset}`);
        console.log(`1. 🚢 Listar Viajes Activos en MongoDB`);
        console.log(`2. ➕ Registrar Nuevo Buque de Prueba`);
        console.log(`3. 📍 Mover Buque (Cruzar Geocerca / Gatillar Alarma en vivo)`);
        console.log(`4. ⚡ Forzar Traspaso Pendiente (Ignorar Coordenadas)`);
        console.log(`5. 🛰️  Activar Ingesta/Puente AIS en Tiempo Real (AisStream.io)`);
        console.log(`6. 🔄 Sincronizar Posiciones desde Oracle AIS (Base de Datos Backend)`);
        console.log(`7. ❌ Salir`);
        console.log(`${colors.blue}----------------------------------------------------------------${colors.reset}`);
        
        const option = await rl.question(`${colors.bright}Selecciona una opción (1-7): ${colors.reset}`);
        
        if (option === '1') {
            await handleListarViajes(rl);
        } else if (option === '2') {
            await handleRegistrarBuque(rl);
        } else if (option === '3') {
            await handleMoverBuque(rl);
        } else if (option === '4') {
            await handleForzarTraspaso(rl);
        } else if (option === '5') {
            await handleAisStream(rl);
        } else if (option === '6') {
            await handleSincronizarOracleAis(rl);
        } else if (option === '7') {
            console.log(`\n¡Gracias por utilizar el Simulador de Demostración MBPC!\n`);
            break;
        } else {
            await rl.question(`\n${colors.red}Opción inválida.${colors.reset} Presiona Enter para continuar...`);
        }
    }
    
    rl.close();
}

async function handleListarViajes(rl) {
    console.log(`\n${colors.cyan}Consultando viajes activos en base de datos...${colors.reset}`);
    const viajes = await getViajesActivos();
    
    if (viajes.length === 0) {
        console.log(`\n${colors.yellow}No hay viajes activos registrados en el simulador.${colors.reset}`);
    } else {
        console.log(`\n${colors.bright}Viajes Activos:${colors.reset}`);
        console.table(viajes.map(v => ({
            "TravelId / MMSI": v.travelId,
            "Nombre Buque": v.vesselName,
            "Estado": v.navegationStatusDesc,
            "Jurisdicción Asignada": v.costeraId
        })));
    }
    await rl.question(`\nPresiona Enter para volver al menú...`);
}

async function handleRegistrarBuque(rl) {
    console.log(`\n${colors.bright}${colors.green}=== REGISTRAR NUEVO BUQUE ===${colors.reset}`);
    
    const mmsi = await rl.question(`MMSI / TravelId (ej: 701006867): `);
    const nombre = (await rl.question(`Nombre del Buque (ej: SABATER R): `)).toUpperCase().trim();
    const costera = await rl.question(`Jurisdicción Inicial (ej: 422 para Rosario, 425 para GYCH): `);
    
    console.log(`\nUbicación inicial de partida (Presets):`);
    PRESETS.forEach((p, idx) => {
        console.log(`  ${idx + 1}. ${p.name} [${p.lat}, ${p.lng}]`);
    });
    console.log(`  ${PRESETS.length + 1}. Personalizar Coordenadas`);
    
    const posChoice = await rl.question(`Selecciona ubicación inicial (1-${PRESETS.length + 1}): `);
    let lat, lng, origin;
    
    const idx = parseInt(posChoice, 10) - 1;
    if (idx >= 0 && idx < PRESETS.length) {
        lat = PRESETS[idx].lat;
        lng = PRESETS[idx].lng;
        origin = PRESETS[idx].name.split(' ')[0];
    } else {
        lat = parseFloat(await rl.question(`Latitud (ej: -33.0159): `));
        lng = parseFloat(await rl.question(`Longitud (ej: -58.5058): `));
        origin = "PERSONALIZADA";
    }

    const payload = {
        travelId: parseInt(mmsi, 10),
        nombreBuque: nombre,
        mmsi: mmsi,
        imo: 0,
        callSign: "AIS-DEMO",
        latitud: lat,
        longitud: lng,
        origen: origin,
        destino: "RECALADA",
        velocidad: 8.0,
        curso: 120.0,
        costeraId: parseInt(costera, 10)
    };

    try {
        const res = await fetch(`${BACKEND_URL}/api/simulador/insertar-buque-real`, {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify(payload)
        });

        if (res.ok) {
            console.log(`\n${colors.green}✅ ¡Buque registrado con éxito!${colors.reset}`);
            console.log(`El buque ya es visible en el dashboard React.`);
        } else {
            const txt = await res.text();
            console.error(`\n❌ Error al registrar: ${res.status} ${res.statusText}\n${txt}`);
        }
    } catch (e) {
        console.error(`\n❌ Error de conexión: ${e.message}`);
    }
    
    await rl.question(`\nPresiona Enter para continuar...`);
}

async function handleMoverBuque(rl) {
    console.log(`\n${colors.bright}${colors.yellow}=== SIMULAR MOVIMIENTO Y CRUCE DE JURISDICCIÓN ===${colors.reset}`);
    const viajes = await getViajesActivos();
    
    if (viajes.length === 0) {
        console.log(`\n${colors.yellow}No hay viajes activos registrados. Registra uno primero.${colors.reset}`);
        await rl.question(`\nPresiona Enter para continuar...`);
        return;
    }

    console.log(`\nSelecciona el buque a mover:`);
    viajes.forEach((v, idx) => {
        console.log(`  ${idx + 1}. ${v.vesselName} (MMSI: ${v.travelId}) [Costera asignada: ${v.costeraId}]`);
    });
    
    const vesselChoice = await rl.question(`Selecciona buque (1-${viajes.length}): `);
    const selectedVessel = viajes[parseInt(vesselChoice, 10) - 1];
    
    if (!selectedVessel) {
        console.log(`\n${colors.red}Selección inválida.${colors.reset}`);
        await rl.question(`\nPresiona Enter para continuar...`);
        return;
    }

    console.log(`\nDestino del movimiento (Presets de Jurisdicción):`);
    PRESETS.forEach((p, idx) => {
        console.log(`  ${idx + 1}. ${p.name} [${p.lat}, ${p.lng}]`);
    });
    console.log(`  ${PRESETS.length + 1}. Personalizar Coordenadas`);
    
    const posChoice = await rl.question(`Selecciona destino (1-${PRESETS.length + 1}): `);
    let lat, lng;
    
    const idx = parseInt(posChoice, 10) - 1;
    if (idx >= 0 && idx < PRESETS.length) {
        lat = PRESETS[idx].lat;
        lng = PRESETS[idx].lng;
    } else {
        lat = parseFloat(await rl.question(`Latitud (ej: -33.0159): `));
        lng = parseFloat(await rl.question(`Longitud (ej: -58.5058): `));
    }

    const payload = {
        travelId: selectedVessel.travelId,
        latitud: lat,
        longitud: lng
    };

    console.log(`\nEnviando movimiento para ${selectedVessel.vesselName} a [${lat}, ${lng}]...`);

    try {
        const res = await fetch(`${BACKEND_URL}/api/simulador/simular-movimiento`, {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify(payload)
        });

        if (res.ok) {
            const data = await res.json();
            console.log(`\n${colors.green}✅ Movimiento enviado correctamente.${colors.reset}`);
            if (data.reconciliado) {
                console.log(`${colors.bright}${colors.magenta}🔔 ¡CRUCE DE GEOCERCA DETECTADO!${colors.reset}`);
                console.log(`El backend detectó que ingresó a una nueva costera.`);
                console.log(`Una tarjeta de transferencia está pendiente en la UI.`);
            } else {
                console.log(`El buque se movió pero sigue en aguas de su costera actual o fuera de polígonos conocidos.`);
            }
        } else {
            const txt = await res.text();
            console.error(`\n❌ Error del API: ${res.status} ${res.statusText}\n${txt}`);
        }
    } catch (e) {
        console.error(`\n❌ Error de conexión: ${e.message}`);
    }

    await rl.question(`\nPresiona Enter para continuar...`);
}

async function handleForzarTraspaso(rl) {
    console.log(`\n${colors.bright}${colors.magenta}=== FORZAR SOLICITUD DE TRASPASO ===${colors.reset}`);
    const viajes = await getViajesActivos();
    
    if (viajes.length === 0) {
        console.log(`\n${colors.yellow}No hay viajes activos registrados. Registra uno primero.${colors.reset}`);
        await rl.question(`\nPresiona Enter para continuar...`);
        return;
    }

    console.log(`\nSelecciona el buque:`);
    viajes.forEach((v, idx) => {
        console.log(`  ${idx + 1}. ${v.vesselName} (MMSI: ${v.travelId}) [Costera asignada: ${v.costeraId}]`);
    });
    
    const vesselChoice = await rl.question(`Selecciona buque (1-${viajes.length}): `);
    const selectedVessel = viajes[parseInt(vesselChoice, 10) - 1];
    
    if (!selectedVessel) {
        console.log(`\n${colors.red}Selección inválida.${colors.reset}`);
        await rl.question(`\nPresiona Enter para continuar...`);
        return;
    }

    const targetCostera = await rl.question(`ID de Costera Destino del traspaso (ej: 425 para GYCH, 467 para SNIC): `);

    const payload = {
        travelId: selectedVessel.travelId,
        nuevaCosteraId: parseInt(targetCostera, 10)
    };

    try {
        const res = await fetch(`${BACKEND_URL}/api/simulador/forzar-transferencia`, {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify(payload)
        });

        if (res.ok) {
            const data = await res.json();
            console.log(`\n${colors.green}✅ ${data.mensaje}${colors.reset}`);
            console.log(`Una tarjeta de transferencia de Rosario ➔ Costera ${targetCostera} aparecerá en el dashboard.`);
        } else {
            const txt = await res.text();
            console.error(`\n❌ Error del API: ${res.status} ${res.statusText}\n${txt}`);
        }
    } catch (e) {
        console.error(`\n❌ Error de conexión: ${e.message}`);
    }

    await rl.question(`\nPresiona Enter para continuar...`);
}

async function handleAisStream(rl) {
    console.log(`\n${colors.bright}${colors.cyan}=== INGESTAR TRÁFICO AIS EN VIVO (AisStream.io) ===${colors.reset}`);
    console.log(`1. Modo Descubrimiento: Listar todos los barcos fluviales reales que transmiten en este instante`);
    console.log(`2. Modo Puente: Vincular telemetría en tiempo real a un buque de la grilla`);
    console.log(`3. Volver al menú principal`);
    
    const choice = await rl.question(`Selecciona una opción (1-3): `);
    if (choice === '3') return;
    
    let modoDescubrimiento = choice === '1';
    let targetVessel = null;
    let targetMmsi = null;
    
    if (choice === '2') {
        const viajes = await getViajesActivos();
        if (viajes.length === 0) {
            console.log(`\n${colors.yellow}No hay viajes activos en la grilla para vincular.${colors.reset}`);
            await rl.question(`\nPresiona Enter para continuar...`);
            return;
        }
        
        console.log(`\nSelecciona el buque destino de la telemetría:`);
        viajes.forEach((v, idx) => {
            console.log(`  ${idx + 1}. ${v.vesselName} (MMSI: ${v.travelId})`);
        });
        
        const vesselChoice = await rl.question(`Selecciona buque (1-${viajes.length}): `);
        targetVessel = viajes[parseInt(vesselChoice, 10) - 1];
        if (!targetVessel) {
            console.log(`\n${colors.red}Selección inválida.${colors.reset}`);
            await rl.question(`\nPresiona Enter para continuar...`);
            return;
        }
        
        targetMmsi = targetVessel.mmsi?.trim() || targetVessel.travelId?.toString().trim();
        console.log(`\n🎯 [FILTRO] Vinculando telemetría del MMSI real ${targetMmsi} al viaje "${targetVessel.vesselName}"`);
    }
    
    console.log(`\n🔌 Conectando con AisStream.io...`);
    
    const socket = new WebSocket("wss://stream.aisstream.io/v0/stream");
    let active = true;
    const barcosVistos = new Set();
    
    socket.onopen = () => {
        console.log(`\n${colors.green}[OK] Conexión establecida con AisStream.io.${colors.reset}`);
        console.log(`[INFO] Suscribiéndose al stream regional de Sudamérica (Paraná / Río de la Plata)...`);
        
        const subscriptionMessage = {
            Apikey: AIS_API_KEY,
            BoundingBoxes: boundingBoxArgentina
        };
        
        socket.send(JSON.stringify(subscriptionMessage));
        console.log(`\n${colors.bright}${colors.yellow}📡 INGESTANDO TRANSMISIÓN EN TIEMPO REAL...${colors.reset}`);
        console.log(`${colors.cyan}>> Presiona ENTER en esta consola en cualquier momento para detener la ingesta y volver. <<${colors.reset}\n`);
    };
    
    socket.onmessage = async (event) => {
        if (!active) return;
        try {
            const rawText = (event.data && typeof event.data.text === 'function')
                ? await event.data.text()
                : event.data;
            const aisMessage = JSON.parse(rawText);
            
            const metadata = aisMessage.MetaData;
            const positionReport = aisMessage.Message?.PositionReport;
            
            if (!positionReport || positionReport.Latitude === undefined || positionReport.Longitude === undefined) return;
            
            const mmsiMensaje = metadata?.MMSI?.toString() || positionReport.UserId?.toString();
            if (!mmsiMensaje) return;
            
            const shipName = (metadata && metadata.ShipName) ? metadata.ShipName.trim() : "SIN NOMBRE";
            const lat = positionReport.Latitude;
            const lng = positionReport.Longitude;
            const sog = positionReport.SpeedOverGround || 0;
            
            if (modoDescubrimiento) {
                const claveBarco = `${mmsiMensaje}-${shipName}`;
                if (!barcosVistos.has(claveBarco)) {
                    barcosVistos.add(claveBarco);
                    console.log(`🚢 [DETECTADO] MMSI: ${mmsiMensaje.padEnd(10)} | Buque: "${shipName.padEnd(20)}" | Pos: [${lat.toFixed(5)}, ${lng.toFixed(5)}] | Vel: ${sog} kn`);
                }
            } else {
                if (targetMmsi && mmsiMensaje === targetMmsi) {
                    console.log(`🚢 Telemetría recibida para "${targetVessel.vesselName}": Pos [${lat.toFixed(5)}, ${lng.toFixed(5)}] | Vel ${sog} kn`);
                    
                    const payload = {
                        travelId: targetVessel.travelId,
                        latitud: lat,
                        longitud: lng
                    };
                    
                    try {
                        const postRes = await fetch(`${BACKEND_URL}/api/simulador/simular-movimiento`, {
                            method: "POST",
                            headers: { "Content-Type": "application/json" },
                            body: JSON.stringify(payload)
                        });
                        
                        if (postRes.ok) {
                            const postData = await postRes.json();
                            if (postData.reconciliado) {
                                console.log(`   ${colors.magenta}🔔 [GEOFENCING] ¡Cruce de geocerca detectado para ${targetVessel.vesselName}!${colors.reset}`);
                            } else {
                                console.log(`   [OK] Posición sincronizada en grilla.`);
                            }
                        }
                    } catch (e) {
                        console.warn(`   [ERROR API] No se pudo enviar al backend: ${e.message}`);
                    }
                }
            }
        } catch (e) {
            // Ignorar errores de parseo menores
        }
    };
    
    socket.onerror = (err) => {
        console.error(`\n❌ Error de WebSocket: ${err.message || err}`);
    };
    
    // Escuchamos el Enter para salir
    await rl.question("");
    active = false;
    socket.close();
    console.log(`\n🔌 Conexión con AisStream.io cerrada.`);
    await rl.question(`\nPresiona Enter para continuar...`);
}

async function handleSincronizarOracleAis(rl) {
    console.log(`\n${colors.bright}${colors.cyan}=== SINCRONIZAR POSICIONES DESDE ORACLE AIS ===${colors.reset}`);
    console.log(`Invocando el servicio de sincronización de Oracle AIS del backend...`);
    
    try {
        const res = await fetch(`${BACKEND_URL}/api/simulador/ejecutar-ingesta-ais`, {
            method: "POST"
        });
        
        if (res.ok) {
            const data = await res.json();
            console.log(`\n${colors.green}✅ Sincronización finalizada: ${data.mensaje || "Posiciones actualizadas."}${colors.reset}`);
        } else {
            const txt = await res.text();
            console.error(`\n❌ Error del API del backend: ${res.status} ${res.statusText}\n${txt}`);
        }
    } catch (e) {
        console.error(`\n❌ Error de conexión con el backend: ${e.message}`);
    }
    
    await rl.question(`\nPresiona Enter para continuar...`);
}

menu();
