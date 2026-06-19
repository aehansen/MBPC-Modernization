import { useState, useEffect } from "react";
import { useForm } from "react-hook-form";
import {
  useViajeComplementos,
  useAgregarNotaBitacora,
  useAsignarAgencias,
  useObtenerEtapas,
  useIntercalarEtapa,
} from "../../hooks/viajes/useViajeComplementos";
import {
  NotaBitacora,
  AgregarNotaBitacoraDto,
  Agencia,
  AsignarAgenciaDto,
  ViajeComplementos,
} from "../../types/complementos.types";
import PbipForm from "./pbip/PbipForm";
import EmbarcacionSelect from "./EmbarcacionSelect";

// ─── PROPS ────────────────────────────────────────────────────────────────────

interface ModalComplementosViajeProps {
  isOpen: boolean;
  onClose: () => void;
  viajeId: string;
}

// ─── SUB-COMPONENTES ──────────────────────────────────────────────────────────

// Categorías disponibles para la bitácora
const CATEGORIAS_BITACORA = ["Operacional", "Seguridad", "Administrativo", "Técnico", "Otro"];

function TabBitacora({
  viajeId,
  notas,
}: {
  viajeId: string;
  notas: NotaBitacora[];
}) {
  const { register, handleSubmit, reset, formState: { errors } } = useForm<AgregarNotaBitacoraDto>({
    defaultValues: { texto: "", categoria: "Operacional" },
  });

  const mutation = useAgregarNotaBitacora(viajeId);

  return (
    <div className="flex flex-col gap-5">
      {/* Lista de notas */}
      <div className="flex flex-col gap-2 max-h-72 overflow-y-auto pr-1 custom-scroll">
        {notas.length === 0 && (
          <p className="text-slate-500 text-sm italic text-center py-6">
            Sin notas registradas aún.
          </p>
        )}
        {notas.map((nota) => (
          <div
            key={nota.id}
            className="bg-slate-800/60 border border-slate-700/50 rounded-lg p-3 flex flex-col gap-1"
          >
            <div className="flex items-center justify-between gap-2">
              <span className="text-xs font-semibold px-2 py-0.5 rounded-full bg-cyan-900/60 text-cyan-300 border border-cyan-800/50">
                {nota.categoria}
              </span>
              <span className="text-xs text-slate-500">
                {new Date(nota.fechaHora).toLocaleString("es-AR")}
              </span>
            </div>
            <p className="text-slate-200 text-sm leading-relaxed">{nota.texto}</p>
            <p className="text-xs text-slate-500">
              <span className="text-slate-400 font-medium">@{nota.usuario}</span>
            </p>
          </div>
        ))}
      </div>

      {/* Formulario nueva nota */}
      <form
        onSubmit={handleSubmit((data) => mutation.mutate(data, { onSuccess: () => reset() }))}
        className="flex flex-col gap-3 border-t border-slate-700/50 pt-4"
      >
        <p className="text-xs font-semibold text-slate-400 uppercase tracking-widest">
          Nueva entrada de bitácora
        </p>
        <div className="flex gap-2">
          <div className="flex-1">
            <textarea
              {...register("texto", { required: "El texto es obligatorio.", minLength: { value: 5, message: "Mínimo 5 caracteres." } })}
              placeholder="Redactar observación..."
              rows={3}
              className="w-full bg-slate-900 border border-slate-700 rounded-lg px-3 py-2 text-sm text-slate-200 placeholder-slate-600 focus:outline-none focus:ring-1 focus:ring-cyan-500 resize-none"
            />
            {errors.texto && (
              <p className="text-red-400 text-xs mt-1">{errors.texto.message}</p>
            )}
          </div>
          <div className="flex flex-col gap-2 w-36">
            <select
              {...register("categoria", { required: true })}
              className="bg-slate-900 border border-slate-700 rounded-lg px-2 py-1.5 text-sm text-slate-300 focus:outline-none focus:ring-1 focus:ring-cyan-500"
            >
              {CATEGORIAS_BITACORA.map((cat) => (
                <option key={cat} value={cat}>{cat}</option>
              ))}
            </select>
            <button
              type="submit"
              disabled={mutation.isPending}
              className="flex-1 bg-cyan-600 hover:bg-cyan-500 disabled:opacity-50 disabled:cursor-not-allowed text-white text-sm font-semibold rounded-lg px-3 py-1.5 transition-colors"
            >
              {mutation.isPending ? (
                <span className="flex items-center justify-center gap-1.5">
                  <SpinnerIcon /> Guardando…
                </span>
              ) : (
                "＋ Agregar"
              )}
            </button>
          </div>
        </div>
        {mutation.isError && (
          <p className="text-red-400 text-xs">
            Error al guardar: {(mutation.error as Error).message}
          </p>
        )}
        {mutation.isSuccess && (
          <p className="text-emerald-400 text-xs">✓ Nota registrada correctamente.</p>
        )}
      </form>
    </div>
  );
}

