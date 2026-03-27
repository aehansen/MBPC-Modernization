// MbpcDashboard.jsx - VERSIÓN MODERNIZADA v0.6.0
// Cambios v0.6.0:
//   INTEGRACIÓN ARCGIS: Se importó MapaAIS.jsx y se cableó el botón "Ver Mapa" 
//   para alternar entre la vista de Grilla (Dashboard) y el Mapa Geoespacial.
// Cambios v0.5.0:
//   TAREA 1 — Tabla de Viajes Activos: 3 botones de estado por fila (Zarpar / Amarrar / Fondear)
//             que llaman a PUT /api/viajes/{id}/zarpar|amarrar|fondear y recargan la grilla.
//   TAREA 2 — Manifiesto de Carga: botón "Añadir Carga" que abre un modal para ingresar
//             Nombre, Tipo (Bodega/Barcaza) y Tonelaje, y llama a POST /api/carga/viaje/{buque}.

import React, { useState, useEffect } from 'react';
import axios from 'axios';
import MapaAIS from './MapaAIS'; // <-- Importamos nuestro nuevo componente de mapa

// Configuración API - Asegurate de que esto coincida con tu puerto de .NET
const API_BASE_URL = 'http://localhost:5009/api';

const api = axios.create({
    baseURL: API_BASE_URL,
    headers: { 'Content-Type': 'application/json' }
});

// ─────────────────────────────────────────────────────────────────────────────
// DATOS DEL ENUM DeclaracionMalvinasEnum — mapeados 1:1 al C# para el select
// ─────────────────────────────────────────────────────────────────────────────
const DECLARACION_MALVINAS_OPTIONS = [
    { value: 'NoVieneDeMalvinas_L',                              label: 'No viene de Malvinas (L)' },
    { value: 'VieneDeMalvinas_AutorizadoCPER_M',                 label: 'Viene de Malvinas: Autorizado por la CPER (M)' },
    { value: 'VieneDeMalvinas_NoAutorizado_Infraccion_Extranjero_W', label: 'Viene de Malvinas: No autorizado - Se labró infracción - Va al extranjero (W)' },
    { value: 'VieneDeMalvinas_NoSolicitoAutorizacion_Amarra_Y',  label: 'Viene de Malvinas: No solicitó autorización (Amarra en el país) (Y)' },
    { value: 'VieneDeMalvinas_SolicitoAutorizacion_Amarra_V',    label: 'Viene de Malvinas: Solicitó autorización (Amarra en el país) (V)' },
    { value: 'NoVaAMalvinas_Exceptuado_MilitarOGC_D',            label: 'No va a Malvinas: Exceptuado, Militar o GC - Cualquier bandera (D)' },
    { value: 'NoVaAMalvinas_Exceptuado_NoNavegacionMaritima_F',  label: 'No va a Malvinas: Exceptuado, no realiza navegación marítima (F)' },
    { value: 'NoVaAMalvinas_B',                                  label: 'No va a Malvinas (B)' },
    { value: 'NoVaAMalvinas_Exceptuado_GiroInteriorPuerto_G',    label: 'No va a Malvinas: Exceptuado, giro interior puerto - misma jurisdicción (G)' },
    { value: 'NoVaAMalvinas_Exceptuado_NavegacionRadaRiaCostera_E', label: 'No va a Malvinas: Exceptuado, navegación Rada-Ría o Costera (E)' },
    { value: 'NoVaAMalvinas_Exceptuado_OtrosMotivos_X',          label: 'No va a Malvinas: Exceptuado, por otros motivos (X)' },
    { value: 'NoVaAMalvinas_NoPresentoDeclaracion_N',            label: 'No va a Malvinas: No presentó Declaración Jurada (N)' },
    { value: 'NoVaAMalvinas_PresentoDeclaracion_J',              label: 'No va a Malvinas: Presentó Declaración Jurada (J)' },
    { value: 'NoVaAMalvinas_ReiniciaNavegacion_PresentoDeclaracion_K', label: 'No va a Malvinas: Reinicia navegación - Presentó Declaración Jurada (K)' },
    { value: 'VaAMalvinas_Exceptuado_MilitarOGC_Q',              label: 'Va a Malvinas: Exceptuado, Militar o GC - Cualquier bandera (Q)' },
    { value: 'VaAMalvinas_AutorizadoCPER_A',                     label: 'Va a Malvinas: Autorizado por la CPER (A)' },
    { value: 'VaAMalvinas_AutorizadoCPER_ReiniciaNavegacion_R',  label: 'Va a Malvinas: Autorizado por la CPER - Reinicia navegación (R)' },
    { value: 'VaAMalvinas_NoAutorizadoCPER_Z',                   label: 'Va a Malvinas: No autorizado por la CPER (Z)' },
    { value: 'VaAMalvinas_NoAutorizadoCPER_Fondeo_P',            label: 'Va a Malvinas: No autorizado por la CPER - Se ordenó fondeo (P)' },
];

// Opciones de Próximo Punto de Control
const PUNTOS_CONTROL_OPTIONS = [
    { value: 'RPNA_AMARR_ELDO', label: 'Río Paraná - AMARR ELDO' },
    { value: 'RPNA_PREFECT_ROS', label: 'Río Paraná - Prefectura Rosario' },
    { value: 'RPLATA_CANAL_MITRE', label: 'Río de la Plata - Canal Mitre KM 0' },
];

// ─────────────────────────────────────────────────────────────────────────────
// ICONOS SVG
// ─────────────────────────────────────────────────────────────────────────────
const IconoBarco = ({ className = "w-6 h-6 mr-3 text-blue-300" }) => (
    <svg className={className} fill="none" stroke="currentColor" viewBox="0 0 24 24">
        <path strokeLinecap="round" strokeLinejoin="round" strokeWidth="2" d="M12 19l9 2-9-18-9 18 9-2zm0 0v-8" />
    </svg>
);

const IconoCarga = ({ className = "w-5 h-5 mr-2 text-gray-500" }) => (
    <svg className={className} fill="none" stroke="currentColor" viewBox="0 0 24 24">
        <path strokeLinecap="round" strokeLinejoin="round" strokeWidth="2" d="M20 7l-8-4-8 4m16 0l-8 4m8-4v10l-8 4m0-10L4 7m8 4v10M4 7v10l8 4" />
    </svg>
);

const IconoUbicacion = ({ className = "w-5 h-5 mr-2 text-gray-400" }) => (
    <svg className={className} fill="none" stroke="currentColor" viewBox="0 0 24 24">
        <path strokeLinecap="round" strokeLinejoin="round" strokeWidth="2" d="M17.657 16.657L13.414 20.9a1.998 1.998 0 01-2.827 0l-4.244-4.243a8 8 0 1111.314 0z" />
        <path strokeLinecap="round" strokeLinejoin="round" strokeWidth="2" d="M15 11a3 3 0 11-6 0 3 3 0 016 0z" />
    </svg>
);

const IconoX = () => (
    <svg className="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
        <path strokeLinecap="round" strokeLinejoin="round" strokeWidth="2" d="M6 18L18 6M6 6l12 12" />
    </svg>
);

const IconoSpinner = () => (
    <svg className="animate-spin w-4 h-4" fill="none" viewBox="0 0 24 24">
        <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" />
        <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4z" />
    </svg>
);

// ─────────────────────────────────────────────────────────────────────────────
// ESTADO INICIAL DEL FORMULARIO "NUEVO VIAJE"
// ─────────────────────────────────────────────────────────────────────────────
const NUEVO_VIAJE_INITIAL = {
    nombreBuque: '', origen: '', destino: '', muelleSalida: '',
    proximoPuntoControl: '', fechaPartida: '', eta: '',
    zoe: '', posicion: '', rioCanalKmPar: '', declaracionMalvinas: '',
};

const HISTORICO_FILTRO_INITIAL = {
    nombre: '', omi: '', matricula: '', origen: '', destino: '', desde: '', hasta: ''
};

// TAREA 2 — Estado inicial del formulario "Añadir Carga"
const NUEVA_CARGA_INITIAL = {
    nombre: '',
    tipo: 'Barcaza',
    tonelaje: '',
};

// ─────────────────────────────────────────────────────────────────────────────
// COMPONENTE CAMPO DE FORMULARIO
// ─────────────────────────────────────────────────────────────────────────────
const Campo = ({ label, required, children }) => (
    <div>
        <label className="block text-xs font-semibold text-gray-600 mb-1">
            {label}{required && <span className="text-red-500 ml-0.5">*</span>}
        </label>
        {children}
    </div>
);

const inputCls = "w-full px-3 py-2 border border-gray-300 rounded-lg text-sm focus:ring-2 focus:ring-blue-200 focus:border-[#104a8e] transition outline-none";

// ─────────────────────────────────────────────────────────────────────────────
// COMPONENTE MODAL GENÉRICO — overlay + contenedor centrado
// ─────────────────────────────────────────────────────────────────────────────
const Modal = ({ onClose, children, maxWidth = "max-w-2xl" }) => (
    <div className="fixed inset-0 z-50 overflow-y-auto" role="dialog" aria-modal="true">
        <div className="flex items-center justify-center min-h-screen p-4">
            <div className="fixed inset-0 bg-gray-900 bg-opacity-60 transition-opacity" aria-hidden="true" onClick={onClose} />
            <div className={`relative bg-white rounded-2xl shadow-2xl w-full ${maxWidth} max-h-[90vh] overflow-y-auto z-10`}>
                {children}
            </div>
        </div>
    </div>
);

const ModalHeader = ({ titulo, subtitulo, icono, onClose }) => (
    <div className="sticky top-0 bg-[#002454] text-white px-6 py-4 flex items-center justify-between rounded-t-2xl z-10">
        <div className="flex items-center gap-3">
            {icono}
            <div>
                <h2 className="text-lg font-bold">{titulo}</h2>
                {subtitulo && <p className="text-xs text-blue-300">{subtitulo}</p>}
            </div>
        </div>
        <button onClick={onClose} className="text-blue-200 hover:text-white transition p-1 rounded-lg hover:bg-blue-800">
            <IconoX />
        </button>
    </div>
);

