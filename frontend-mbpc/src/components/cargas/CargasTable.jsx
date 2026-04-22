import { useState, useEffect } from "react";
import axios from "axios";
import CargaEditModal from "./CargaEditModal";
import CargaDeleteModal from "./CargaDeleteModal";

/**
 * CargasTable
 *
 * Grilla de cargas de un viaje con integración completa de los modales
 * de edición y eliminación.
 *
 * Props:
 *   viajeId {string} – ID o nombre del buque para consultar las cargas.
 */
export default function CargasTable({ viajeId }) {
  // ── Estado principal ────────────────────────────────────────────────────
  const [cargas, setCargas] = useState([]);
  const [isLoading, setIsLoading] = useState(false);
  const [fetchError, setFetchError] = useState(null);

  // ── Control de modales ──────────────────────────────────────────────────
  // cargaSeleccionada guarda el objeto CargaDto activo para el modal abierto.
  const [cargaSeleccionada, setCargaSeleccionada] = useState(null);
  const [modalAbierto, setModalAbierto] = useState(null); // "editar" | "eliminar" | null

  // ── Carga de datos ──────────────────────────────────────────────────────
  const fetchCargas = async () => {
    if (!viajeId) return;
    setIsLoading(true);
    setFetchError(null);

    const token = localStorage.getItem("mbpc_token");

    try {
      const { data } = await axios.get(
        `/api/carga/viaje/${encodeURIComponent(viajeId)}`,
        { headers: { Authorization: `Bearer ${token}` } }
      );
      setCargas(data);
    } catch (err) {
      setFetchError(
        err.response?.data?.mensaje ?? "Error al cargar el manifiesto de cargas."
      );
    } finally {
      setIsLoading(false);
    }
  };

  useEffect(() => {
    fetchCargas();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [viajeId]);

  // ── Handlers de apertura de modales ─────────────────────────────────────
  const abrirEditar = (carga) => {
    setCargaSeleccionada(carga);
    setModalAbierto("editar");
  };

  const abrirEliminar = (carga) => {
    setCargaSeleccionada(carga);
    setModalAbierto("eliminar");
  };

  const cerrarModal = () => {
    setModalAbierto(null);
    setCargaSeleccionada(null);
  };

  // onSuccess: refrescar la grilla y cerrar modal
  const handleSuccess = () => {
    cerrarModal();
    fetchCargas();
  };

  // ── Helper: Badge de TipoUnidad ─────────────────────────────────────────
  /**
   * Devuelve el badge de Tailwind correspondiente al tipo de unidad.
   * Hito 5.7: diferenciación visual explícita de Bodega vs Barcaza.
   */
  const TipoUnidadBadge = ({ tipoUnidad }) => {
    if (tipoUnidad === "Bodega") {
      return (
        <span className="bg-blue-100 text-blue-800 text-xs font-semibold px-2 py-1 rounded mr-2">
          Bodega
        </span>
      );
    }
    return (
      <span className="bg-amber-100 text-amber-800 text-xs font-semibold px-2 py-1 rounded mr-2">
        Barcaza
      </span>
    );
  };

  // ── Render ───────────────────────────────────────────────────────────────
  if (isLoading) {
    return (
      <div className="flex items-center justify-center py-12 text-slate-500 text-sm">
        <svg
          className="mr-2 h-4 w-4 animate-spin"
          xmlns="http://www.w3.org/2000/svg"
          fill="none"
          viewBox="0 0 24 24"
        >
          <circle
            className="opacity-25"
            cx="12"
            cy="12"
            r="10"
            stroke="currentColor"
            strokeWidth="4"
          />
          <path
            className="opacity-75"
            fill="currentColor"
            d="M4 12a8 8 0 018-8v8H4z"
          />
        </svg>
        Cargando manifiesto...
      </div>
    );
  }

  if (fetchError) {
    return (
      <p className="rounded-md bg-red-50 px-4 py-3 text-sm font-medium text-red-600 ring-1 ring-red-200">
        {fetchError}
      </p>
    );
  }

  return (
    <>
      {/* ── Tabla ── */}
      <div className="overflow-x-auto rounded-md shadow-sm ring-1 ring-slate-200">
        <table className="min-w-full divide-y divide-slate-200 bg-white text-sm">
          <thead className="bg-slate-50">
            <tr>
              <th className="px-4 py-3 text-left font-semibold text-slate-600 uppercase tracking-wide text-xs">
                ID
              </th>
              <th className="px-4 py-3 text-left font-semibold text-slate-600 uppercase tracking-wide text-xs">
                Descripción
              </th>
              <th className="px-4 py-3 text-left font-semibold text-slate-600 uppercase tracking-wide text-xs">
                Muelle
              </th>
              <th className="px-4 py-3 text-right font-semibold text-slate-600 uppercase tracking-wide text-xs">
                Tonelaje
              </th>
              <th className="px-4 py-3 text-left font-semibold text-slate-600 uppercase tracking-wide text-xs">
                Riesgo
              </th>
              <th className="px-4 py-3 text-center font-semibold text-slate-600 uppercase tracking-wide text-xs">
                Acciones
              </th>
            </tr>
          </thead>

          <tbody className="divide-y divide-slate-100">
            {cargas.length === 0 ? (
              <tr>
                <td
                  colSpan={6}
                  className="px-4 py-8 text-center text-slate-400 italic"
                >
                  No hay cargas registradas para este viaje.
                </td>
              </tr>
            ) : (
              cargas.map((carga) => (
                // ── Fila de Carga ────────────────────────────────────────
                <tr
                  key={carga.id}
                  className="hover:bg-slate-50 transition-colors"
                >
                  {/* ID — Hito 5.7: se enmascara visualmente para Bodegas.
                      El valor real (carga.id) se preserva intacto en el objeto
                      para que los modales de edición y eliminación sigan funcionando. */}
                  <td className="px-4 py-3 font-mono text-slate-700">
                    {carga.tipoUnidad === "Bodega" ? (
                      <span className="italic text-slate-300">—</span>
                    ) : (
                      carga.id
                    )}
                  </td>

                  {/* Descripción — Hito 5.7: badge de TipoUnidad a la izquierda */}
                  <td className="px-4 py-3 text-slate-700">
                    <span className="flex items-center flex-wrap gap-y-1">
                      <TipoUnidadBadge tipoUnidad={carga.tipoUnidad} />
                      {carga.descripcionLista}
                    </span>
                  </td>

                  {/* Muelle */}
                  <td className="px-4 py-3 text-slate-500">
                    {carga.muelleActual ?? (
                      <span className="italic text-slate-300">—</span>
                    )}
                  </td>

                  {/* Tonelaje */}
                  <td className="px-4 py-3 text-right font-medium text-slate-700 tabular-nums">
                    {carga.tonelaje.toLocaleString("es-AR", {
                      minimumFractionDigits: 2,
                      maximumFractionDigits: 2,
                    })}{" "}
                    <span className="text-xs text-slate-400">Tn</span>
                  </td>

                  {/* Nivel de Riesgo */}
                  <td className="px-4 py-3">
                    <span
                      className={`inline-flex items-center rounded-full px-2.5 py-0.5 text-xs font-medium ${
                        carga.nivelRiesgo === "Alto"
                          ? "bg-red-100 text-red-700"
                          : carga.nivelRiesgo === "Medio"
                          ? "bg-amber-100 text-amber-700"
                          : "bg-green-100 text-green-700"
                      }`}
                    >
                      {carga.nivelRiesgo}
                    </span>
                  </td>

                  {/* Acciones */}
                  <td className="px-4 py-3">
                    <div className="flex items-center justify-center gap-2">
                      {/* Botón Editar */}
                      <button
                        type="button"
                        onClick={() => abrirEditar(carga)}
                        className="rounded-md bg-sky-50 px-3 py-1.5 text-xs font-medium text-sky-700 hover:bg-sky-100 ring-1 ring-sky-200 transition-colors"
                        aria-label={`Editar carga ${carga.id}`}
                      >
                        Editar
                      </button>

                      {/* Botón Eliminar */}
                      <button
                        type="button"
                        onClick={() => abrirEliminar(carga)}
                        className="rounded-md bg-red-50 px-3 py-1.5 text-xs font-medium text-red-700 hover:bg-red-100 ring-1 ring-red-200 transition-colors"
                        aria-label={`Eliminar carga ${carga.id}`}
                      >
                        Eliminar
                      </button>
                    </div>
                  </td>
                </tr>
              ))
            )}
          </tbody>
        </table>
      </div>

      {/* ── Modal de Edición ── */}
      {modalAbierto === "editar" && cargaSeleccionada && (
        <CargaEditModal
          carga={cargaSeleccionada}
          onClose={cerrarModal}
          onSuccess={handleSuccess}
        />
      )}

      {/* ── Modal de Eliminación ── */}
      {modalAbierto === "eliminar" && cargaSeleccionada && (
        <CargaDeleteModal
          carga={cargaSeleccionada}
          onClose={cerrarModal}
          onSuccess={handleSuccess}
        />
      )}
    </>
  );
}
