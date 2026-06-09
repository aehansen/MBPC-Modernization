import React, { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import apiClient from "../axiosClient";

/**
 * @typedef {Object} Muelle
 * @property {number} id
 * @property {string} nombre
 * @property {string} [codigo]
 * @property {string} [zona]
 * @property {number} [kmPar]
 * @property {number} [profundidadM]
 * @property {string} [estado]
 */

/**
 * @typedef {Object} PuntoControl
 * @property {number} id
 * @property {string} nombre
 */

// Funciones de llamada a la API
const fetchMuelles = async () => {
  const response = await apiClient.get("/catalogos/muelles");
  return response.data;
};

const fetchPuntosControl = async () => {
  const response = await apiClient.get("/catalogos/puntos-control");
  return response.data;
};

export default function Catalogos() {
  const [activeTab, setActiveTab] = useState("muelles");
  const [searchTerm, setSearchTerm] = useState("");

  // Consultas de TanStack Query v5
  const {
    data: muelles = [],
    isLoading: isLoadingMuelles,
    isError: isErrorMuelles,
    error: errorMuelles,
    refetch: refetchMuelles,
  } = useQuery({
    queryKey: ["catalogos", "muelles"],
    queryFn: fetchMuelles,
    staleTime: 60_000,
  });

  const {
    data: puntosControl = [],
    isLoading: isLoadingPuntos,
    isError: isErrorPuntos,
    error: errorPuntos,
    refetch: refetchPuntos,
  } = useQuery({
    queryKey: ["catalogos", "puntos-control"],
    queryFn: fetchPuntosControl,
    staleTime: 60_000,
  });

  const isLoading = activeTab === "muelles" ? isLoadingMuelles : isLoadingPuntos;
  const isError = activeTab === "muelles" ? isErrorMuelles : isErrorPuntos;
  const error = activeTab === "muelles" ? errorMuelles : errorPuntos;
  const refetch = activeTab === "muelles" ? refetchMuelles : refetchPuntos;

  // Filtrado local de datos según búsqueda
  const filteredData = (() => {
    const term = searchTerm.toLowerCase().trim();
    if (activeTab === "muelles") {
      return muelles.filter(
        (m) =>
          m.nombre?.toLowerCase().includes(term) ||
          m.id?.toString().includes(term) ||
          m.codigo?.toLowerCase().includes(term) ||
          m.zona?.toLowerCase().includes(term)
      );
    } else {
      return puntosControl.filter(
        (p) =>
          p.nombre?.toLowerCase().includes(term) || p.id?.toString().includes(term)
      );
    }
  })();

  return (
    <div className="flex-grow p-4 sm:p-6 lg:p-8 max-w-screen-2xl mx-auto w-full">
      {/* Encabezado */}
      <div className="mb-8 border-b border-slate-200 pb-5">
        <h2 className="text-3xl font-extrabold text-slate-900 tracking-tight">
          Administración de Catálogos
        </h2>
        <p className="text-slate-500 mt-2 text-sm max-w-2xl">
          Visualización y gestión de datos maestros del sistema MBPC. Consulte el estado de
          los muelles habilitados y los puntos de control para el tráfico fluvial.
        </p>
      </div>

      {/* Selector de Pestañas y Filtro de Búsqueda */}
      <div className="flex flex-col md:flex-row md:items-center justify-between gap-4 mb-6">
        {/* Pestañas */}
        <div className="flex bg-slate-100 p-1 rounded-xl w-fit">
          <button
            onClick={() => {
              setActiveTab("muelles");
              setSearchTerm("");
            }}
            className={`flex items-center gap-2 px-5 py-2.5 rounded-lg text-sm font-semibold transition-all duration-200 ${
              activeTab === "muelles"
                ? "bg-white text-blue-700 shadow-sm border border-slate-200/50"
                : "text-slate-600 hover:text-slate-900"
            }`}
          >
            <svg
              className="w-4 h-4"
              fill="none"
              stroke="currentColor"
              strokeWidth="2"
              viewBox="0 0 24 24"
            >
              <path
                strokeLinecap="round"
                strokeLinejoin="round"
                d="M19 11H5m14 0a2 2 0 012 2v6a2 2 0 01-2 2H5a2 2 0 01-2-2v-6a2 2 0 012-2m14 0V9a2 2 0 00-2-2M5 11V9a2 2 0 012-2m0 0V5a2 2 0 012-2h6a2 2 0 012 2v2M7 7h10"
              />
            </svg>
            Muelles ({muelles.length})
          </button>
          <button
            onClick={() => {
              setActiveTab("puntos-control");
              setSearchTerm("");
            }}
            className={`flex items-center gap-2 px-5 py-2.5 rounded-lg text-sm font-semibold transition-all duration-200 ${
              activeTab === "puntos-control"
                ? "bg-white text-blue-700 shadow-sm border border-slate-200/50"
                : "text-slate-600 hover:text-slate-900"
            }`}
          >
            <svg
              className="w-4 h-4"
              fill="none"
              stroke="currentColor"
              strokeWidth="2"
              viewBox="0 0 24 24"
            >
              <path
                strokeLinecap="round"
                strokeLinejoin="round"
                d="M9 12.75L11.25 15 15 9.75m-3-7.036A11.959 11.959 0 013.598 6 11.99 11.99 0 003 9.749c0 5.592 3.824 10.29 9 11.623 5.176-1.332 9-6.03 9-11.622 0-1.31-.21-2.57-.598-3.751h-.152c-3.196 0-6.1-1.248-8.25-3.285z"
              />
            </svg>
            Puntos de Control ({puntosControl.length})
          </button>
        </div>

        {/* Input de Búsqueda */}
        <div className="relative max-w-md w-full md:w-80">
          <span className="absolute inset-y-0 left-0 flex items-center pl-3 pointer-events-none">
            <svg
              className="w-5 h-5 text-slate-400"
              fill="none"
              stroke="currentColor"
              viewBox="0 0 24 24"
            >
              <path
                strokeLinecap="round"
                strokeLinejoin="round"
                strokeWidth="2"
                d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z"
              />
            </svg>
          </span>
          <input
            type="text"
            value={searchTerm}
            onChange={(e) => setSearchTerm(e.target.value)}
            placeholder={`Buscar en ${activeTab === "muelles" ? "muelles" : "puntos de control"}...`}
            className="w-full pl-10 pr-4 py-2 text-sm bg-white border border-slate-300 rounded-lg focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500 text-slate-900 transition-all placeholder-slate-400 shadow-sm"
          />
          {searchTerm && (
            <button
              onClick={() => setSearchTerm("")}
              className="absolute inset-y-0 right-0 flex items-center pr-3 text-slate-400 hover:text-slate-600"
            >
              <svg className="w-4 h-4" fill="currentColor" viewBox="0 0 20 20">
                <path
                  fillRule="evenodd"
                  d="M10 18a8 8 0 100-16 8 8 0 000 16zM8.707 7.293a1 1 0 00-1.414 1.414L8.586 10l-1.293 1.293a1 1 0 101.414 1.414L10 11.414l1.293 1.293a1 1 0 001.414-1.414L11.414 10l1.293-1.293a1 1 0 00-1.414-1.414L10 8.586 8.707 7.293z"
                  clipRule="evenodd"
                />
              </svg>
            </button>
          )}
        </div>
      </div>

      {/* Contenido Principal */}
      <div className="bg-white rounded-2xl border border-slate-200/80 shadow-sm overflow-hidden">
        {/* Estado de Error */}
        {isError && (
          <div className="p-8 text-center">
            <div className="inline-flex items-center justify-center w-12 h-12 rounded-full bg-red-100 text-red-600 mb-4">
              <svg
                className="w-6 h-6"
                fill="none"
                stroke="currentColor"
                strokeWidth="2"
                viewBox="0 0 24 24"
              >
                <path
                  strokeLinecap="round"
                  strokeLinejoin="round"
                  d="M12 9v2m0 4h.01m-6.938 4h13.856c1.54 0 2.502-1.667 1.732-3L13.732 4c-.77-1.333-2.694-1.333-3.464 0L3.34 16c-.77 1.333.192 3 1.732 3z"
                />
              </svg>
            </div>
            <h3 className="text-lg font-bold text-slate-900">Error al cargar datos</h3>
            <p className="text-sm text-slate-500 mt-2 max-w-md mx-auto">
              {error?.message || "Ocurrió un error al intentar obtener la información de la base de datos."}
            </p>
            <button
              onClick={() => refetch()}
              className="mt-4 inline-flex items-center gap-2 px-4 py-2 border border-transparent text-sm font-semibold rounded-lg text-white bg-blue-600 hover:bg-blue-700 shadow-sm transition-colors"
            >
              Reintentar
            </button>
          </div>
        )}

        {/* Estado de Carga (Skeleton Loader) */}
        {!isError && isLoading && (
          <div className="w-full">
            {/* Header del loader */}
            <div className="bg-slate-50 h-12 border-b border-slate-200 grid grid-cols-6 items-center px-6 gap-4">
              <div className="h-4 bg-slate-200 rounded animate-pulse col-span-1"></div>
              <div className="h-4 bg-slate-200 rounded animate-pulse col-span-3"></div>
              <div className="h-4 bg-slate-200 rounded animate-pulse col-span-1"></div>
              <div className="h-4 bg-slate-200 rounded animate-pulse col-span-1"></div>
            </div>
            {/* Filas del loader */}
            {[...Array(5)].map((_, i) => (
              <div
                key={i}
                className="h-16 border-b border-slate-100 grid grid-cols-6 items-center px-6 gap-4 last:border-0"
              >
                <div className="h-4 bg-slate-100 rounded animate-pulse col-span-1"></div>
                <div className="h-4 bg-slate-100 rounded animate-pulse col-span-3"></div>
                <div className="h-4 bg-slate-100 rounded animate-pulse col-span-1"></div>
                <div className="h-4 bg-slate-100 rounded animate-pulse col-span-1"></div>
              </div>
            ))}
          </div>
        )}

        {/* Datos Cargados */}
        {!isError && !isLoading && (
          <>
            {filteredData.length === 0 ? (
              // Búsqueda o grilla vacía
              <div className="p-12 text-center text-slate-500">
                <svg
                  className="w-12 h-12 text-slate-300 mx-auto mb-4"
                  fill="none"
                  stroke="currentColor"
                  viewBox="0 0 24 24"
                >
                  <path
                    strokeLinecap="round"
                    strokeLinejoin="round"
                    strokeWidth="1.5"
                    d="M20 13V6a2 2 0 00-2-2H6a2 2 0 00-2 2v7m16 0v5a2 2 0 01-2 2H6a2 2 0 01-2-2v-5m16 0h-2.586a1 1 0 00-.707.293l-2.414 2.414a1 1 0 01-.707.293h-3.172a1 1 0 01-.707-.293l-2.414-2.414A1 1 0 006.586 13H4"
                  />
                </svg>
                <h4 className="text-base font-bold text-slate-800">
                  No se encontraron resultados
                </h4>
                <p className="text-sm text-slate-400 mt-1 max-w-sm mx-auto">
                  Probá ajustando tu búsqueda o validando los filtros aplicados.
                </p>
              </div>
            ) : activeTab === "muelles" ? (
              // Tabla de Muelles
              <div className="overflow-x-auto">
                <table className="w-full text-left border-collapse">
                  <thead>
                    <tr className="bg-slate-50 text-slate-600 font-bold text-xs uppercase tracking-wider border-b border-slate-200">
                      <th className="py-4 px-6 w-24">ID</th>
                      <th className="py-4 px-6">Nombre de Instalación</th>
                      <th className="py-4 px-6 w-36">Código</th>
                      <th className="py-4 px-6 w-44">Zona</th>
                      <th className="py-4 px-6 w-32 text-right">Ubicación (Km)</th>
                      <th className="py-4 px-6 w-36 text-center">Estado</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-slate-100 text-sm text-slate-700">
                    {filteredData.map((m) => (
                      <tr
                        key={m.id}
                        className="hover:bg-slate-50/70 transition-colors duration-150"
                      >
                        <td className="py-3.5 px-6 font-mono text-xs text-slate-500 font-bold">
                          {m.id}
                        </td>
                        <td className="py-3.5 px-6 font-semibold text-slate-900">
                          {m.nombre}
                        </td>
                        <td className="py-3.5 px-6">
                          {m.codigo ? (
                            <span className="font-mono text-xs px-2 py-1 bg-slate-100 text-slate-600 rounded">
                              {m.codigo}
                            </span>
                          ) : (
                            <span className="text-slate-400">—</span>
                          )}
                        </td>
                        <td className="py-3.5 px-6 text-slate-600">{m.zona || "—"}</td>
                        <td className="py-3.5 px-6 text-right font-mono font-medium">
                          {m.kmPar !== undefined && m.kmPar !== null
                            ? m.kmPar.toLocaleString(undefined, { minimumFractionDigits: 1 })
                            : "—"}
                        </td>
                        <td className="py-3.5 px-6 text-center">
                          <span
                            className={`inline-flex items-center gap-1.5 px-2.5 py-0.5 rounded-full text-xs font-semibold ${
                              m.estado === "Inactivo"
                                ? "bg-red-50 text-red-700 border border-red-200"
                                : "bg-green-50 text-green-700 border border-green-200"
                            }`}
                          >
                            <span
                              className={`w-1.5 h-1.5 rounded-full ${
                                m.estado === "Inactivo" ? "bg-red-500" : "bg-green-500"
                              }`}
                            />
                            {m.estado || "Activo"}
                          </span>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            ) : (
              // Tabla de Puntos de Control
              <div className="overflow-x-auto">
                <table className="w-full text-left border-collapse">
                  <thead>
                    <tr className="bg-slate-50 text-slate-600 font-bold text-xs uppercase tracking-wider border-b border-slate-200">
                      <th className="py-4 px-6 w-32">ID</th>
                      <th className="py-4 px-6">Denominación del Punto</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-slate-100 text-sm text-slate-700">
                    {filteredData.map((p) => (
                      <tr
                        key={p.id}
                        className="hover:bg-slate-50/70 transition-colors duration-150"
                      >
                        <td className="py-4 px-6 font-mono text-xs text-slate-500 font-bold">
                          {p.id}
                        </td>
                        <td className="py-4 px-6 font-semibold text-slate-900">
                          {p.nombre}
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </>
        )}
      </div>
    </div>
  );
}
