// src/pages/ViajesPage.tsx
import { useState } from "react";
import MapaAIS         from "../MapaAIS.jsx";
import ViajesDashboard from "../components/viajes/ViajesDashboard";
import ModalHistorico  from "../components/viajes/ModalHistorico";

type Vista = "dashboard" | "mapa";

export default function ViajesPage() {
  const [vistaActual, setVistaActual]     = useState<Vista>("dashboard");
  const [showHistorico, setShowHistorico] = useState(false);

  const toggleVista = () =>
    setVistaActual((v) => (v === "mapa" ? "dashboard" : "mapa"));

  return (
    <div className="flex flex-col flex-grow">

      {/* ════════════════════════════════════════════════════════════════════
          BOTONERA SUPERIOR
      ════════════════════════════════════════════════════════════════════ */}
      <div className="bg-[#002454] border-t border-blue-800 px-6 py-2 flex items-center gap-2 flex-wrap">

        {/* ── 1. Ver Mapa AIS / Volver al Dashboard ──────────────────────── */}
        <button
          onClick={toggleVista}
          className={`flex items-center gap-1.5 px-4 py-1.5 text-white text-xs font-semibold rounded transition border ${
            vistaActual === "mapa"
              ? "bg-amber-600 hover:bg-amber-700 border-amber-500"
              : "bg-[#104a8e] hover:bg-[#1a5fa8] border-blue-600"
          }`}
        >
          {vistaActual === "mapa" ? (
            <>
              <svg className="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth="2"
                  d="M10 19l-7-7m0 0l7-7m-7 7h18" />
              </svg>
              Volver al Dashboard
            </>
          ) : (
            <>
              <svg className="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth="2"
                  d="M9 20l-5.447-2.724A1 1 0 013 16.382V5.618a1 1 0 011.447-.894L9 7m0 13l6-3m-6 3V7m6 10l4.553 2.276A1 1 0 0021 18.382V7.618a1 1 0 00-.553-.894L15 4m0 13V4m0 0L9 7" />
              </svg>
              Ver Mapa AIS
            </>
          )}
        </button>

        {/* ── 2. Amarrar Barcaza ─────────────────────────────────────────── */}
        <button
          onClick={() => alert("Funcionalidad en migración")}
          className="flex items-center gap-1.5 px-4 py-1.5 bg-[#104a8e] hover:bg-[#1a5fa8] text-white text-xs font-semibold rounded transition border border-blue-600"
        >
          <svg className="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth="2"
              d="M13 10V3L4 14h7v7l9-11h-7z" />
          </svg>
          Amarrar Barcaza
        </button>

        {/* ── 3. Nuevo Viaje ─────────────────────────────────────────────── */}
        <button
          onClick={() => alert("Funcionalidad en migración")}
          className="flex items-center gap-1.5 px-4 py-1.5 bg-green-600 hover:bg-green-700 text-white text-xs font-semibold rounded transition border border-green-500"
        >
          <svg className="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth="2"
              d="M12 4v16m8-8H4" />
          </svg>
          Nuevo Viaje
        </button>

        {/* ── 4. Barcos en Puerto ────────────────────────────────────────── */}
        <button
          onClick={() => alert("Funcionalidad en migración")}
          className="flex items-center gap-1.5 px-4 py-1.5 bg-[#104a8e] hover:bg-[#1a5fa8] text-white text-xs font-semibold rounded transition border border-blue-600"
        >
          <svg className="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth="2"
              d="M3 12l2-2m0 0l7-7 7 7M5 10v10a1 1 0 001 1h3m10-11l2 2m-2-2v10a1 1 0 01-1 1h-3m-6 0a1 1 0 001-1v-4a1 1 0 011-1h2a1 1 0 011 1v4a1 1 0 001 1m-6 0h6" />
          </svg>
          Barcos en Puerto
        </button>

        {/* ── 5. Viaje Histórico — abre ModalHistorico ───────────────────── */}
        <button
          onClick={() => setShowHistorico(true)}
          className="flex items-center gap-1.5 px-4 py-1.5 bg-[#104a8e] hover:bg-[#1a5fa8] text-white text-xs font-semibold rounded transition border border-blue-600"
        >
          <svg className="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth="2"
              d="M12 8v4l3 3m6-3a9 9 0 11-18 0 9 9 0 0118 0z" />
          </svg>
          Viaje (Histórico)
        </button>

      </div>{/* /botonera */}

      {/* ════════════════════════════════════════════════════════════════════
          CUERPO
      ════════════════════════════════════════════════════════════════════ */}
      {vistaActual === "mapa" ? (
        <div style={{ height: "calc(100vh - 104px)" }} className="flex-grow">
          <MapaAIS />
        </div>
      ) : (
        <div className="flex-grow p-6 md:p-8 space-y-8">
          <ViajesDashboard />
        </div>
      )}

      {/* ════════════════════════════════════════════════════════════════════
          MODAL HISTÓRICO
      ════════════════════════════════════════════════════════════════════ */}
      {showHistorico && (
        <ModalHistorico onClose={() => setShowHistorico(false)} />
      )}

    </div>
  );
}