// ─────────────────────────────────────────────────────────────────────────────
// COMPONENTE PRINCIPAL
// ─────────────────────────────────────────────────────────────────────────────
const MbpcDashboard = () => {
    // --- ESTADO DE VISTA PRINCIPAL (Dashboard vs Mapa) ---
    const [vistaActual, setVistaActual] = useState('dashboard');

    // --- ESTADOS ORIGINALES ---
    const [viajes, setViajes] = useState([]);
    const [selectedViajeId, setSelectedViajeId] = useState(null);
    const [viajeSeleccionado, setViajeSeleccionado] = useState(null);
    const [cargas, setCargas] = useState([]);
    const [loading, setLoading] = useState({ viajes: false, cargas: false });
    const [error, setError] = useState(null);
    const [filtro, setFiltro] = useState("");
    const [paginaActual, setPaginaActual] = useState(1);
    const tamanioPagina = 10;

    // --- ESTADO MODAL ACCIONES DE CARGA ---
    const [modalState, setModalState] = useState({
        show: false,
        tipo: null,         
        cargaId: null,
        tonelajeActual: 0,
        muelle: '',
        zona: '',
        fechaHora: '',
        posicion: '',
        toneladas: '',      
        loading: false
    });

    // --- ESTADO MODAL NUEVO VIAJE ---
    const [modalNuevoViaje, setModalNuevoViaje] = useState({ show: false, loading: false });
    const [nuevoViajeForm, setNuevoViajeForm] = useState(NUEVO_VIAJE_INITIAL);
    const [nuevoViajeErrors, setNuevoViajeErrors] = useState({});

    // --- ESTADO MODAL BARCOS EN PUERTO ---
    const [modalPuerto, setModalPuerto] = useState({ show: false, loading: false, datos: [] });

    // --- ESTADO MODAL HISTÓRICO ---
    const [modalHistorico, setModalHistorico] = useState({
        show: false, loading: false, resultados: [], buscado: false
    });
    const [filtroHistorico, setFiltroHistorico] = useState(HISTORICO_FILTRO_INITIAL);

    // --- ESTADO MODAL AMARRAR BARCAZA GLOBAL ---
    const [modalAmarrarGlobal, setModalAmarrarGlobal] = useState({
        show: false, loading: false,
        barcazaId: '', lugarAmarre: '', fechaHora: ''
    });

    // TAREA 1 — Estado de carga para los botones de cambio de estado por fila
    const [estadoViajeLoading, setEstadoViajeLoading] = useState({});

    // TAREA 2 — Estado modal "Añadir Carga"
    const [modalNuevaCarga, setModalNuevaCarga] = useState({ show: false, loading: false });
    const [nuevaCargaForm, setNuevaCargaForm] = useState(NUEVA_CARGA_INITIAL);
    const [nuevaCargaErrors, setNuevaCargaErrors] = useState({});

    // ── EFECTOS ────────────────────────────────────────────────────────────────
    useEffect(() => { fetchViajes(); }, [paginaActual]);

    useEffect(() => {
        if (selectedViajeId) {
            fetchCargas(selectedViajeId);
            const viaje = viajes.find(v => v.id === selectedViajeId);
            setViajeSeleccionado(viaje);
        } else {
            setCargas([]);
            setViajeSeleccionado(null);
        }
    }, [selectedViajeId, viajes]);

    // ── LLAMADAS API ────────────────────────────────────────────────────────────
    const fetchViajes = async () => {
        setLoading(prev => ({ ...prev, viajes: true }));
        setError(null);
        try {
            const response = await api.get(`/viajes?pagina=${paginaActual}&tamanio=${tamanioPagina}`);
            setViajes(response.data);
        } catch (err) {
            console.error("Error fetching viajes:", err);
            setError("No se pudo conectar con el servicio de MBPC. Verifique que el Backend esté corriendo.");
        } finally {
            setLoading(prev => ({ ...prev, viajes: false }));
        }
    };

    const fetchCargas = async (viajeId) => {
        setLoading(prev => ({ ...prev, cargas: true }));
        try {
            const response = await api.get(`/carga/viaje/${viajeId}`);
            setCargas(response.data);
        } catch (err) {
            console.error("Error fetching cargas:", err);
        } finally {
            setLoading(prev => ({ ...prev, cargas: false }));
        }
    };

    // ── TAREA 1: CAMBIO DE ESTADO DEL BUQUE ────────────────────────────────────
    const cambiarEstadoViaje = async (viaje, accion) => {
        // accion: 'zarpar' | 'amarrar' | 'fondear'
        const key = `${viaje.id}-${accion}`;
        setEstadoViajeLoading(prev => ({ ...prev, [key]: true }));
        try {
            await api.put(`/viajes/${encodeURIComponent(viaje.id)}/${accion}`);
            // Recargamos la grilla para reflejar el nuevo estado
            await fetchViajes();
        } catch (err) {
            console.error(`Error al ejecutar '${accion}' para viaje ${viaje.id}:`, err);
            alert(`No se pudo ejecutar '${accion}' para ${viaje.buque}. Verificá la consola y el Backend.`);
        } finally {
            setEstadoViajeLoading(prev => ({ ...prev, [key]: false }));
        }
    };

    // ── MODAL ACCIONES DE CARGA ─────────────────────────────────────────────────
    const abrirModalAccion = (carga, tipo) => {
        setModalState({
            show: true, 
            tipo, 
            cargaId: carga.id,
            tonelajeActual: carga.tonelaje || 0,
            muelle: '', zona: '', fechaHora: '', posicion: '', toneladas: '', loading: false
        });
    };

    const cerrarModal = () => {
        setModalState({
            show: false, tipo: null, cargaId: null, tonelajeActual: 0,
            muelle: '', zona: '', fechaHora: '', posicion: '', toneladas: '', loading: false
        });
    };

    const ejecutarAccionCarga = async () => {
        // Validaciones por tipo
        if (modalState.tipo === 'amarrar_buque' && !modalState.muelle.trim()) {
            alert("Por favor, ingrese el nombre del muelle."); return;
        }
        if (modalState.tipo === 'fondear_buque' && !modalState.zona.trim()) {
            alert("Por favor, ingrese la zona de fondeo."); return;
        }
        if (modalState.tipo === 'amarrar_barcaza') {
            if (!modalState.fechaHora) { alert("Por favor, ingrese la fecha y hora de amarre."); return; }
            if (!modalState.posicion.trim()) { alert("Por favor, ingrese la posición."); return; }
        }
        
        // ¡ESTAS SON TUS REGLAS DE NEGOCIO ESTRICTAS!
        if (modalState.tipo === 'cargar') {
            const tons = parseFloat(modalState.toneladas);
            if (isNaN(tons) || tons <= modalState.tonelajeActual) {
                alert(`Para cargar, el tonelaje final (${tons || 0}) debe ser estrictamente MAYOR al actual (${modalState.tonelajeActual} Tn).`); return;
            }
        }
        if (modalState.tipo === 'descargar') {
            const tons = parseFloat(modalState.toneladas);
            if (isNaN(tons) || tons >= modalState.tonelajeActual || tons < 0) {
                alert(`Para descargar, el tonelaje final (${tons || 0}) debe ser estrictamente MENOR al actual (${modalState.tonelajeActual} Tn) y mayor o igual a 0.`); return;
            }
        }

        setModalState(prev => ({ ...prev, loading: true }));
        try {
            const cargaIdSeguro = encodeURIComponent(modalState.cargaId);
            let url = '';

            switch (modalState.tipo) {
                case 'amarrar_buque':
                    url = `/carga/${cargaIdSeguro}/amarrar?nuevoMuelle=${encodeURIComponent(modalState.muelle)}`;
                    break;
                case 'fondear_buque':
                    url = `/carga/${cargaIdSeguro}/fondear?zonaFondeo=${encodeURIComponent(modalState.zona)}`;
                    break;
                case 'cargar':
                    url = `/carga/${cargaIdSeguro}/cargar?toneladas=${encodeURIComponent(parseFloat(modalState.toneladas))}`;
                    break;
                case 'descargar':
                    url = `/carga/${cargaIdSeguro}/descargar?toneladas=${encodeURIComponent(parseFloat(modalState.toneladas))}`;
                    break;
                case 'amarrar_barcaza':
                    url = `/carga/${cargaIdSeguro}/amarrar?nuevoMuelle=${encodeURIComponent(modalState.posicion)}&fechaHora=${encodeURIComponent(modalState.fechaHora)}`;
                    break;
                default:
                    url = `/carga/${cargaIdSeguro}/fondear?zonaFondeo=Zona_General`;
            }

            await api.put(url);
            await fetchCargas(selectedViajeId); // Recarga para ver el nuevo peso
            cerrarModal();
        } catch (err) {
            console.error(`Error al ejecutar acción ${modalState.tipo}:`, err);
            alert(`Falló la conexión con la API de .NET. Verificá la consola.`);
        } finally {
            setModalState(prev => ({ ...prev, loading: false }));
        }
    };

    const getModalTitle = () => {
        const titles = {
            amarrar_buque:   'Confirmar Amarre del Remolcador/Buque',
            fondear_buque:   'Confirmar Fondeo del Remolcador/Buque',
            cargar:          'Registrar Carga Final',
            descargar:       'Registrar Descarga Final',
            amarrar_barcaza: 'Confirmar Amarre de Barcaza',
        };
        return titles[modalState.tipo] || 'Confirmar Operación';
    };

    // ── MODAL NUEVO VIAJE ──────────────────────────────────────────────────────
    const abrirNuevoViaje = () => {
        setNuevoViajeForm(NUEVO_VIAJE_INITIAL);
        setNuevoViajeErrors({});
        setModalNuevoViaje({ show: true, loading: false });
    };

    const cerrarNuevoViaje = () => setModalNuevoViaje({ show: false, loading: false });

    const handleNuevoViajeChange = (e) => {
        const { name, value } = e.target;
        setNuevoViajeForm(prev => ({ ...prev, [name]: value }));
        if (nuevoViajeErrors[name]) setNuevoViajeErrors(prev => ({ ...prev, [name]: null }));
    };

    const validarNuevoViaje = () => {
        const errors = {};
        if (!nuevoViajeForm.nombreBuque.trim()) errors.nombreBuque = "El nombre del buque es requerido.";
        if (!nuevoViajeForm.origen.trim()) errors.origen = "El origen es requerido.";
        if (!nuevoViajeForm.destino.trim()) errors.destino = "El destino es requerido.";
        if (!nuevoViajeForm.proximoPuntoControl) errors.proximoPuntoControl = "El próximo punto de control es requerido.";
        if (!nuevoViajeForm.fechaPartida) errors.fechaPartida = "La fecha de partida es requerida.";
        if (!nuevoViajeForm.eta) errors.eta = "La ETA es requerida.";
        if (!nuevoViajeForm.declaracionMalvinas) errors.declaracionMalvinas = "La declaración de Malvinas es requerida.";
        return errors;
    };

    const guardarNuevoViaje = async () => {
        const errors = validarNuevoViaje();
        if (Object.keys(errors).length > 0) { setNuevoViajeErrors(errors); return; }
        setModalNuevoViaje(prev => ({ ...prev, loading: true }));
        try {
            const payload = {
                nombreBuque: nuevoViajeForm.nombreBuque,
                origen: nuevoViajeForm.origen,
                destino: nuevoViajeForm.destino,
                muelleSalida: nuevoViajeForm.muelleSalida || null,
                proximoPuntoControl: nuevoViajeForm.proximoPuntoControl,
                fechaPartida: nuevoViajeForm.fechaPartida,
                eta: nuevoViajeForm.eta,
                zoe: nuevoViajeForm.zoe || null,
                posicion: nuevoViajeForm.posicion || null,
                rioCanalKmPar: nuevoViajeForm.rioCanalKmPar ? parseFloat(nuevoViajeForm.rioCanalKmPar) : null,
                declaracionMalvinas: nuevoViajeForm.declaracionMalvinas,
            };
            await api.post('/viajes', payload);
            await fetchViajes();
            cerrarNuevoViaje();
        } catch (err) {
            console.error("Error al crear viaje:", err);
            alert("No se pudo crear el viaje. Verificá la consola y el Backend.");
        } finally {
            setModalNuevoViaje(prev => ({ ...prev, loading: false }));
        }
    };

    // ── MODAL BARCOS EN PUERTO ─────────────────────────────────────────────────
    const abrirModalPuerto = async () => {
        setModalPuerto({ show: true, loading: true, datos: [] });
        try {
            const response = await api.get('/viajes/puerto');
            setModalPuerto({ show: true, loading: false, datos: response.data });
        } catch (err) {
            console.error("Error fetching barcos en puerto:", err);
            setModalPuerto({ show: true, loading: false, datos: [] });
            alert("No se pudo obtener la lista de barcos en puerto.");
        }
    };

    const cerrarModalPuerto = () => setModalPuerto({ show: false, loading: false, datos: [] });

    // ── MODAL HISTÓRICO ────────────────────────────────────────────────────────
    const abrirModalHistorico = () => {
        setFiltroHistorico(HISTORICO_FILTRO_INITIAL);
        setModalHistorico({ show: true, loading: false, resultados: [], buscado: false });
    };

    const cerrarModalHistorico = () =>
        setModalHistorico({ show: false, loading: false, resultados: [], buscado: false });

    const handleFiltroHistoricoChange = (e) => {
        const { name, value } = e.target;
        setFiltroHistorico(prev => ({ ...prev, [name]: value }));
    };

    const buscarHistorico = async () => {
        setModalHistorico(prev => ({ ...prev, loading: true }));
        try {
            const params = new URLSearchParams();
            if (filtroHistorico.nombre)    params.append('nombre',    filtroHistorico.nombre);
            if (filtroHistorico.omi)       params.append('omi',       filtroHistorico.omi);
            if (filtroHistorico.matricula) params.append('matricula', filtroHistorico.matricula);
            if (filtroHistorico.origen)    params.append('origen',    filtroHistorico.origen);
            if (filtroHistorico.destino)   params.append('destino',   filtroHistorico.destino);
            if (filtroHistorico.desde)     params.append('desde',     filtroHistorico.desde);
            if (filtroHistorico.hasta)     params.append('hasta',     filtroHistorico.hasta);

            const response = await api.get(`/viajes/historico?${params.toString()}`);
            setModalHistorico(prev => ({
                ...prev, loading: false, resultados: response.data, buscado: true
            }));
        } catch (err) {
            console.error("Error fetching histórico:", err);
            setModalHistorico(prev => ({ ...prev, loading: false, buscado: true, resultados: [] }));
            alert("No se pudo obtener el histórico. Verificá el Backend.");
        }
    };

    // ── MODAL AMARRAR BARCAZA GLOBAL ───────────────────────────────────────────
    const abrirAmarrarGlobal = () => {
        setModalAmarrarGlobal({ show: true, loading: false, barcazaId: '', lugarAmarre: '', fechaHora: '' });
    };

    const cerrarAmarrarGlobal = () =>
        setModalAmarrarGlobal({ show: false, loading: false, barcazaId: '', lugarAmarre: '', fechaHora: '' });

    const ejecutarAmarrarGlobal = async () => {
        if (!modalAmarrarGlobal.barcazaId.trim()) { alert("Ingrese el nombre o ID de la barcaza."); return; }
        if (!modalAmarrarGlobal.lugarAmarre.trim()) { alert("Ingrese el lugar de amarre."); return; }
        if (!modalAmarrarGlobal.fechaHora) { alert("Ingrese la fecha y hora de amarre."); return; }

        setModalAmarrarGlobal(prev => ({ ...prev, loading: true }));
        try {
            const idSeguro  = encodeURIComponent(modalAmarrarGlobal.barcazaId.trim());
            const muelleSeguro = encodeURIComponent(modalAmarrarGlobal.lugarAmarre.trim());
            await api.put(`/carga/${idSeguro}/amarrar?nuevoMuelle=${muelleSeguro}`);
            cerrarAmarrarGlobal();
            // Recargamos cargas si hay un viaje seleccionado
            if (selectedViajeId) await fetchCargas(selectedViajeId);
            alert(`Barcaza "${modalAmarrarGlobal.barcazaId}" amarrada exitosamente en "${modalAmarrarGlobal.lugarAmarre}".`);
        } catch (err) {
            console.error("Error al amarrar barcaza (global):", err);
            alert("Falló la operación. Verificá que el ID de la barcaza sea correcto y el Backend esté online.");
        } finally {
            setModalAmarrarGlobal(prev => ({ ...prev, loading: false }));
        }
    };

    // ── TAREA 2: MODAL AÑADIR CARGA ────────────────────────────────────────────
    const abrirModalNuevaCarga = () => {
        setNuevaCargaForm(NUEVA_CARGA_INITIAL);
        setNuevaCargaErrors({});
        setModalNuevaCarga({ show: true, loading: false });
    };

    const cerrarModalNuevaCarga = () => setModalNuevaCarga({ show: false, loading: false });

    const handleNuevaCargaChange = (e) => {
        const { name, value } = e.target;
        setNuevaCargaForm(prev => ({ ...prev, [name]: value }));
        if (nuevaCargaErrors[name]) setNuevaCargaErrors(prev => ({ ...prev, [name]: null }));
    };

    const validarNuevaCarga = () => {
        const errors = {};
        if (!nuevaCargaForm.nombre.trim()) errors.nombre = "El nombre/ID de la carga es requerido.";
        if (!nuevaCargaForm.tipo) errors.tipo = "El tipo es requerido.";
        const tons = parseFloat(nuevaCargaForm.tonelaje);
        if (isNaN(tons) || tons < 0)
            errors.tonelaje = "El tonelaje debe ser un número mayor o igual a 0.";
        return errors;
    };

    const guardarNuevaCarga = async () => {
        const errors = validarNuevaCarga();
        if (Object.keys(errors).length > 0) { setNuevaCargaErrors(errors); return; }

        // Necesitamos el nombre del buque para la URL del endpoint
        const nombreBuque = viajeSeleccionado?.buque;
        if (!nombreBuque) {
            alert("No se puede identificar el buque del viaje seleccionado.");
            return;
        }

        setModalNuevaCarga(prev => ({ ...prev, loading: true }));
        try {
            const payload = {
                nombre: nuevaCargaForm.nombre.trim(),
                tipo: nuevaCargaForm.tipo,
                tonelaje: parseFloat(nuevaCargaForm.tonelaje),
            };
            await api.post(`/carga/viaje/${encodeURIComponent(nombreBuque)}`, payload);
            // Recargamos el manifiesto para ver la nueva carga
            await fetchCargas(selectedViajeId);
            cerrarModalNuevaCarga();
        } catch (err) {
            console.error("Error al agregar carga:", err);
            alert("No se pudo agregar la carga. Verificá la consola y el Backend.");
        } finally {
            setModalNuevaCarga(prev => ({ ...prev, loading: false }));
        }
    };

    // ── HELPERS ────────────────────────────────────────────────────────────────
    const getRiesgoBadge = (nivel) => {
        const baseClass = "px-3 py-1 text-xs font-medium rounded-full";
        switch (nivel?.toUpperCase()) {
            case 'ALTO':  return `<span class="${baseClass} bg-red-100 text-red-800">Alto</span>`;
            case 'MEDIO': return `<span class="${baseClass} bg-indigo-100 text-indigo-800">Medio</span>`;
            default:      return `<span class="${baseClass} bg-blue-100 text-blue-800">Bajo</span>`;
        }
    };

    const esBarcaza = (carga) => carga.tipoCarga?.toUpperCase() === 'BARCAZA' || carga.esBarcaza === true;

    const getEstadoBadgeClass = (estado) => {
        const e = (estado || '').toLowerCase();
        if (e.includes('amarr') || e.includes('moored')) return 'bg-green-100 text-green-800';
        if (e.includes('fondea') || e.includes('anchor')) return 'bg-yellow-100 text-yellow-800';
        if (e.includes('cancel')) return 'bg-red-100 text-red-800';
        if (e.includes('finaliz')) return 'bg-gray-100 text-gray-600';
        return 'bg-blue-100 text-blue-800';
    };

    // ─────────────────────────────────────────────────────────────────────────
    // JSX
    // ─────────────────────────────────────────────────────────────────────────
    return (
        <div className="min-h-screen bg-gray-50 flex flex-col font-sans text-gray-900">

            {/* ── ENCABEZADO ─────────────────────────────────────────────── */}
            <header className="bg-[#002454] text-white shadow-lg p-4 flex items-center justify-between sticky top-0 z-40">
                <div className="flex items-center">
                    <img
                        src="https://www.argentina.gob.ar/sites/default/files/styles/isotipo/public/imagenEncabezado/prefectura-escudo.png?itok=EywBfOaV"
                        alt="PNA Logo"
                        className="h-12 mr-4"
                    />
                    <div>
                        <h1 className="text-2xl font-bold tracking-tight">MBPC - Modernización</h1>
                        <p className="text-sm text-blue-200">Prefectura Naval Argentina - Gestión de Tráfico de Neri</p>
                    </div>
                </div>
                <div className="flex items-center gap-2">
                    <span className="text-xs bg-green-500 text-white px-2 py-1 rounded-full font-mono">BACKEND ONLINE</span>
                    <div className="w-10 h-10 rounded-full bg-blue-800 flex items-center justify-center font-bold text-blue-100 border-2 border-blue-600">AN</div>
                </div>
            </header>

            {/* ── BOTONERA SUPERIOR ─────────────────────────────────────── */}
            <div className="bg-[#002454] border-t border-blue-800 px-6 py-2 flex items-center gap-2 flex-wrap">
                {/* BOTON: Ver Mapa / Volver al Dashboard */}
                <button
                    onClick={() => setVistaActual(vistaActual === 'mapa' ? 'dashboard' : 'mapa')}
                    className={`flex items-center gap-1.5 px-4 py-1.5 text-white text-xs font-semibold rounded transition border ${vistaActual === 'mapa' ? 'bg-amber-600 hover:bg-amber-700 border-amber-500' : 'bg-[#104a8e] hover:bg-[#1a5fa8] border-blue-600'}`}
                >
                    {vistaActual === 'mapa' ? (
                        <>
                            <svg className="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth="2" d="M10 19l-7-7m0 0l7-7m-7 7h18" />
                            </svg>
                            Volver al Dashboard
                        </>
                    ) : (
                        <>
                            <svg className="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth="2" d="M9 20l-5.447-2.724A1 1 0 013 16.382V5.618a1 1 0 011.447-.894L9 7m0 13l6-3m-6 3V7m6 10l4.553 2.276A1 1 0 0021 18.382V7.618a1 1 0 00-.553-.894L15 4m0 13V4m0 0L9 7" />
                            </svg>
                            Ver Mapa AIS
                        </>
                    )}
                </button>

                {/* Amarrar Barcaza (global) */}
                <button
                    onClick={abrirAmarrarGlobal}
                    className="flex items-center gap-1.5 px-4 py-1.5 bg-[#104a8e] hover:bg-[#1a5fa8] text-white text-xs font-semibold rounded transition border border-blue-600"
                >
                    <svg className="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                        <path strokeLinecap="round" strokeLinejoin="round" strokeWidth="2" d="M13 10V3L4 14h7v7l9-11h-7z" />
                    </svg>
                    Amarrar Barcaza
                </button>

                {/* Nuevo Viaje */}
                <button
                    onClick={abrirNuevoViaje}
                    className="flex items-center gap-1.5 px-4 py-1.5 bg-green-600 hover:bg-green-700 text-white text-xs font-semibold rounded transition border border-green-500"
                >
                    <svg className="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                        <path strokeLinecap="round" strokeLinejoin="round" strokeWidth="2" d="M12 4v16m8-8H4" />
                    </svg>
                    Nuevo Viaje
                </button>

                {/* Barcos en Puerto */}
                <button
                    onClick={abrirModalPuerto}
                    className="flex items-center gap-1.5 px-4 py-1.5 bg-[#104a8e] hover:bg-[#1a5fa8] text-white text-xs font-semibold rounded transition border border-blue-600"
                >
                    <svg className="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                        <path strokeLinecap="round" strokeLinejoin="round" strokeWidth="2" d="M3 12l2-2m0 0l7-7 7 7M5 10v10a1 1 0 001 1h3m10-11l2 2m-2-2v10a1 1 0 01-1 1h-3m-6 0a1 1 0 001-1v-4a1 1 0 011-1h2a1 1 0 011 1v4a1 1 0 001 1m-6 0h6" />
                    </svg>
                    Barcos en Puerto
                </button>

                {/* Viaje Histórico */}
                <button
                    onClick={abrirModalHistorico}
                    className="flex items-center gap-1.5 px-4 py-1.5 bg-[#104a8e] hover:bg-[#1a5fa8] text-white text-xs font-semibold rounded transition border border-blue-600"
                >
                    <svg className="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                        <path strokeLinecap="round" strokeLinejoin="round" strokeWidth="2" d="M12 8v4l3 3m6-3a9 9 0 11-18 0 9 9 0 0118 0z" />
                    </svg>
                    Viaje (Histórico)
                </button>
            </div>

            {/* ── CONTENIDO PRINCIPAL ─────────────────────────────────────── */}
            <main className={`flex-grow ${vistaActual === 'mapa' ? '' : 'p-6 md:p-8 space-y-8'}`}>
                {/* RENDERIZADO DEL MAPA */}
                {vistaActual === 'mapa' ? (
                    <div style={{ height: 'calc(100vh - 130px)' }}> 
                        <MapaAIS />
                    </div>
                ) : (
                    <>
                        {/* RENDERIZADO DEL DASHBOARD ORIGINAL */}
                        {error && (
                            <div className="bg-red-50 border-l-4 border-red-500 p-4 rounded shadow-sm text-red-800 flex items-center">
                                <svg className="w-6 h-6 mr-3 flex-shrink-0" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth="2" d="M12 8v4m0 4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z" />
                                </svg>
                                <span>{error}</span>
                            </div>
                        )}

                        {/* ── SECCIÓN VIAJES ────────────────────────────────────── */}
                        <section className="bg-white rounded-xl shadow-sm border border-gray-100 p-6">
                            <div className="flex items-center justify-between mb-6 gap-4">
                                <div className="flex items-center">
                                    <IconoBarco />
                                    <h2 className="text-xl font-semibold text-gray-800">Viajes Activos / Recientes</h2>
                                </div>
                                <div className="flex items-center gap-3">
                                    <input
                                        type="text"
                                        placeholder="Filtrar por buque o ruta..."
                                        className="px-4 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-200 focus:border-[#104a8e] transition text-sm w-64 outline-none"
                                        value={filtro}
                                        onChange={(e) => setFiltro(e.target.value)}
                                    />
                                    <div className="flex gap-1">
                                        <button onClick={() => setPaginaActual(p => Math.max(1, p - 1))} className="px-3 py-1.5 border rounded-lg text-sm text-gray-600 hover:bg-gray-100 disabled:opacity-50" disabled={paginaActual === 1}>&lt;</button>
                                        <span className="px-3 py-1.5 text-sm font-medium text-gray-700">Pág. {paginaActual}</span>
                                        <button onClick={() => setPaginaActual(p => p + 1)} className="px-3 py-1.5 border rounded-lg text-sm text-gray-600 hover:bg-gray-100 disabled:opacity-50" disabled={viajes.length < tamanioPagina}>&gt;</button>
                                    </div>
                                </div>
                            </div>

                            <div className="overflow-x-auto">
                                <table className="w-full text-sm text-left">
                                    <thead className="text-xs text-gray-500 uppercase bg-gray-50 rounded-t-lg">
                                        <tr>
                                            <th className="px-5 py-3">Buque</th>
                                            <th className="px-5 py-3">Ruta (Origen - Destino)</th>
                                            <th className="px-5 py-3">Último Estado</th>
                                            <th className="px-5 py-3 text-right">Acciones</th>
                                        </tr>
                                    </thead>
                                    <tbody className="divide-y divide-gray-100">
                                        {loading.viajes ? (
                                            <tr><td colSpan="4" className="text-center py-10 text-gray-500">Cargando viajes desde MongoDB...</td></tr>
                                        ) : viajes.length === 0 ? (
                                            <tr><td colSpan="4" className="text-center py-10 text-gray-500">No se encontraron viajes.</td></tr>
                                        ) : viajes
                                            .filter(v => filtro === "" || v.buque?.toLowerCase().includes(filtro.toLowerCase()) || v.ruta?.toLowerCase().includes(filtro.toLowerCase()))
                                            .map(viaje => (
                                                <tr
                                                    key={viaje.id}
                                                    className={`hover:bg-blue-50 transition cursor-pointer ${selectedViajeId === viaje.id ? 'bg-blue-50' : ''}`}
                                                    onClick={() => setSelectedViajeId(viaje.id)}
                                                >
                                                    <td className="px-5 py-4 font-medium text-[#002454] flex items-center">
                                                        <IconoBarco />
                                                        {viaje.buque}
                                                    </td>
                                                    <td className="px-5 py-4 text-gray-600">{viaje.ruta}</td>
                                                    <td className="px-5 py-4">
                                                        <span className={`px-2.5 py-1 text-xs font-semibold rounded-full ${getEstadoBadgeClass(viaje.estadoActual)}`}>
                                                            {viaje.estadoActual}
                                                        </span>
                                                        <span className="text-xs text-gray-400 ml-2">{viaje.fechaEstado}</span>
                                                    </td>
                                                    <td className="px-5 py-4 text-right" onClick={e => e.stopPropagation()}>
                                                        <div className="flex items-center justify-end gap-1.5 flex-wrap">
                                                            {/* Ver Cargas */}
                                                            <button
                                                                onClick={() => setSelectedViajeId(viaje.id)}
                                                                className={`text-xs font-semibold px-2.5 py-1.5 rounded border transition ${selectedViajeId === viaje.id ? 'bg-[#104a8e] text-white border-[#104a8e]' : 'text-gray-500 border-gray-300 hover:bg-gray-100'}`}
                                                            >
                                                                {selectedViajeId === viaje.id ? 'Seleccionado' : 'Ver Cargas'}
                                                            </button>

                                                            {/* Zarpar */}
                                                            <button
                                                                onClick={() => cambiarEstadoViaje(viaje, 'zarpar')}
                                                                disabled={!!estadoViajeLoading[`${viaje.id}-zarpar`]}
                                                                title="Hacer zarpar el buque (estado → Navegando)"
                                                                className="flex items-center gap-1 text-xs font-semibold px-2.5 py-1.5 rounded border border-green-400 text-green-700 bg-green-50 hover:bg-green-100 transition disabled:opacity-50"
                                                            >
                                                                {estadoViajeLoading[`${viaje.id}-zarpar`]
                                                                    ? <IconoSpinner />
                                                                    : <svg className="w-3 h-3" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path strokeLinecap="round" strokeLinejoin="round" strokeWidth="2.5" d="M5 12h14M12 5l7 7-7 7" /></svg>
                                                                }
                                                                Zarpar
                                                            </button>

                                                            {/* Amarrar */}
                                                            <button
                                                                onClick={() => cambiarEstadoViaje(viaje, 'amarrar')}
                                                                disabled={!!estadoViajeLoading[`${viaje.id}-amarrar`]}
                                                                title="Amarrar el buque en puerto (estado → Amarrado)"
                                                                className="flex items-center gap-1 text-xs font-semibold px-2.5 py-1.5 rounded border border-[#104a8e] text-[#104a8e] bg-blue-50 hover:bg-blue-100 transition disabled:opacity-50"
                                                            >
                                                                {estadoViajeLoading[`${viaje.id}-amarrar`]
                                                                    ? <IconoSpinner />
                                                                    : <svg className="w-3 h-3" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path strokeLinecap="round" strokeLinejoin="round" strokeWidth="2.5" d="M13 10V3L4 14h7v7l9-11h-7z" /></svg>
                                                                }
                                                                Amarrar
                                                            </button>

                                                            {/* Fondear */}
                                                            <button
                                                                onClick={() => cambiarEstadoViaje(viaje, 'fondear')}
                                                                disabled={!!estadoViajeLoading[`${viaje.id}-fondear`]}
                                                                title="Fondear el buque (estado → Fondeado)"
                                                                className="flex items-center gap-1 text-xs font-semibold px-2.5 py-1.5 rounded border border-yellow-500 text-yellow-700 bg-yellow-50 hover:bg-yellow-100 transition disabled:opacity-50"
                                                            >
                                                                {estadoViajeLoading[`${viaje.id}-fondear`]
                                                                    ? <IconoSpinner />
                                                                    : <svg className="w-3 h-3" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path strokeLinecap="round" strokeLinejoin="round" strokeWidth="2.5" d="M3 6h18M3 12h18M3 18h18" /></svg>
                                                                }
                                                                Fondear
                                                            </button>
                                                        </div>
                                                    </td>
                                                </tr>
                                            ))}
                                    </tbody>
                                </table>
                            </div>
                        </section>

                        {/* ── SECCIÓN CARGAS ────────────────────────────────────── */}
                        {selectedViajeId && (
                            <section className="bg-white rounded-xl shadow-lg border border-gray-100 border-t-4 border-t-[#104a8e] p-6 fade-in">
                                <div className="flex items-center justify-between mb-6 pb-4 border-b">
                                    <div>
                                        <h3 className="text-lg font-bold text-gray-800 flex items-center">
                                            <IconoCarga />
                                            Manifiesto de Carga - {viajeSeleccionado?.buque || 'Cargando...'}
                                        </h3>
                                        <p className="text-sm text-gray-500 flex items-center mt-1">
                                            <IconoUbicacion />
                                            {viajeSeleccionado?.ruta}
                                        </p>
                                    </div>
                                    <div className="flex items-center gap-3">
                                        {loading.cargas && <span className="text-sm text-gray-400">Actualizando...</span>}
                                        <button
                                            onClick={abrirModalNuevaCarga}
                                            className="flex items-center gap-1.5 px-4 py-2 bg-green-600 hover:bg-green-700 text-white text-sm font-semibold rounded-lg transition shadow-sm"
                                        >
                                            <svg className="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                                                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth="2" d="M12 4v16m8-8H4" />
                                            </svg>
                                            Añadir Carga
                                        </button>
                                    </div>
                                </div>

                                {loading.cargas && cargas.length === 0 ? (
                                    <div className="text-center py-10 text-gray-500">Cargando manifiesto...</div>
                                ) : cargas.length === 0 ? (
                                    <div className="text-center py-10 text-gray-500 bg-gray-50 rounded-lg">
                                        Este viaje no tiene cargas registradas en Mongo.
                                        <br />
                                        <span className="text-xs text-gray-400 mt-1 block">Usá el botón "Añadir Carga" para registrar la primera.</span>
                                    </div>
                                ) : (
                                    <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
                                        {cargas.map(carga => {
                                            const barcaza = esBarcaza(carga);
                                            return (
                                                <div key={carga.id} className="border border-gray-100 rounded-xl p-5 hover:shadow-md transition bg-gray-50/50 space-y-4">
                                                    <div className="flex justify-between items-start">
                                                        <div>
                                                            <p className="font-semibold text-gray-800 text-base">{carga.descripcionLista}</p>
                                                            <p className="text-xs text-gray-400 font-mono">ID: {carga.id}</p>
                                                            <span className={`inline-block mt-1 px-2 py-0.5 text-xs font-medium rounded-full ${barcaza ? 'bg-amber-100 text-amber-800' : 'bg-blue-100 text-blue-800'}`}>
                                                                {barcaza ? 'Barcaza' : 'Remolcador / Buque'}
                                                            </span>
                                                        </div>
                                                        <div dangerouslySetInnerHTML={{ __html: getRiesgoBadge(carga.nivelRiesgo) }} />
                                                    </div>

                                                    <div className="flex items-center justify-between text-sm bg-white p-3 rounded-lg border">
                                                        <span className="text-gray-500">Tonelaje Actual:</span>
                                                        <span className="font-bold text-gray-800">{carga.tonelaje} Tn</span>
                                                    </div>

                                                    <div className="flex items-center text-sm text-gray-600">
                                                        <IconoUbicacion />
                                                        <span>Estado: <strong className="text-[#002454]">{carga.muelleActual || 'Navegando / Fondeado'}</strong></span>
                                                    </div>

                                                    {/* ── BOTONES DIFERENCIADOS POR TIPO ── */}
                                                    {barcaza ? (
                                                        <div className="flex gap-2 pt-2 flex-wrap">
                                                            <button
                                                                onClick={() => abrirModalAccion(carga, 'cargar')}
                                                                className="flex-1 text-center bg-green-600 text-white px-3 py-2 rounded-lg text-xs font-semibold hover:bg-green-700 transition"
                                                            >
                                                                Cargar
                                                            </button>
                                                            <button
                                                                onClick={() => abrirModalAccion(carga, 'descargar')}
                                                                className="flex-1 text-center bg-amber-500 text-white px-3 py-2 rounded-lg text-xs font-semibold hover:bg-amber-600 transition"
                                                            >
                                                                Descargar
                                                            </button>
                                                            <button
                                                                onClick={() => abrirModalAccion(carga, 'amarrar_barcaza')}
                                                                className="flex-1 text-center bg-[#104a8e] text-white px-3 py-2 rounded-lg text-xs font-semibold hover:bg-[#002454] transition"
                                                            >
                                                                Amarrar
                                                            </button>
                                                        </div>
                                                    ) : (
                                                        <div className="flex flex-col gap-2 pt-2">
                                                            <div className="flex gap-2">
                                                                <button 
                                                                    onClick={() => abrirModalAccion(carga, 'cargar')}
                                                                    className="flex-1 text-center bg-green-600 text-white px-4 py-2 rounded-lg text-sm font-semibold hover:bg-green-700 transition"
                                                                >
                                                                    Cargar
                                                                </button>
                                                                <button 
                                                                    onClick={() => abrirModalAccion(carga, 'descargar')}
                                                                    className="flex-1 text-center bg-amber-500 text-white px-4 py-2 rounded-lg text-sm font-semibold hover:bg-amber-600 transition"
                                                                >
                                                                    Descargar
                                                                </button>
                                                            </div>
                                                            <div className="flex gap-2">
                                                                <button 
                                                                    onClick={() => abrirModalAccion(carga, 'amarrar_buque')}
                                                                    className="flex-1 text-center bg-[#104a8e] text-white px-4 py-2 rounded-lg text-sm font-semibold hover:bg-[#002454] transition disabled:opacity-50"
                                                                    disabled={!!carga.muelleActual}
                                                                >
                                                                    Amarrar
                                                                </button>
                                                                <button 
                                                                    onClick={() => abrirModalAccion(carga, 'fondear_buque')}
                                                                    className="flex-1 text-center bg-cyan-600 text-white px-4 py-2 rounded-lg text-sm font-semibold hover:bg-cyan-700 transition"
                                                                >
                                                                    Fondear
                                                                </button>
                                                            </div>
                                                        </div>
                                                    )}
                                                </div>
                                            );
                                        })}
                                    </div>
                                )}
                            </section>
                        )}
                    </>
                )}
            </main>

            {/* ── PIE DE PÁGINA ─────────────────────────────────────────── */}
            {vistaActual === 'dashboard' && (
                <footer className="border-t mt-12 p-6 bg-white text-center text-xs text-gray-400">
                    <p>&copy; 2026 Prefectura Naval Argentina - Dirección de Informática y Comunicaciones.</p>
                    <p className="mt-1">Sistema de Gestión de Tráfico Marítimo (MBPC) - Módulo de Modernización - v0.6.0</p>
                </footer>
            )}

            {/* ════════════════════════════════════════════════════════════
                MODAL ACCIONES DE CARGA (amarrar/fondear/cargar/descargar)
            ════════════════════════════════════════════════════════════ */}
            {modalState.show && (
                <Modal onClose={cerrarModal} maxWidth="max-w-lg">
                    <ModalHeader
                        titulo={getModalTitle()}
                        subtitulo={`ID: ${modalState.cargaId}`}
                        icono={<IconoUbicacion className="w-6 h-6 text-blue-300" />}
                        onClose={cerrarModal}
                    />
                    <div className="p-6 space-y-4">
                        {/* Amarrar Buque → pide Muelle */}
                        {modalState.tipo === 'amarrar_buque' && (
                            <>
                                <p className="text-sm text-gray-500">Indique el muelle de destino donde amarrará el remolcador/buque:</p>
                                <input type="text" placeholder="Ej: Muelle Alte. Storni - Sitio 3" className={inputCls}
                                    value={modalState.muelle}
                                    onChange={(e) => setModalState(prev => ({ ...prev, muelle: e.target.value }))}
                                    autoFocus />
                            </>
                        )}

                        {/* Fondear Buque → pide Zona */}
                        {modalState.tipo === 'fondear_buque' && (
                            <>
                                <p className="text-sm text-gray-500">Indique la zona de fondeo asignada al remolcador/buque:</p>
                                <input type="text" placeholder="Ej: Zona A - Ancla 7" className={inputCls}
                                    value={modalState.zona}
                                    onChange={(e) => setModalState(prev => ({ ...prev, zona: e.target.value }))}
                                    autoFocus />
                            </>
                        )}

                        {/* Cargar / Descargar → pide tonelaje final */}
                        {(modalState.tipo === 'cargar' || modalState.tipo === 'descargar') && (
                            <>
                                <p className="text-sm text-gray-500">
                                    {modalState.tipo === 'cargar'
                                        ? `Indique el tonelaje final de la carga. Debe ser mayor al actual (${modalState.tonelajeActual} Tn).`
                                        : `Indique el tonelaje final de la carga. Debe ser menor al actual (${modalState.tonelajeActual} Tn). Si es 0, pasará a estado EN LASTRE.`}
                                </p>
                                <div>
                                    <label className="block text-xs font-semibold text-gray-600 mb-1">
                                        Tonelaje Final <span className="text-red-500">*</span>
                                    </label>
                                    <input
                                        type="number"
                                        min="0"
                                        step="0.01"
                                        placeholder="Ej: 250.5"
                                        className={inputCls}
                                        value={modalState.toneladas}
                                        onChange={(e) => setModalState(prev => ({ ...prev, toneladas: e.target.value }))}
                                        autoFocus
                                    />
                                </div>
                            </>
                        )}

                        {/* Amarrar Barcaza → pide Fecha/Hora y Posición */}
                        {modalState.tipo === 'amarrar_barcaza' && (
                            <>
                                <p className="text-sm text-gray-500">Complete los datos de amarre de la barcaza:</p>
                                <div>
                                    <label className="block text-xs font-semibold text-gray-600 mb-1">Lugar de Amarre / Muelle <span className="text-red-500">*</span></label>
                                    <input type="text" placeholder="Ej: Muelle Sur - Sitio 4" className={inputCls}
                                        value={modalState.posicion}
                                        onChange={(e) => setModalState(prev => ({ ...prev, posicion: e.target.value }))} />
                                </div>
                                <div>
                                    <label className="block text-xs font-semibold text-gray-600 mb-1">Fecha y Hora de Amarre <span className="text-red-500">*</span></label>
                                    <input type="datetime-local" className={inputCls}
                                        value={modalState.fechaHora}
                                        onChange={(e) => setModalState(prev => ({ ...prev, fechaHora: e.target.value }))} />
                                </div>
                            </>
                        )}
                    </div>
                    <div className="bg-gray-50 px-6 py-4 flex flex-row-reverse gap-2 rounded-b-2xl">
                        <button
                            className="inline-flex items-center gap-2 px-5 py-2 bg-[#104a8e] hover:bg-[#002454] text-white text-sm font-semibold rounded-lg transition disabled:opacity-50"
                            onClick={ejecutarAccionCarga}
                            disabled={modalState.loading}
                        >
                            {modalState.loading ? <><IconoSpinner /> Ejecutando...</> : 'Confirmar Operación'}
                        </button>
                        <button
                            className="px-5 py-2 border border-gray-300 text-sm font-medium text-gray-700 rounded-lg hover:bg-gray-100 transition"
                            onClick={cerrarModal}
                            disabled={modalState.loading}
                        >
                            Cancelar
                        </button>
                    </div>
                </Modal>
            )}

            {/* ════════════════════════════════════════════════════════════
                MODAL BARCOS EN PUERTO
            ════════════════════════════════════════════════════════════ */}
            {modalPuerto.show && (
                <Modal onClose={cerrarModalPuerto} maxWidth="max-w-4xl">
                    <ModalHeader
                        titulo="Barcos en Puerto"
                        subtitulo="Unidades con estado Amarrado o Fondeado — Fuente: MongoDB (last_mbpc)"
                        icono={
                            <svg className="w-6 h-6 text-blue-300" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth="2" d="M3 12l2-2m0 0l7-7 7 7M5 10v10a1 1 0 001 1h3m10-11l2 2m-2-2v10a1 1 0 01-1 1h-3m-6 0a1 1 0 001-1v-4a1 1 0 011-1h2a1 1 0 011 1v4a1 1 0 001 1m-6 0h6" />
                            </svg>
                        }
                        onClose={cerrarModalPuerto}
                    />
                    <div className="p-6">
                        {modalPuerto.loading ? (
                            <div className="flex items-center justify-center py-16 text-gray-500 gap-3">
                                <IconoSpinner />
                                Consultando MongoDB...
                            </div>
                        ) : modalPuerto.datos.length === 0 ? (
                            <div className="text-center py-12 text-gray-500 bg-gray-50 rounded-xl">
                                No se encontraron barcos en puerto en este momento.
                            </div>
                        ) : (
                            <div className="overflow-x-auto">
                                <table className="w-full text-sm text-left">
                                    <thead className="text-xs text-gray-500 uppercase bg-gray-50">
                                        <tr>
                                            <th className="px-4 py-3">Buque</th>
                                            <th className="px-4 py-3">Origen</th>
                                            <th className="px-4 py-3">Destino</th>
                                            <th className="px-4 py-3">ETA</th>
                                            <th className="px-4 py-3">Estado</th>
                                            <th className="px-4 py-3">MMSI</th>
                                        </tr>
                                    </thead>
                                    <tbody className="divide-y divide-gray-100">
                                        {modalPuerto.datos.map((barco) => (
                                            <tr key={barco.id} className="hover:bg-blue-50 transition">
                                                <td className="px-4 py-3 font-semibold text-[#002454]">{barco.buque}</td>
                                                <td className="px-4 py-3 text-gray-600">{barco.origen}</td>
                                                <td className="px-4 py-3 text-gray-600">{barco.destino}</td>
                                                <td className="px-4 py-3 text-gray-600 font-mono text-xs">{barco.eta}</td>
                                                <td className="px-4 py-3">
                                                    <span className={`px-2.5 py-1 text-xs font-semibold rounded-full ${getEstadoBadgeClass(barco.estado)}`}>
                                                        {barco.estado}
                                                    </span>
                                                </td>
                                                <td className="px-4 py-3 text-gray-400 font-mono text-xs">{barco.mmsi}</td>
                                            </tr>
                                        ))}
                                    </tbody>
                                </table>
                                <p className="text-xs text-gray-400 mt-4 text-right">
                                    {modalPuerto.datos.length} registro(s) encontrado(s)
                                </p>
                            </div>
                        )}
                    </div>
                    <div className="bg-gray-50 px-6 py-4 flex justify-end rounded-b-2xl">
                        <button onClick={cerrarModalPuerto} className="px-5 py-2 border border-gray-300 text-sm font-medium text-gray-700 rounded-lg hover:bg-gray-100 transition">
                            Cerrar
                        </button>
                    </div>
                </Modal>
            )}

            {/* ════════════════════════════════════════════════════════════
                MODAL HISTÓRICO DE VIAJES
            ════════════════════════════════════════════════════════════ */}
            {modalHistorico.show && (
                <Modal onClose={cerrarModalHistorico} maxWidth="max-w-5xl">
                    <ModalHeader
                        titulo="Histórico de Viajes"
                        subtitulo="PKG_MBPC_VIAJES.SP_HISTORICO — Consulta multi-criterio a Oracle"
                        icono={
                            <svg className="w-6 h-6 text-blue-300" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth="2" d="M12 8v4l3 3m6-3a9 9 0 11-18 0 9 9 0 0118 0z" />
                            </svg>
                        }
                        onClose={cerrarModalHistorico}
                    />
                    <div className="p-6 space-y-6">
                        {/* Formulario de búsqueda */}
                        <div className="bg-blue-50 rounded-xl p-5 border border-blue-100">
                            <p className="text-xs font-bold text-[#002454] uppercase tracking-wider mb-4">Criterios de Búsqueda</p>
                            <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
                                <Campo label="Nombre del Buque">
                                    <input name="nombre" type="text" className={inputCls}
                                        placeholder="Ej: ARA Alte. Brown"
                                        value={filtroHistorico.nombre}
                                        onChange={handleFiltroHistoricoChange} />
                                </Campo>
                                <Campo label="N° OMI">
                                    <input name="omi" type="text" className={inputCls}
                                        placeholder="Ej: IMO9000001"
                                        value={filtroHistorico.omi}
                                        onChange={handleFiltroHistoricoChange} />
                                </Campo>
                                <Campo label="Matrícula">
                                    <input name="matricula" type="text" className={inputCls}
                                        placeholder="Ej: ARG-0001"
                                        value={filtroHistorico.matricula}
                                        onChange={handleFiltroHistoricoChange} />
                                </Campo>
                                <Campo label="Puerto de Origen">
                                    <input name="origen" type="text" className={inputCls}
                                        placeholder="Ej: Puerto Rosario"
                                        value={filtroHistorico.origen}
                                        onChange={handleFiltroHistoricoChange} />
                                </Campo>
                                <Campo label="Puerto de Destino">
                                    <input name="destino" type="text" className={inputCls}
                                        placeholder="Ej: Puerto Buenos Aires"
                                        value={filtroHistorico.destino}
                                        onChange={handleFiltroHistoricoChange} />
                                </Campo>
                                <div className="grid grid-cols-2 gap-2">
                                    <Campo label="Desde">
                                        <input name="desde" type="date" className={inputCls}
                                            value={filtroHistorico.desde}
                                            onChange={handleFiltroHistoricoChange} />
                                    </Campo>
                                    <Campo label="Hasta">
                                        <input name="hasta" type="date" className={inputCls}
                                            value={filtroHistorico.hasta}
                                            onChange={handleFiltroHistoricoChange} />
                                    </Campo>
                                </div>
                            </div>
                            <div className="mt-4 flex justify-end">
                                <button
                                    onClick={buscarHistorico}
                                    disabled={modalHistorico.loading}
                                    className="flex items-center gap-2 px-6 py-2 bg-[#002454] hover:bg-[#104a8e] text-white text-sm font-semibold rounded-lg transition disabled:opacity-50"
                                >
                                    {modalHistorico.loading ? (
                                        <><IconoSpinner /> Buscando...</>
                                    ) : (
                                        <>
                                            <svg className="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                                                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth="2" d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z" />
                                            </svg>
                                            Buscar
                                        </>
                                    )}
                                </button>
                            </div>
                        </div>

                        {/* Resultados */}
                        {modalHistorico.buscado && (
                            modalHistorico.resultados.length === 0 ? (
                                <div className="text-center py-10 text-gray-500 bg-gray-50 rounded-xl">
                                    No se encontraron viajes con los criterios ingresados.
                                </div>
                            ) : (
                                <div className="overflow-x-auto">
                                    <p className="text-xs text-gray-400 mb-3">{modalHistorico.resultados.length} resultado(s)</p>
                                    <table className="w-full text-sm text-left">
                                        <thead className="text-xs text-gray-500 uppercase bg-gray-50">
                                            <tr>
                                                <th className="px-4 py-3">Buque</th>
                                                <th className="px-4 py-3">OMI</th>
                                                <th className="px-4 py-3">Matrícula</th>
                                                <th className="px-4 py-3">Origen</th>
                                                <th className="px-4 py-3">Destino</th>
                                                <th className="px-4 py-3">Partida</th>
                                                <th className="px-4 py-3">ETA</th>
                                                <th className="px-4 py-3">Estado</th>
                                            </tr>
                                        </thead>
                                        <tbody className="divide-y divide-gray-100">
                                            {modalHistorico.resultados.map((v) => (
                                                <tr key={v.id} className="hover:bg-blue-50 transition">
                                                    <td className="px-4 py-3 font-semibold text-[#002454]">{v.buque}</td>
                                                    <td className="px-4 py-3 font-mono text-xs text-gray-500">{v.omi}</td>
                                                    <td className="px-4 py-3 font-mono text-xs text-gray-500">{v.matricula}</td>
                                                    <td className="px-4 py-3 text-gray-600">{v.origen}</td>
                                                    <td className="px-4 py-3 text-gray-600">{v.destino}</td>
                                                    <td className="px-4 py-3 text-gray-500 text-xs">{v.fechaPartida}</td>
                                                    <td className="px-4 py-3 text-gray-500 text-xs">{v.eta}</td>
                                                    <td className="px-4 py-3">
                                                        <span className={`px-2.5 py-1 text-xs font-semibold rounded-full ${getEstadoBadgeClass(v.estado)}`}>
                                                            {v.estado}
                                                        </span>
                                                    </td>
                                                </tr>
                                            ))}
                                        </tbody>
                                    </table>
                                </div>
                            )
                        )}
                    </div>
                    <div className="bg-gray-50 px-6 py-4 flex justify-end rounded-b-2xl">
                        <button onClick={cerrarModalHistorico} className="px-5 py-2 border border-gray-300 text-sm font-medium text-gray-700 rounded-lg hover:bg-gray-100 transition">
                            Cerrar
                        </button>
                    </div>
                </Modal>
            )}

            {/* ════════════════════════════════════════════════════════════
                MODAL AMARRAR BARCAZA GLOBAL
            ════════════════════════════════════════════════════════════ */}
            {modalAmarrarGlobal.show && (
                <Modal onClose={cerrarAmarrarGlobal} maxWidth="max-w-md">
                    <ModalHeader
                        titulo="Amarrar Barcaza"
                        subtitulo="Amarre global — PKG_MBPC_CARGAS.SP_AMARRAR"
                        icono={
                            <svg className="w-6 h-6 text-blue-300" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth="2" d="M13 10V3L4 14h7v7l9-11h-7z" />
                            </svg>
                        }
                        onClose={cerrarAmarrarGlobal}
                    />
                    <div className="p-6 space-y-4">
                        <p className="text-sm text-gray-500">
                            Complete los datos de amarre. Se enviará la operación a Oracle y se sincronizará MongoDB vía CQRS.
                        </p>
                        <Campo label="Nombre o ID de la Barcaza" required>
                            <input
                                type="text"
                                placeholder="Ej: BARCAZA NORTE IV"
                                className={inputCls}
                                value={modalAmarrarGlobal.barcazaId}
                                onChange={(e) => setModalAmarrarGlobal(prev => ({ ...prev, barcazaId: e.target.value }))}
                                autoFocus
                            />
                        </Campo>
                        <Campo label="Lugar de Amarre / Muelle" required>
                            <input
                                type="text"
                                placeholder="Ej: Muelle Alte. Storni - Sitio 2"
                                className={inputCls}
                                value={modalAmarrarGlobal.lugarAmarre}
                                onChange={(e) => setModalAmarrarGlobal(prev => ({ ...prev, lugarAmarre: e.target.value }))}
                            />
                        </Campo>
                        <Campo label="Fecha y Hora de Amarre" required>
                            <input
                                type="datetime-local"
                                className={inputCls}
                                value={modalAmarrarGlobal.fechaHora}
                                onChange={(e) => setModalAmarrarGlobal(prev => ({ ...prev, fechaHora: e.target.value }))}
                            />
                        </Campo>
                    </div>
                    <div className="bg-gray-50 px-6 py-4 flex flex-row-reverse gap-2 rounded-b-2xl">
                        <button
                            onClick={ejecutarAmarrarGlobal}
                            disabled={modalAmarrarGlobal.loading}
                            className="flex items-center gap-2 px-5 py-2 bg-[#002454] hover:bg-[#104a8e] text-white text-sm font-semibold rounded-lg transition disabled:opacity-50"
                        >
                            {modalAmarrarGlobal.loading ? <><IconoSpinner /> Amarrando...</> : 'Confirmar Amarre'}
                        </button>
                        <button
                            onClick={cerrarAmarrarGlobal}
                            disabled={modalAmarrarGlobal.loading}
                            className="px-5 py-2 border border-gray-300 text-sm font-medium text-gray-700 rounded-lg hover:bg-gray-100 transition"
                        >
                            Cancelar
                        </button>
                    </div>
                </Modal>
            )}

            {/* ════════════════════════════════════════════════════════════
                TAREA 2 — MODAL AÑADIR CARGA AL VIAJE
                POST /api/carga/viaje/{viajeNombreBuque}
            ════════════════════════════════════════════════════════════ */}
            {modalNuevaCarga.show && (
                <Modal onClose={cerrarModalNuevaCarga} maxWidth="max-w-md">
                    <ModalHeader
                        titulo="Añadir Carga al Viaje"
                        subtitulo={`PKG_MBPC_CARGAS.SP_AGREGAR_CARGA — Buque: ${viajeSeleccionado?.buque || '...'}`}
                        icono={<IconoCarga className="w-6 h-6 text-blue-300" />}
                        onClose={cerrarModalNuevaCarga}
                    />
                    <div className="p-6 space-y-4">
                        <p className="text-sm text-gray-500">
                            Registrá una nueva carga en Oracle y sincronizá el array <code className="bg-gray-100 px-1 rounded text-xs">barcazas</code> en MongoDB vía CQRS (Update.Push).
                        </p>

                        {/* Nombre / ID */}
                        <Campo label="Nombre o ID de la Carga" required>
                            <input
                                name="nombre"
                                type="text"
                                placeholder="Ej: BARCAZA SUR VII / BODEGA-01"
                                className={`${inputCls} ${nuevaCargaErrors.nombre ? 'border-red-400 ring-1 ring-red-300' : ''}`}
                                value={nuevaCargaForm.nombre}
                                onChange={handleNuevaCargaChange}
                                autoFocus
                            />
                            {nuevaCargaErrors.nombre && <p className="text-xs text-red-500 mt-1">{nuevaCargaErrors.nombre}</p>}
                        </Campo>

                        {/* Tipo */}
                        <Campo label="Tipo de Carga" required>
                            <select
                                name="tipo"
                                className={`${inputCls} ${nuevaCargaErrors.tipo ? 'border-red-400 ring-1 ring-red-300' : ''}`}
                                value={nuevaCargaForm.tipo}
                                onChange={handleNuevaCargaChange}
                            >
                                <option value="Barcaza">Barcaza</option>
                                <option value="Bodega">Bodega</option>
                            </select>
                            {nuevaCargaErrors.tipo && <p className="text-xs text-red-500 mt-1">{nuevaCargaErrors.tipo}</p>}
                        </Campo>

                        {/* Tonelaje */}
                        <Campo label="Tonelaje Inicial (Tn)" required>
                            <input
                                name="tonelaje"
                                type="number"
                                min="0"
                                step="0.01"
                                placeholder="Ej: 1250.00"
                                className={`${inputCls} ${nuevaCargaErrors.tonelaje ? 'border-red-400 ring-1 ring-red-300' : ''}`}
                                value={nuevaCargaForm.tonelaje}
                                onChange={handleNuevaCargaChange}
                            />
                            {nuevaCargaErrors.tonelaje && <p className="text-xs text-red-500 mt-1">{nuevaCargaErrors.tonelaje}</p>}
                        </Campo>

                        {/* Aviso CQRS */}
                        <div className="bg-blue-50 border border-blue-100 rounded-lg px-4 py-3 text-xs text-blue-700">
                            <strong>CQRS:</strong> Se escribirá en Oracle y se hará un <code>Update.Push</code> en <code>details_mbpc</code> para el buque <strong>{viajeSeleccionado?.buque}</strong>.
                        </div>
                    </div>
                    <div className="sticky bottom-0 bg-gray-50 border-t px-6 py-4 flex justify-end gap-3 rounded-b-2xl">
                        <button
                            onClick={cerrarModalNuevaCarga}
                            disabled={modalNuevaCarga.loading}
                            className="px-5 py-2 border border-gray-300 rounded-lg text-sm font-medium text-gray-700 hover:bg-gray-100 transition disabled:opacity-50"
                        >
                            Cancelar
                        </button>
                        <button
                            onClick={guardarNuevaCarga}
                            disabled={modalNuevaCarga.loading}
                            className="flex items-center gap-2 px-6 py-2 bg-green-600 hover:bg-green-700 text-white text-sm font-semibold rounded-lg transition disabled:opacity-50 shadow-sm"
                        >
                            {modalNuevaCarga.loading ? (
                                <><IconoSpinner /> Guardando...</>
                            ) : (
                                <>
                                    <svg className="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                                        <path strokeLinecap="round" strokeLinejoin="round" strokeWidth="2" d="M5 13l4 4L19 7" />
                                    </svg>
                                    Confirmar Carga
                                </>
                            )}
                        </button>
                    </div>
                </Modal>
            )}

            {/* ════════════════════════════════════════════════════════════
                MODAL NUEVO VIAJE — todos los campos del DTO expandido
            ════════════════════════════════════════════════════════════ */}
            {modalNuevoViaje.show && (
                <Modal onClose={cerrarNuevoViaje} maxWidth="max-w-3xl">
                    <ModalHeader
                        titulo="Nuevo Viaje"
                        subtitulo="PKG_MBPC_VIAJES.SP_CREAR_VIAJE — El buque nace con estado 'Amarrado'"
                        icono={
                            <svg className="w-6 h-6 text-blue-300" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth="2" d="M12 4v16m8-8H4" />
                            </svg>
                        }
                        onClose={cerrarNuevoViaje}
                    />

                    <div className="p-6 space-y-6">
                        {/* ── Datos del Buque ── */}
                        <fieldset>
                            <legend className="text-xs font-bold text-[#002454] uppercase tracking-wider mb-3 pb-1 border-b border-blue-100 w-full">Datos del Buque</legend>
                            <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
                                <Campo label="Nombre del Buque" required>
                                    <input name="nombreBuque" type="text"
                                        className={`${inputCls} ${nuevoViajeErrors.nombreBuque ? 'border-red-400 ring-1 ring-red-300' : ''}`}
                                        placeholder="Ej: ARA Gral. San Martín"
                                        value={nuevoViajeForm.nombreBuque} onChange={handleNuevoViajeChange} />
                                    {nuevoViajeErrors.nombreBuque && <p className="text-xs text-red-500 mt-1">{nuevoViajeErrors.nombreBuque}</p>}
                                </Campo>
                                <Campo label="Origen" required>
                                    <input name="origen" type="text"
                                        className={`${inputCls} ${nuevoViajeErrors.origen ? 'border-red-400 ring-1 ring-red-300' : ''}`}
                                        placeholder="Ej: Puerto Buenos Aires"
                                        value={nuevoViajeForm.origen} onChange={handleNuevoViajeChange} />
                                    {nuevoViajeErrors.origen && <p className="text-xs text-red-500 mt-1">{nuevoViajeErrors.origen}</p>}
                                </Campo>
                                <Campo label="Destino" required>
                                    <input name="destino" type="text"
                                        className={`${inputCls} ${nuevoViajeErrors.destino ? 'border-red-400 ring-1 ring-red-300' : ''}`}
                                        placeholder="Ej: Puerto Rosario"
                                        value={nuevoViajeForm.destino} onChange={handleNuevoViajeChange} />
                                    {nuevoViajeErrors.destino && <p className="text-xs text-red-500 mt-1">{nuevoViajeErrors.destino}</p>}
                                </Campo>
                            </div>
                        </fieldset>

                        {/* ── Muelle y Control ── */}
                        <fieldset>
                            <legend className="text-xs font-bold text-[#002454] uppercase tracking-wider mb-3 pb-1 border-b border-blue-100 w-full">Muelle y Control</legend>
                            <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                                <Campo label="Muelle de Salida (opcional)">
                                    <input name="muelleSalida" type="text" className={inputCls}
                                        placeholder="Ej: Muelle Alte. Storni - Sitio 2"
                                        value={nuevoViajeForm.muelleSalida} onChange={handleNuevoViajeChange} />
                                </Campo>
                                <Campo label="Próximo Punto de Control" required>
                                    <select name="proximoPuntoControl"
                                        className={`${inputCls} ${nuevoViajeErrors.proximoPuntoControl ? 'border-red-400 ring-1 ring-red-300' : ''}`}
                                        value={nuevoViajeForm.proximoPuntoControl} onChange={handleNuevoViajeChange}>
                                        <option value="">-- Seleccionar punto --</option>
                                        {PUNTOS_CONTROL_OPTIONS.map(opt => (
                                            <option key={opt.value} value={opt.value}>{opt.label}</option>
                                        ))}
                                    </select>
                                    {nuevoViajeErrors.proximoPuntoControl && <p className="text-xs text-red-500 mt-1">{nuevoViajeErrors.proximoPuntoControl}</p>}
                                </Campo>
                            </div>
                        </fieldset>

                        {/* ── Fechas ── */}
                        <fieldset>
                            <legend className="text-xs font-bold text-[#002454] uppercase tracking-wider mb-3 pb-1 border-b border-blue-100 w-full">Fechas y Tiempos</legend>
                            <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                                <Campo label="Fecha de Partida" required>
                                    <input name="fechaPartida" type="datetime-local"
                                        className={`${inputCls} ${nuevoViajeErrors.fechaPartida ? 'border-red-400 ring-1 ring-red-300' : ''}`}
                                        value={nuevoViajeForm.fechaPartida} onChange={handleNuevoViajeChange} />
                                    {nuevoViajeErrors.fechaPartida && <p className="text-xs text-red-500 mt-1">{nuevoViajeErrors.fechaPartida}</p>}
                                </Campo>
                                <Campo label="ETA (Tiempo Estimado de Arribo)" required>
                                    <input name="eta" type="datetime-local"
                                        className={`${inputCls} ${nuevoViajeErrors.eta ? 'border-red-400 ring-1 ring-red-300' : ''}`}
                                        value={nuevoViajeForm.eta} onChange={handleNuevoViajeChange} />
                                    {nuevoViajeErrors.eta && <p className="text-xs text-red-500 mt-1">{nuevoViajeErrors.eta}</p>}
                                </Campo>
                            </div>
                        </fieldset>

                        {/* ── Posición y Zona ── */}
                        <fieldset>
                            <legend className="text-xs font-bold text-[#002454] uppercase tracking-wider mb-3 pb-1 border-b border-blue-100 w-full">Posición y Zona</legend>
                            <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
                                <Campo label="ZOE (Zona de Operación Especial)">
                                    <input name="zoe" type="text" className={inputCls}
                                        placeholder="Ej: ZOE-LITORAL"
                                        value={nuevoViajeForm.zoe} onChange={handleNuevoViajeChange} />
                                </Campo>
                                <Campo label="Posición Inicial">
                                    <input name="posicion" type="text" className={inputCls}
                                        placeholder="Ej: 34°36'S 058°22'W"
                                        value={nuevoViajeForm.posicion} onChange={handleNuevoViajeChange} />
                                </Campo>
                                <Campo label="Río/Canal - Km Par">
                                    <input name="rioCanalKmPar" type="number" min="0" max="9999.9" step="0.1" className={inputCls}
                                        placeholder="Ej: 588.5"
                                        value={nuevoViajeForm.rioCanalKmPar} onChange={handleNuevoViajeChange} />
                                </Campo>
                            </div>
                        </fieldset>

                        {/* ── Declaración Malvinas ── */}
                        <fieldset>
                            <legend className="text-xs font-bold text-[#002454] uppercase tracking-wider mb-3 pb-1 border-b border-blue-100 w-full">Declaración Jurada de Malvinas</legend>
                            <Campo label="Código de Declaración" required>
                                <select name="declaracionMalvinas"
                                    className={`${inputCls} ${nuevoViajeErrors.declaracionMalvinas ? 'border-red-400 ring-1 ring-red-300' : ''}`}
                                    value={nuevoViajeForm.declaracionMalvinas} onChange={handleNuevoViajeChange}>
                                    <option value="">-- Seleccionar declaración --</option>
                                    {DECLARACION_MALVINAS_OPTIONS.map(opt => (
                                        <option key={opt.value} value={opt.value}>{opt.label}</option>
                                    ))}
                                </select>
                                {nuevoViajeErrors.declaracionMalvinas && <p className="text-xs text-red-500 mt-1">{nuevoViajeErrors.declaracionMalvinas}</p>}
                            </Campo>
                        </fieldset>
                    </div>

                    <div className="sticky bottom-0 bg-gray-50 border-t px-6 py-4 flex justify-end gap-3 rounded-b-2xl">
                        <button
                            type="button"
                            onClick={cerrarNuevoViaje}
                            disabled={modalNuevoViaje.loading}
                            className="px-5 py-2 border border-gray-300 rounded-lg text-sm font-medium text-gray-700 hover:bg-gray-100 transition disabled:opacity-50"
                        >
                            Cancelar
                        </button>
                        <button
                            type="button"
                            onClick={guardarNuevoViaje}
                            disabled={modalNuevoViaje.loading}
                            className="flex items-center gap-2 px-6 py-2 bg-[#002454] hover:bg-[#104a8e] text-white text-sm font-semibold rounded-lg transition disabled:opacity-50 shadow-sm"
                        >
                            {modalNuevoViaje.loading ? (
                                <><IconoSpinner /> Guardando...</>
                            ) : (
                                <>
                                    <svg className="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                                        <path strokeLinecap="round" strokeLinejoin="round" strokeWidth="2" d="M5 13l4 4L19 7" />
                                    </svg>
                                    Registrar Viaje
                                </>
                            )}
                        </button>
                    </div>
                </Modal>
            )}

            {/* CSS animación fade-in */}
            <style>{`
                .fade-in {
                    animation: fadeIn 0.5s ease-in-out;
                }
                @keyframes fadeIn {
                    from { opacity: 0; transform: translateY(10px); }
                    to { opacity: 1; transform: translateY(0); }
                }
            `}</style>
        </div>
    );
};

export default MbpcDashboard;