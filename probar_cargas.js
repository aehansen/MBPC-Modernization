const BASE_URL = "http://localhost:5000";

// Formatting helpers
const logHeader = (text) => {
    console.log(`\x1b[36m\n=======================================================`);
    console.log(` ${text}`);
    console.log(`=======================================================\x1b[0m`);
};
const logSuccess = (text) => console.log(`  \x1b[32m[OK]\x1b[0m ${text}`);
const logFailure = (text) => console.log(`  \x1b[31m[FAIL]\x1b[0m ${text}`);
const logInfo = (text) => console.log(`  \x1b[33m[INFO]\x1b[0m ${text}`);

async function runTests() {
    let token = "";
    let headers = {};

    try {
        // ── STEP 1: AUTHENTICATION ──
        logHeader("PASO 1: Autenticación de Operador (Costera 0)");
        const authRes = await fetch(`${BASE_URL}/api/auth/login`, {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({ CosteraId: 0, Password: "testpassword" })
        });

        if (!authRes.ok) {
            throw new Error(`Fallo de autenticación: ${authRes.status} ${authRes.statusText}`);
        }

        const authData = await authRes.json();
        token = authData.token;
        headers = {
            "Authorization": `Bearer ${token}`,
            "Content-Type": "application/json"
        };
        logSuccess(`Autenticación exitosa. Token obtenido (expira en: ${authData.expiracion}).`);

        // ── STEP 2: CREATE FRESH TEST VOYAGES ──
        logHeader("PASO 2: Crear Viajes de Prueba Nuevos");
        
        logInfo("Creando Viaje A de prueba (EDERRA I)...");
        const resA = await fetch(`${BASE_URL}/api/viajes`, {
            method: "POST",
            headers,
            body: JSON.stringify({
                buqueId: 5000012,
                nombreBuque: "EDERRA I",
                origen: "Zárate",
                destino: "Buenos Aires",
                proximoPuntoControl: "Km 100",
                fechaPartida: new Date().toISOString(),
                eta: new Date(Date.now() + 86400000).toISOString(),
                declaracionMalvinas: 0
            })
        });
        if (!resA.ok) throw new Error(`Fallo al crear viaje A: ${resA.status}`);
        
        logInfo("Creando Viaje B de prueba (DON BENJAMIN)...");
        const resB = await fetch(`${BASE_URL}/api/viajes`, {
            method: "POST",
            headers,
            body: JSON.stringify({
                buqueId: 5000013,
                nombreBuque: "DON BENJAMIN",
                origen: "Zárate",
                destino: "Buenos Aires",
                proximoPuntoControl: "Km 120",
                fechaPartida: new Date().toISOString(),
                eta: new Date(Date.now() + 86400000).toISOString(),
                declaracionMalvinas: 0
            })
        });
        if (!resB.ok) throw new Error(`Fallo al crear viaje B: ${resB.status}`);

        // Fetch all voyages to get their IDs
        const viajesRes = await fetch(`${BASE_URL}/api/viajes?tamanio=100`, { headers });
        if (!viajesRes.ok) {
            throw new Error(`Error recuperando viajes: ${viajesRes.status}`);
        }
        const viajes = await viajesRes.json();

        // Find the newly created voyages (they will be the first ones/newest ones)
        let activeVoyagesA = viajes.filter(v => (v.nombreBuque === "EDERRA I" || v.buque === "5000012") && v.estadoActual !== "Finalizado");
        let activeVoyagesB = viajes.filter(v => (v.nombreBuque === "DON BENJAMIN" || v.buque === "5000013") && v.estadoActual !== "Finalizado");
        
        if (activeVoyagesA.length === 0 || activeVoyagesB.length === 0) {
            throw new Error("No se pudieron encontrar los viajes recién creados en la lista.");
        }

        // The newest ones will be at the beginning of the list because GetViajesAsync sorts by MsgTime descending
        let voyageA = activeVoyagesA[0];
        let voyageB = activeVoyagesB[0];

        const voyageAId = voyageA.id || voyageA.Id;
        const voyageBId = voyageB.id || voyageB.Id;

        logSuccess(`Viaje A (EDERRA I) listo con ID: ${voyageAId}`);
        logSuccess(`Viaje B (DON BENJAMIN) listo con ID: ${voyageBId}`);

        // ── STEP 3: TEST MAX CAPACITY VALIDATION (FAIL CASE) ──
        logHeader("PASO 3: Validación de Capacidad Máxima (Caso Fallido)");
        logInfo("Tratando de cargar 3000 Tn en barcaza RS001 (Capacidad = 2500 Tn)");
        const failCargaRes = await fetch(`${BASE_URL}/api/carga/viaje/${voyageAId}/agregar`, {
            method: "POST",
            headers,
            body: JSON.stringify({
                BarcazaId: 3000001,
                BarcazaNombre: "RS001",
                Tipo: "Barcaza",
                Tonelaje: 3000.0,
                MercaderiaId: 1,
                Unidad: "Tn"
            })
        });

        if (failCargaRes.ok) {
            logFailure("El backend aceptó una carga de 3000 Tn en una barcaza con capacidad de 2500 Tn!");
        } else {
            const body = await failCargaRes.text();
            if (body.includes("supera la capacidad máxima")) {
                logSuccess(`Validación correcta: El servidor rechazó la carga por exceder la capacidad. Respuesta: ${body}`);
            } else {
                logFailure(`Se rechazó la carga pero el mensaje no fue el de capacidad física: ${body}`);
            }
        }

        // ── STEP 4: ADD VALID CARGO (SUCCESS CASE) ──
        logHeader("PASO 4: Agregar Carga Válida (Caso Exitoso)");
        logInfo("Cargando 2000 Tn en barcaza RS001");
        const successCargaRes = await fetch(`${BASE_URL}/api/carga/viaje/${voyageAId}/agregar`, {
            method: "POST",
            headers,
            body: JSON.stringify({
                BarcazaId: 3000001,
                BarcazaNombre: "RS001",
                Tipo: "Barcaza",
                Tonelaje: 2000.0,
                MercaderiaId: 1,
                Unidad: "Tn"
            })
        });

        if (!successCargaRes.ok) {
            const errBody = await successCargaRes.text();
            throw new Error(`Fallo al agregar carga válida: ${errBody}`);
        }
        const successCargaData = await successCargaRes.json();
        logSuccess(`Carga agregada con éxito: ${successCargaData.mensaje}`);

        // ── STEP 5: HISTORICAL RECTIFICATION (SUCCESS CASE) ──
        logHeader("PASO 5: Rectificación Histórica de Carga (Caso Exitoso)");
        
        const getCargasRes = await fetch(`${BASE_URL}/api/carga/viaje/${voyageAId}`, { headers });
        if (!getCargasRes.ok) {
            throw new Error(`Error al recuperar cargas para Voyage A: ${getCargasRes.status}`);
        }
        const cargasA = await getCargasRes.json();
        const addedCargo = cargasA.find(c => c.descripcionLista.includes("RS001") || c.id === "3000001" || c.id === "RS001");
        if (!addedCargo) {
            throw new Error("No se pudo encontrar la barcaza agregada en la lista de cargas.");
        }
        const cargaId = addedCargo.id;
        logInfo(`Carga encontrada en base de datos. Usando cargaId = "${cargaId}" para las pruebas.`);

        logInfo(`Corrigiendo el tonelaje de la barcaza ${cargaId} a 2400 Tn`);
        const rectRes = await fetch(`${BASE_URL}/api/cargas/${voyageAId}/cargas/${cargaId}/rectificar`, {
            method: "PUT",
            headers,
            body: JSON.stringify({
                Tonelaje: 2400.0,
                Motivo: "Ajuste de báscula verificado por Aduana"
            })
        });

        if (!rectRes.ok) {
            const errBody = await rectRes.text();
            throw new Error(`Fallo al rectificar carga: ${errBody}`);
        }
        const rectData = await rectRes.json();
        logSuccess(`Rectificación exitosa: ${rectData.mensaje}`);

        // Verificar valor en MongoDB
        const getCargasRes2 = await fetch(`${BASE_URL}/api/carga/viaje/${voyageAId}`, { headers });
        const cargasA2 = await getCargasRes2.json();
        const cargaRS001 = cargasA2.find(c => c.id === cargaId);
        if (cargaRS001 && cargaRS001.tonelaje === 2400) {
            logSuccess(`Base de datos verificada: Tonelaje en MongoDB es de ${cargaRS001.tonelaje} Tn.`);
        } else {
            logFailure(`El tonelaje no coincide en base de datos. Obtenido: ${cargaRS001 ? cargaRS001.tonelaje : "No encontrada"}`);
        }

        // ── STEP 6: HISTORICAL RECTIFICATION (FAIL CASE - EXCEED CAPACITY) ──
        logHeader("PASO 6: Rectificación Histórica (Caso Fallido - Superar Capacidad)");
        logInfo("Intentando rectificar a 2900 Tn (Supera capacidad de 2500 Tn)");
        const failRectRes = await fetch(`${BASE_URL}/api/cargas/${voyageAId}/cargas/${cargaId}/rectificar`, {
            method: "PUT",
            headers,
            body: JSON.stringify({
                Tonelaje: 2900.0,
                Motivo: "Pesaje erróneo mayor a capacidad"
            })
        });

        if (failRectRes.ok) {
            logFailure("El backend aceptó una rectificación que supera la capacidad física de la barcaza!");
        } else {
            const body = await failRectRes.text();
            if (body.includes("supera la capacidad máxima")) {
                logSuccess(`Validación correcta: El servidor rechazó la rectificación. Respuesta: ${body}`);
            } else {
                logFailure(`Se rechazó pero por un motivo inesperado: ${body}`);
            }
        }

        // ── STEP 7: CARGO TRANSSHIPMENT (SUCCESS CASE) ──
        logHeader("PASO 7: Transbordo (Transferencia) de Cargas");
        logInfo("Transbordando 1000 Tn desde RS001 (Viaje A) hacia RS002 (Viaje B, Capacidad 2500 Tn)");
        const transRes = await fetch(`${BASE_URL}/api/cargas/${voyageAId}/cargas/${cargaId}/transferir`, {
            method: "POST",
            headers,
            body: JSON.stringify({
                DestinoViajeId: voyageBId,
                DestinoBarcazaId: 3000002,
                Tonelaje: 1000.0
            })
        });

        if (!transRes.ok) {
            const errBody = await transRes.text();
            throw new Error(`Fallo al transbordar: ${errBody}`);
        }
        const transData = await transRes.json();
        logSuccess(`Transbordo exitoso: ${transData.mensaje}`);

        // Verificar balance post-transbordo
        logInfo("Verificando balance de cargas post-transbordo...");
        const cargasAPost = await (await fetch(`${BASE_URL}/api/carga/viaje/${voyageAId}`, { headers })).json();
        const cargasBPost = await (await fetch(`${BASE_URL}/api/carga/viaje/${voyageBId}`, { headers })).json();

        const rs001Post = cargasAPost.find(c => c.descripcionLista.includes("RS001") || c.id === "3000001" || c.id === "RS001" || c.id === cargaId);
        const rs002Post = cargasBPost.find(c => c.descripcionLista.includes("RS002") || c.id === "3000002" || c.id === "RS002");

        const checkA = rs001Post && rs001Post.tonelaje === 1400;
        const checkB = rs002Post && rs002Post.tonelaje === 1000;

        if (checkA) {
            logSuccess(`Origen (RS001 en Viaje A): Restante es 1400 Tn (correcto).`);
        } else {
            logFailure(`Origen (RS001 en Viaje A): Se esperaba 1400 Tn, obtenido: ${rs001Post ? rs001Post.tonelaje : "No encontrado"}`);
        }

        if (checkB) {
            logSuccess(`Destino (RS002 en Viaje B): Recibido es 1000 Tn (correcto).`);
        } else {
            logFailure(`Destino (RS002 en Viaje B): Se esperaba 1000 Tn, obtenido: ${rs002Post ? rs002Post.tonelaje : "No encontrado"}`);
        }

        if (checkA && checkB) {
            console.log(`\n\x1b[32;1m>>> ¡TODAS LAS COMPROBACIONES DE CARGA SE COMPLETARON EXITOSAMENTE EN CMD! <<<\n\x1b[0m`);
        } else {
            console.log(`\n\x1b[31;1m>>> HUBO ALGUNOS FALLOS EN LAS COMPROBACIONES DE CARGA. <<<\n\x1b[0m`);
        }

    } catch (error) {
        logFailure(`Fallo crítico en el proceso de pruebas: ${error.message}`);
    }
}

runTests();
