// ControlesEstadoBuque.jsx — Panel de Máquina de Estados para un Buque
// Props:
//   viajeId          {string} — ID del viaje / buque en la API
//   estadoActual     {string} — Estado actual del buque (para mostrar en el panel)
//   onEstadoCambiado {fn}     — Callback(accion, mensajeExito) invocado tras éxito

import React, { useState } from 'react';
import { viajesApi } from './axiosClient';

// ─── Icono Spinner ────────────────────────────────────────────────────────────
const IconoSpinner = () => (
    <svg className="animate-spin w-4 h-4" fill="none" viewBox="0 0 24 24">
        <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" />
        <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4z" />
    </svg>
);

// ─── Configuración de botones ────────────────────────────────────────────────
const BOTONES = [
    {
        accion: 'zarpar',
        label: 'Zarpar',
        colorBase: 'bg-green-600 hover:bg-green-700 border-green-700 text-white',
        colorDisabled: 'disabled:bg-green-300 disabled:border-green-300',
        icono: (
            <svg className="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth="2.5" d="M5 12h14M12 5l7 7-7 7" />
            </svg>
        ),
    },
    {
        accion: 'amarrar',
        label: 'Amarrar',
        colorBase: 'bg-[#104a8e] hover:bg-[#002454] border-[#104a8e] text-white',
        colorDisabled: 'disabled:bg-blue-300 disabled:border-blue-300',
        icono: (
            <svg className="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth="2.5" d="M13 10V3L4 14h7v7l9-11h-7z" />
            </svg>
        ),
    },
    {
        accion: 'fondear',
        label: 'Fondear',
        colorBase: 'bg-amber-500 hover:bg-amber-600 border-amber-600 text-white',
        colorDisabled: 'disabled:bg-amber-200 disabled:border-amber-200',
        icono: (
            <svg className="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth="2.5" d="M3 6h18M3 12h18M3 18h18" />
            </svg>
        ),
    },
    {
        accion: 'reanudar',
        label: 'Reanudar',
        colorBase: 'bg-cyan-600 hover:bg-cyan-700 border-cyan-700 text-white',
        colorDisabled: 'disabled:bg-cyan-200 disabled:border-cyan-200',
        icono: (
            <svg className="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth="2.5" d="M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15" />
            </svg>
        ),
    },
];

// Mapa de acción → método del cliente centralizado
const TRANSICIONES = {
    zarpar  : (id) => viajesApi.zarpar(id),
    amarrar : (id) => viajesApi.amarrar(id),
    fondear : (id) => viajesApi.fondear(id),
    reanudar: (id) => viajesApi.reanudar(id),
};

// ─── Componente Principal ─────────────────────────────────────────────────────
const ControlesEstadoBuque = ({ viajeId, estadoActual, onEstadoCambiado }) => {
    // accionProcesando: string | null — cuál botón está en vuelo
    const [accionProcesando, setAccionProcesando] = useState(null);
    // errorTransicion: string | null — mensaje de error del backend
    const [errorTransicion, setErrorTransicion] = useState(null);

    const ejecutarTransicion = async (accion) => {
        if (!viajeId) return;

        setAccionProcesando(accion);
        setErrorTransicion(null);

        try {
            const res = await TRANSICIONES[accion](viajeId);
            const mensajeExito = res.data?.mensaje || `'${accion}' ejecutado correctamente.`;

            if (typeof onEstadoCambiado === 'function') {
                await onEstadoCambiado(accion, mensajeExito);
            }
        } catch (err) {
            const status  = err?.response?.status;
            const mensaje = err?.response?.data?.mensaje;

            if (status === 422) {
                // Unprocessable Entity → transición no permitida por la máquina de estados
                setErrorTransicion(mensaje || `No se pudo ejecutar '${accion}'. Transición no permitida.`);
            } else if (status) {
                // Otro error HTTP con cuerpo del backend (401/403 ya los maneja el interceptor)
                setErrorTransicion(mensaje || `Error ${status} al ejecutar '${accion}'.`);
            } else {
                // Error de red (sin respuesta del servidor)
                console.error(`[ControlesEstadoBuque] Error en '${accion}':`, err);
                setErrorTransicion('Error de red. Verificá que el Backend esté corriendo.');
            }
        } finally {
            setAccionProcesando(null);
        }
    };

    const hayAccionEnVuelo = accionProcesando !== null;

    return (
        <div className="mt-6 pt-5 border-t border-blue-100">
            {/* ── Encabezado del panel ─────────────────────────────────── */}
            <div className="flex items-center gap-2 mb-3">
                <svg className="w-5 h-5 text-[#104a8e]" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth="2"
                        d="M9 17V7m0 10a2 2 0 01-2 2H5a2 2 0 01-2-2V7a2 2 0 012-2h2a2 2 0 012 2m0 10a2 2 0 002 2h2a2 2 0 002-2M9 7a2 2 0 012-2h2a2 2 0 012 2m0 10V7m0 10a2 2 0 002 2h2a2 2 0 002-2V7a2 2 0 00-2-2h-2a2 2 0 00-2 2" />
                </svg>
                <h4 className="text-sm font-bold text-gray-700 uppercase tracking-wide">
                    Controles de Estado del Buque
                </h4>
                {estadoActual && (
                    <span className="ml-auto text-xs font-semibold px-2.5 py-1 rounded-full bg-blue-100 text-[#104a8e]">
                        Estado actual: {estadoActual}
                    </span>
                )}
            </div>

            {/* ── Banner de error ──────────────────────────────────────── */}
            {errorTransicion && (
                <div className="mb-3 flex items-start gap-2 bg-red-50 border border-red-300 text-red-800 text-sm rounded-lg px-4 py-3">
                    <svg className="w-5 h-5 flex-shrink-0 mt-0.5 text-red-500" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                        <path strokeLinecap="round" strokeLinejoin="round" strokeWidth="2"
                            d="M12 8v4m0 4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z" />
                    </svg>
                    <span>{errorTransicion}</span>
                    <button
                        onClick={() => setErrorTransicion(null)}
                        className="ml-auto text-red-400 hover:text-red-600 transition"
                        aria-label="Cerrar error"
                    >
                        <svg className="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth="2" d="M6 18L18 6M6 6l12 12" />
                        </svg>
                    </button>
                </div>
            )}

            {/* ── Grid de botones ──────────────────────────────────────── */}
            <div className="grid grid-cols-2 sm:grid-cols-4 gap-3">
                {BOTONES.map(({ accion, label, colorBase, colorDisabled, icono }) => {
                    const esteBotonCargando = accionProcesando === accion;
                    const deshabilitado = hayAccionEnVuelo && !esteBotonCargando;

                    return (
                        <button
                            key={accion}
                            onClick={() => ejecutarTransicion(accion)}
                            disabled={hayAccionEnVuelo}
                            className={`
                                flex items-center justify-center gap-2
                                px-4 py-2.5 rounded-lg border text-sm font-semibold
                                transition duration-150 shadow-sm
                                ${colorBase}
                                ${colorDisabled}
                                ${deshabilitado ? 'opacity-40 cursor-not-allowed' : ''}
                            `}
                        >
                            {esteBotonCargando ? (
                                <>
                                    <IconoSpinner />
                                    <span>Procesando...</span>
                                </>
                            ) : (
                                <>
                                    {icono}
                                    <span>{label}</span>
                                </>
                            )}
                        </button>
                    );
                })}
            </div>
        </div>
    );
};

export default ControlesEstadoBuque;
