import React from "react";
import { Link } from "react-router-dom";
import InspeccionesTable from "../components/inspecciones/InspeccionesTable";

export default function InspeccionesPage() {
  return (
    <div className="flex-grow p-4 sm:p-6 lg:p-8 max-w-screen-2xl mx-auto w-full">
      {/* Encabezado */}
      <div className="mb-8 border-b border-slate-200 pb-5 flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4">
        <div>
          <h2 className="text-3xl font-extrabold text-slate-900 tracking-tight">
            Gestión de Inspecciones
          </h2>
          <p className="text-slate-500 mt-2 text-sm max-w-2xl">
            Visualización y administración de las inspecciones realizadas a los buques bajo jurisdicción de la costera.
          </p>
        </div>
        <Link
          to="/dashboard"
          className="inline-flex items-center justify-center gap-2 px-4 py-2 bg-[#002454] hover:bg-[#0b3166] text-white text-sm font-semibold rounded-lg border border-[#C8A84B]/40 hover:border-[#C8A84B] transition-all duration-200 shadow-sm shrink-0 h-fit"
        >
          <svg className="w-4 h-4 text-[#C8A84B]" fill="none" stroke="currentColor" strokeWidth="2.5" viewBox="0 0 24 24">
            <path strokeLinecap="round" strokeLinejoin="round" d="M10 19l-7-7m0 0l7-7m-7 7h18" />
          </svg>
          Volver al Dashboard
        </Link>
      </div>

      {/* Tabla de Inspecciones */}
      <InspeccionesTable />
    </div>
  );
}