function TabAgencias({
  viajeId,
  agencias,
}: {
  viajeId: string;
  agencias: Agencia[];
}) {
  const [draftAgencias, setDraftAgencias] = useState<Agencia[]>(agencias);
  const [nuevaAgencia, setNuevaAgencia] = useState<Agencia>({
    rol: "",
    nombre: "",
    contacto: "",
  });
  // Estado que rastrea qué fila está en modo edición (null = ninguna)
  const [editingIndex, setEditingIndex] = useState<number | null>(null);
  // Buffer temporal para los cambios en edición (evita mutar draftAgencias hasta confirmar)
  const [editBuffer, setEditBuffer] = useState<Agencia>({ rol: "", nombre: "", contacto: "" });

  const mutation = useAsignarAgencias(viajeId);

  // Sincronizar el draft cuando las agencias provistas por el servidor cambian
  useEffect(() => {
    setDraftAgencias(agencias);
    setEditingIndex(null);
  }, [agencias]);

  const handleAgregar = (e: React.FormEvent) => {
    e.preventDefault();
    if (
      !nuevaAgencia.rol.trim() ||
      !nuevaAgencia.nombre.trim() ||
      !nuevaAgencia.contacto.trim()
    ) {
      return;
    }
    setDraftAgencias([...draftAgencias, { ...nuevaAgencia }]);
    setNuevaAgencia({ rol: "", nombre: "", contacto: "" });
  };

  const handleEliminar = (index: number) => {
    // Si se elimina la fila en edición, limpiar el estado de edición
    if (editingIndex === index) setEditingIndex(null);
    setDraftAgencias(draftAgencias.filter((_, i) => i !== index));
  };

  const handleIniciarEdicion = (index: number) => {
    setEditingIndex(index);
    setEditBuffer({ ...draftAgencias[index] });
  };

  const handleGuardarEdicion = (index: number) => {
    if (!editBuffer.rol.trim() || !editBuffer.nombre.trim() || !editBuffer.contacto.trim()) return;
    const updated = draftAgencias.map((ag, i) => (i === index ? { ...editBuffer } : ag));
    setDraftAgencias(updated);
    setEditingIndex(null);
  };

  const handleCancelarEdicion = () => {
    setEditingIndex(null);
    setEditBuffer({ rol: "", nombre: "", contacto: "" });
  };

  const isDirty = JSON.stringify(draftAgencias) !== JSON.stringify(agencias);

  // Clase compartida para los inputs de edición inline (mismo estilo que el formulario de alta)
  const inputClass =
    "bg-slate-900 border border-slate-700 rounded-lg px-3 py-1.5 text-sm text-slate-200 placeholder-slate-600 focus:outline-none focus:ring-1 focus:ring-cyan-500 w-full";

  return (
    <div className="flex flex-col gap-6">
      {/* Tabla Interactiva */}
      <div className="overflow-x-auto">
        {draftAgencias.length === 0 ? (
          <p className="text-slate-500 text-sm italic text-center py-6">
            Sin agencias asignadas.
          </p>
        ) : (
          <table className="w-full text-sm">
            <thead>
              <tr className="border-b border-slate-700">
                <th className="text-left text-xs font-semibold text-slate-500 uppercase tracking-wider pb-2 pr-4">
                  Rol
                </th>
                <th className="text-left text-xs font-semibold text-slate-500 uppercase tracking-wider pb-2 pr-4">
                  Agencia
                </th>
                <th className="text-left text-xs font-semibold text-slate-500 uppercase tracking-wider pb-2 pr-4">
                  Contacto
                </th>
                <th className="text-right text-xs font-semibold text-slate-500 uppercase tracking-wider pb-2">
                  Acciones
                </th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-800">
              {draftAgencias.map((ag, i) => (
                <tr
                  key={i}
                  className={`transition-colors ${
                    editingIndex === i ? "bg-slate-800/70" : "hover:bg-slate-800/40"
                  }`}
                >
                  {editingIndex === i ? (
                    // ── Modo edición: inputs controlados ──────────────────────
                    <>
                      <td className="py-2 pr-3">
                        <input
                          type="text"
                          value={editBuffer.rol}
                          onChange={(e) =>
                            setEditBuffer({ ...editBuffer, rol: e.target.value })
                          }
                          placeholder="Rol"
                          className={inputClass}
                        />
                      </td>
                      <td className="py-2 pr-3">
                        <input
                          type="text"
                          value={editBuffer.nombre}
                          onChange={(e) =>
                            setEditBuffer({ ...editBuffer, nombre: e.target.value })
                          }
                          placeholder="Nombre de la Agencia"
                          className={inputClass}
                        />
                      </td>
                      <td className="py-2 pr-3">
                        <input
                          type="text"
                          value={editBuffer.contacto}
                          onChange={(e) =>
                            setEditBuffer({ ...editBuffer, contacto: e.target.value })
                          }
                          placeholder="Contacto (Tlf / Email)"
                          className={inputClass}
                        />
                      </td>
                      <td className="py-2 text-right">
                        <div className="flex items-center justify-end gap-2">
                          <button
                            type="button"
                            onClick={() => handleGuardarEdicion(i)}
                            disabled={
                              !editBuffer.rol.trim() ||
                              !editBuffer.nombre.trim() ||
                              !editBuffer.contacto.trim()
                            }
                            className="text-emerald-400 hover:text-emerald-300 disabled:opacity-40 disabled:cursor-not-allowed text-xs font-semibold transition-colors"
                            title="Confirmar cambios"
                          >
                            ✓ Guardar
                          </button>
                          <button
                            type="button"
                            onClick={handleCancelarEdicion}
                            className="text-slate-400 hover:text-slate-200 text-xs font-semibold transition-colors"
                            title="Cancelar edición"
                          >
                            ✕ Cancelar
                          </button>
                        </div>
                      </td>
                    </>
                  ) : (
                    // ── Modo lectura ───────────────────────────────────────────
                    <>
                      <td className="py-2.5 pr-4">
                        <span className="px-2 py-0.5 rounded-full text-xs font-medium bg-emerald-900/50 text-emerald-300 border border-emerald-800/50">
                          {ag.rol}
                        </span>
                      </td>
                      <td className="py-2.5 pr-4 text-slate-200 font-medium">{ag.nombre}</td>
                      <td className="py-2.5 pr-4 text-slate-400">{ag.contacto}</td>
                      <td className="py-2.5 text-right">
                        <div className="flex items-center justify-end gap-3">
                          <button
                            type="button"
                            onClick={() => handleIniciarEdicion(i)}
                            className="text-cyan-400 hover:text-cyan-300 text-xs font-semibold transition-colors"
                            title="Editar agencia"
                          >
                            ✎ Editar
                          </button>
                          <button
                            type="button"
                            onClick={() => handleEliminar(i)}
                            className="text-red-400 hover:text-red-300 text-xs font-semibold transition-colors"
                            title="Eliminar agencia"
                          >
                            ✕ Eliminar
                          </button>
                        </div>
                      </td>
                    </>
                  )}
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>

      {/* Formulario de Alta */}
      <form
        onSubmit={handleAgregar}
        className="bg-slate-800/40 border border-slate-700/50 rounded-xl p-4 flex flex-col gap-3"
      >
        <p className="text-xs font-semibold text-slate-400 uppercase tracking-widest">
          Agregar Agencia Interviniente
        </p>
        <div className="grid grid-cols-1 sm:grid-cols-3 gap-3">
          <input
            type="text"
            placeholder="Rol (Ej: Marítimo, Despachante)"
            value={nuevaAgencia.rol}
            onChange={(e) => setNuevaAgencia({ ...nuevaAgencia, rol: e.target.value })}
            className="bg-slate-900 border border-slate-700 rounded-lg px-3 py-2 text-sm text-slate-200 placeholder-slate-600 focus:outline-none focus:ring-1 focus:ring-cyan-500"
            required
          />
          <input
            type="text"
            placeholder="Nombre de la Agencia"
            value={nuevaAgencia.nombre}
            onChange={(e) => setNuevaAgencia({ ...nuevaAgencia, nombre: e.target.value })}
            className="bg-slate-900 border border-slate-700 rounded-lg px-3 py-2 text-sm text-slate-200 placeholder-slate-600 focus:outline-none focus:ring-1 focus:ring-cyan-500"
            required
          />
          <input
            type="text"
            placeholder="Contacto (Tlf / Email)"
            value={nuevaAgencia.contacto}
            onChange={(e) => setNuevaAgencia({ ...nuevaAgencia, contacto: e.target.value })}
            className="bg-slate-900 border border-slate-700 rounded-lg px-3 py-2 text-sm text-slate-200 placeholder-slate-600 focus:outline-none focus:ring-1 focus:ring-cyan-500"
            required
          />
        </div>
        <div className="flex justify-end">
          <button
            type="submit"
            className="bg-cyan-600 hover:bg-cyan-500 text-white text-xs font-semibold rounded-lg px-4 py-2 transition-colors"
          >
            ＋ Agregar a la lista
          </button>
        </div>
      </form>

      {/* Persistencia al Backend */}
      <div className="flex items-center gap-3 pt-4 border-t border-slate-700/50">
        <button
          type="button"
          disabled={mutation.isPending || !isDirty}
          onClick={() => mutation.mutate(draftAgencias)}
          className="bg-emerald-700 hover:bg-emerald-600 disabled:opacity-50 disabled:cursor-not-allowed text-white text-sm font-semibold rounded-lg px-5 py-2 transition-colors flex items-center gap-2"
        >
          {mutation.isPending ? (
            <>
              <SpinnerIcon /> Guardando…
            </>
          ) : (
            "Guardar Cambios de Agencias"
          )}
        </button>
        {mutation.isSuccess && (
          <span className="text-emerald-400 text-xs">✓ Agencias actualizadas correctamente.</span>
        )}
        {mutation.isError && (
          <span className="text-red-400 text-xs">
            Error: {(mutation.error as Error).message}
          </span>
        )}
      </div>
    </div>
  );
}
// TabPbip component removed. PbipForm is used instead.

// ─── ICONO SPINNER ────────────────────────────────────────────────────────────

function SpinnerIcon() {
  return (
    <svg
      className="animate-spin h-3.5 w-3.5 text-current"
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
        d="M4 12a8 8 0 018-8v4a4 4 0 00-4 4H4z"
      />
    </svg>
  );
}

// ─── SKELETON LOADER ──────────────────────────────────────────────────────────

function SkeletonLoader() {
  return (
    <div className="flex flex-col gap-3 animate-pulse">
      {[1, 2, 3].map((i) => (
        <div key={i} className="h-14 bg-slate-800 rounded-lg" />
      ))}
    </div>
  );
}

// ─── TAB ETAPAS (SOPORTE) ──────────────────────────────────────────────────────

function TabEtapas({ viajeId }: { viajeId: string }) {
  const { data: etapas = [], isLoading, isError, error } = useObtenerEtapas(viajeId);
  const mutation = useIntercalarEtapa(viajeId);

  const [showForm, setShowForm] = useState(false);
  const [fechaInicio, setFechaInicio] = useState("");
  const [fechaFin, setFechaFin] = useState("");
  const [remolcador, setRemolcador] = useState<{ nombre: string; matricula: string } | null>(null);
  const [barcazas, setBarcazas] = useState<string[]>([]);
  const [successMsg, setSuccessMsg] = useState<string | null>(null);
  const [formError, setFormError] = useState<string | null>(null);

  const handleSelectRemolcador = (emb: any) => {
    setRemolcador({ nombre: emb.nombre, matricula: emb.matricula });
  };

  const handleSelectBarcaza = (emb: any) => {
    if (!barcazas.includes(emb.nombre)) {
      setBarcazas([...barcazas, emb.nombre]);
    }
  };

  const handleRemoveBarcaza = (nombre: string) => {
    setBarcazas(barcazas.filter((b) => b !== nombre));
  };

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    setSuccessMsg(null);
    setFormError(null);

    if (!fechaInicio) {
      setFormError("La fecha de inicio es requerida.");
      return;
    }

    mutation.mutate(
      {
        fechaInicio: new Date(fechaInicio).toISOString(),
        fechaFin: fechaFin ? new Date(fechaFin).toISOString() : undefined,
        remolcadorNombre: remolcador?.nombre,
        remolcadorMatricula: remolcador?.matricula,
        barcazasNombres: barcazas,
      },
      {
        onSuccess: () => {
          setSuccessMsg("✓ Etapa intercalada y ordenada correctamente.");
          setFechaInicio("");
          setFechaFin("");
          setRemolcador(null);
          setBarcazas([]);
          setShowForm(false);
        },
        onError: (err) => {
          setFormError(err.message);
        },
      }
    );
  };

  if (isLoading) return <SkeletonLoader />;
  if (isError) {
    return (
      <div className="text-red-400 text-sm bg-red-950/20 border border-red-800/40 rounded-lg p-3">
        Error al cargar etapas: {error.message}
      </div>
    );
  }

  return (
    <div className="flex flex-col gap-6">
      {/* Listado de etapas actuales (Timeline) */}
      <div className="space-y-4">
        <h3 className="text-sm font-semibold text-slate-400 uppercase tracking-widest">
          Línea de Tiempo de Etapas
        </h3>
        {etapas.length === 0 ? (
          <p className="text-slate-500 text-sm italic text-center py-4">
            Sin etapas registradas para este viaje.
          </p>
        ) : (
          <div className="relative border-l border-slate-700 ml-4 space-y-6">
            {etapas.map((etapa) => (
              <div key={etapa.etapaId} className="relative pl-6">
                {/* Indicador en el timeline */}
                <div className="absolute -left-[9px] top-1.5 w-[18px] h-[18px] rounded-full border-2 border-slate-900 bg-cyan-500 flex items-center justify-center text-[10px] font-bold text-slate-950">
                  {etapa.etapaId}
                </div>
                <div className="bg-slate-800/40 border border-slate-800 rounded-lg p-3.5 space-y-2">
                  <div className="flex justify-between items-center flex-wrap gap-2">
                    <span className="text-sm font-bold text-slate-200">
                      Etapa #{etapa.etapaId}
                    </span>
                    <div className="text-xs text-slate-500 space-y-0.5">
                      <div>
                        <span className="font-semibold text-slate-400">Inicio:</span>{" "}
                        {etapa.fechaInicio ? new Date(etapa.fechaInicio).toLocaleString("es-AR") : "N/D"}
                      </div>
                      {etapa.fechaFin && (
                        <div>
                          <span className="font-semibold text-slate-400">Fin:</span>{" "}
                          {new Date(etapa.fechaFin).toLocaleString("es-AR")}
                        </div>
                      )}
                    </div>
                  </div>

                  <div className="border-t border-slate-700/50 pt-2 grid grid-cols-1 md:grid-cols-2 gap-4">
                    <div>
                      <span className="text-xs text-slate-500 block">Remolcador</span>
                      <span className="text-sm text-cyan-300 font-medium">
                        {etapa.remolcadorNombre
                          ? `${etapa.remolcadorNombre} (${etapa.remolcadorMatricula || "—"})`
                          : "Ninguno"}
                      </span>
                    </div>
                    <div>
                      <span className="text-xs text-slate-500 block">Barcazas ({etapa.barcazas.length})</span>
                      {etapa.barcazas.length > 0 ? (
                        <div className="flex flex-wrap gap-1 mt-1">
                          {etapa.barcazas.map((b, i) => (
                            <span
                              key={i}
                              className="text-[11px] bg-slate-700 text-slate-300 px-2 py-0.5 rounded border border-slate-600/50"
                            >
                              {b}
                            </span>
                          ))}
                        </div>
                      ) : (
                        <span className="text-xs text-slate-500 italic">Sin barcazas</span>
                      )}
                    </div>
                  </div>
                </div>
              </div>
            ))}
          </div>
        )}
      </div>

      {/* Botón para abrir el formulario */}
      {!showForm && (
        <button
          type="button"
          onClick={() => { setShowForm(true); setFormError(null); setSuccessMsg(null); }}
          className="bg-cyan-600/20 hover:bg-cyan-600/30 text-cyan-400 border border-cyan-500/40 rounded-lg px-4 py-2 text-sm font-semibold transition-colors flex items-center justify-center gap-2"
        >
          ＋ Intercalar Nueva Etapa
        </button>
      )}

      {/* Formulario colapsable para intercalar etapa */}
      {showForm && (
        <form onSubmit={handleSubmit} className="border-t border-slate-800 pt-5 space-y-4">
          <div className="flex justify-between items-center">
            <h4 className="text-xs font-semibold text-cyan-400 uppercase tracking-widest">
              Intercalar Nueva Etapa
            </h4>
            <button
              type="button"
              onClick={() => setShowForm(false)}
              className="text-xs text-slate-400 hover:text-white"
            >
              Cancelar
            </button>
          </div>

          <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
            <div>
              <label className="block text-xs font-semibold text-slate-400 mb-1">
                Fecha Inicio *
              </label>
              <input
                type="datetime-local"
                value={fechaInicio}
                onChange={(e) => setFechaInicio(e.target.value)}
                required
                className="w-full bg-slate-900 border border-slate-700 rounded-lg px-3 py-2 text-sm text-slate-200 focus:outline-none focus:ring-1 focus:ring-cyan-500"
              />
            </div>
            <div>
              <label className="block text-xs font-semibold text-slate-400 mb-1">
                Fecha Fin (Opcional)
              </label>
              <input
                type="datetime-local"
                value={fechaFin}
                onChange={(e) => setFechaFin(e.target.value)}
                className="w-full bg-slate-900 border border-slate-700 rounded-lg px-3 py-2 text-sm text-slate-200 focus:outline-none focus:ring-1 focus:ring-cyan-500"
              />
            </div>
          </div>

          {/* Autocomplete de Remolcador */}
          <div>
            <label className="block text-xs font-semibold text-slate-400 mb-1">
              Remolcador (Catálogo Maestro)
            </label>
            <EmbarcacionSelect
              onSelect={handleSelectRemolcador}
              allowedTipos={["remolcador"]}
            />
            {remolcador && (
              <div className="mt-2 text-xs text-slate-300 bg-slate-800 px-3 py-1.5 rounded border border-slate-700 flex justify-between items-center">
                <span>
                  Remolcador seleccionado:{" "}
                  <strong className="text-cyan-400">
                    {remolcador.nombre} ({remolcador.matricula})
                  </strong>
                </span>
                <button
                  type="button"
                  onClick={() => setRemolcador(null)}
                  className="text-red-400 hover:text-red-300 font-bold"
                >
                  Quitar
                </button>
              </div>
            )}
          </div>

          {/* Autocomplete de Barcazas */}
          <div className="space-y-2">
            <label className="block text-xs font-semibold text-slate-400 mb-1">
              Asociar Barcazas (Catálogo Maestro)
            </label>
            <EmbarcacionSelect
              onSelect={handleSelectBarcaza}
              allowedTipos={["barcaza"]}
            />
            
            {/* Lista de Barcazas Seleccionadas */}
            <div className="space-y-1">
              <span className="text-xs text-slate-500">Barcazas seleccionadas para esta etapa:</span>
              {barcazas.length === 0 ? (
                <p className="text-xs text-slate-500 italic">Ninguna barcaza seleccionada.</p>
              ) : (
                <div className="flex flex-wrap gap-1.5 mt-1">
                  {barcazas.map((b) => (
                    <span
                      key={b}
                      className="text-xs bg-slate-800 text-slate-200 border border-slate-700 rounded px-2 py-1 flex items-center gap-1.5"
                    >
                      {b}
                      <button
                        type="button"
                        onClick={() => handleRemoveBarcaza(b)}
                        className="text-red-400 hover:text-red-300 font-bold text-xs"
                      >
                        &times;
                      </button>
                    </span>
                  ))}
                </div>
              )}
            </div>
          </div>

          {formError && (
            <div className="text-red-400 text-xs mt-1 bg-red-950/20 border border-red-800/40 p-2.5 rounded-lg">
              {formError}
            </div>
          )}

          <div className="flex gap-2 justify-end pt-2">
            <button
              type="button"
              onClick={() => setShowForm(false)}
              className="bg-slate-800 hover:bg-slate-700 text-slate-300 px-4 py-2 rounded-lg text-sm font-semibold transition-colors"
            >
              Cancelar
            </button>
            <button
              type="submit"
              disabled={mutation.isPending}
              className="bg-cyan-600 hover:bg-cyan-500 disabled:opacity-50 disabled:cursor-not-allowed text-white px-4 py-2 rounded-lg text-sm font-semibold transition-colors flex items-center gap-2"
            >
              {mutation.isPending ? "Intercalando..." : "Intercalar Etapa"}
            </button>
          </div>
        </form>
      )}

      {successMsg && (
        <div className="text-emerald-400 text-sm bg-emerald-950/20 border border-emerald-800/40 p-3 rounded-lg">
          {successMsg}
        </div>
      )}
    </div>
  );
}

// ─── TABS CONFIG ──────────────────────────────────────────────────────────────

type TabId = "bitacora" | "agencias" | "pbip" | "etapas";

const TABS: { id: TabId; label: string; icon: string }[] = [
  { id: "bitacora", label: "Bitácora", icon: "📋" },
  { id: "agencias", label: "Agencias", icon: "🏢" },
  { id: "pbip", label: "Seguridad PBIP", icon: "🛡️" },
  { id: "etapas", label: "Etapas (Soporte)", icon: "⚓" },
];

// ─── COMPONENTE PRINCIPAL ─────────────────────────────────────────────────────

export default function ModalComplementosViaje({
  isOpen,
  onClose,
  viajeId,
}: ModalComplementosViajeProps) {
  const [activeTab, setActiveTab] = useState<TabId>("bitacora");

  const {
    data,
    isLoading,
    isError,
    error,
  } = useViajeComplementos(viajeId);

  const { data: etapasData } = useObtenerEtapas(viajeId);

  if (!isOpen) return null;

  return (
    // ── Overlay ──
    <div
      className="fixed inset-0 z-50 flex items-center justify-center bg-black/70 backdrop-blur-sm"
      onClick={(e) => {
        if (e.target === e.currentTarget) onClose();
      }}
    >
      {/* ── Panel del Modal ── */}
      <div className="relative w-full max-w-2xl mx-4 bg-slate-900 border border-slate-700/60 rounded-2xl shadow-2xl shadow-black/60 flex flex-col max-h-[90vh]">
        {/* ── Header ── */}
        <div className="flex items-start justify-between px-6 pt-5 pb-4 border-b border-slate-800">
          <div>
            <p className="text-xs font-semibold text-cyan-400 uppercase tracking-widest mb-0.5">
              Panel de Complementos
            </p>
            <h2 className="text-lg font-bold text-slate-100 leading-tight">
              Viaje{" "}
              <span className="font-mono text-cyan-300">{viajeId}</span>
            </h2>
          </div>
          <button
            onClick={onClose}
            className="text-slate-500 hover:text-slate-300 transition-colors ml-4 mt-0.5"
            aria-label="Cerrar modal"
          >
            <svg className="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M6 18L18 6M6 6l12 12" />
            </svg>
          </button>
        </div>

        {/* ── Tabs ── */}
        <div className="flex gap-1 px-6 pt-3 border-b border-slate-800">
          {TABS.map((tab) => (
            <button
              key={tab.id}
              onClick={() => setActiveTab(tab.id)}
              className={`flex items-center gap-1.5 px-3 py-2 text-sm font-medium rounded-t-lg transition-colors border-b-2 -mb-px
                ${
                  activeTab === tab.id
                    ? "text-cyan-300 border-cyan-400 bg-slate-800/60"
                    : "text-slate-500 border-transparent hover:text-slate-300 hover:border-slate-600"
                }`}
            >
              <span>{tab.icon}</span>
              <span>{tab.label}</span>
              {/* Badges de conteo */}
              {tab.id === "bitacora" && data && (
                <span className="ml-1 text-xs bg-slate-700 text-slate-300 rounded-full px-1.5 py-0.5 leading-none">
                  {data.notasBitacora.length}
                </span>
              )}
              {tab.id === "agencias" && data && (
                <span className="ml-1 text-xs bg-slate-700 text-slate-300 rounded-full px-1.5 py-0.5 leading-none">
                  {data.agencias.length}
                </span>
              )}
              {tab.id === "etapas" && etapasData && (
                <span className="ml-1 text-xs bg-slate-700 text-slate-300 rounded-full px-1.5 py-0.5 leading-none">
                  {etapasData.length}
                </span>
              )}
            </button>
          ))}
        </div>

        {/* ── Contenido ── */}
        <div className="flex-1 overflow-y-auto px-6 py-5">
          {isLoading && <SkeletonLoader />}

          {isError && (
            <div className="flex items-center gap-3 bg-red-950/40 border border-red-800/50 rounded-lg px-4 py-3">
              <span className="text-red-400 text-lg">⚠️</span>
              <div>
                <p className="text-red-300 font-semibold text-sm">
                  Error al cargar los complementos
                </p>
                <p className="text-red-400 text-xs mt-0.5">{error.message}</p>
              </div>
            </div>
          )}

          {data && (
            <>
              {activeTab === "bitacora" && (
                <TabBitacora viajeId={viajeId} notas={data.notasBitacora} />
              )}
              {activeTab === "agencias" && (
                <TabAgencias viajeId={viajeId} agencias={data.agencias} />
              )}
              {activeTab === "pbip" && (
                <PbipForm viajeId={viajeId} datosPbip={data.datosPbip} />
              )}
              {activeTab === "etapas" && (
                <TabEtapas viajeId={viajeId} />
              )}
            </>
          )}
        </div>

        {/* ── Footer ── */}
        <div className="px-6 py-3 border-t border-slate-800 flex items-center justify-between">
          <p className="text-xs text-slate-600">
            {data
              ? `Última carga: ${new Date().toLocaleTimeString("es-AR")}`
              : isLoading
              ? "Cargando datos…"
              : ""}
          </p>
          <button
            onClick={onClose}
            className="text-xs text-slate-500 hover:text-slate-300 transition-colors"
          >
            Cerrar panel
          </button>
        </div>
      </div>
    </div>
  );
}
