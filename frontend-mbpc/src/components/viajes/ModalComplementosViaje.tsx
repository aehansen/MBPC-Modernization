import { useState } from "react";
import { useForm } from "react-hook-form";
import {
  useViajeComplementos,
  useAgregarNotaBitacora,
  useActualizarDatosPbip,
} from "../../hooks/viajes/useViajeComplementos";

// ─── TIPOS (espejo de los DTOs del backend) ──────────────────────────────────

interface NotaBitacoraDto {
  id: string;
  texto: string;
  usuario: string;
  fechaHora: string;
  categoria: string;
}

interface AgregarNotaBitacoraDto {
  texto: string;
  categoria: string;
}

interface AgenciaDto {
  rol: string;
  nombre: string;
  contacto: string;
}

interface DatosPbipDto {
  contactoOcpm: string;
  nroInmarsat: string;
  arqueoBruto: number;
  nivelProteccion: number;
}

interface ActualizarDatosPbipDto {
  contactoOcpm: string;
  nroInmarsat: string;
  arqueoBruto: number;
  nivelProteccion: number;
}

interface ViajeComplementosDto {
  viajeId: string;
  notasBitacora: NotaBitacoraDto[];
  agencias: AgenciaDto[];
  datosPbip: DatosPbipDto | null;
}

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
  notas: NotaBitacoraDto[];
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

function TabAgencias({ agencias }: { agencias: AgenciaDto[] }) {
  return (
    <div className="overflow-x-auto">
      {agencias.length === 0 ? (
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
              <th className="text-left text-xs font-semibold text-slate-500 uppercase tracking-wider pb-2">
                Contacto
              </th>
            </tr>
          </thead>
          <tbody className="divide-y divide-slate-800">
            {agencias.map((ag, i) => (
              <tr key={i} className="hover:bg-slate-800/40 transition-colors">
                <td className="py-2.5 pr-4">
                  <span className="px-2 py-0.5 rounded-full text-xs font-medium bg-emerald-900/50 text-emerald-300 border border-emerald-800/50">
                    {ag.rol}
                  </span>
                </td>
                <td className="py-2.5 pr-4 text-slate-200 font-medium">{ag.nombre}</td>
                <td className="py-2.5 text-slate-400">{ag.contacto}</td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </div>
  );
}

const NIVELES_PROTECCION = [
  { value: 1, label: "Nivel 1 — Normal" },
  { value: 2, label: "Nivel 2 — Heightened" },
  { value: 3, label: "Nivel 3 — Excepción" },
];

function TabPbip({
  viajeId,
  datosPbip,
}: {
  viajeId: string;
  datosPbip: DatosPbipDto | null;
}) {
  const {
    register,
    handleSubmit,
    formState: { errors, isDirty },
  } = useForm<ActualizarDatosPbipDto>({
    defaultValues: {
      contactoOcpm: datosPbip?.contactoOcpm ?? "",
      nroInmarsat: datosPbip?.nroInmarsat ?? "",
      arqueoBruto: datosPbip?.arqueoBruto ?? 0,
      nivelProteccion: datosPbip?.nivelProteccion ?? 1,
    },
  });

  const mutation = useActualizarDatosPbip(viajeId);


  return (
    <form onSubmit={handleSubmit((data) => mutation.mutate(data))} className="flex flex-col gap-4">
      <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
        <div className="flex flex-col gap-1">
          <label className="text-xs font-semibold text-slate-400 uppercase tracking-wider">
            Contacto OCPM
          </label>
          <input
            {...register("contactoOcpm", { required: "Campo obligatorio." })}
            className="bg-slate-900 border border-slate-700 rounded-lg px-3 py-2 text-sm text-slate-200 placeholder-slate-600 focus:outline-none focus:ring-1 focus:ring-cyan-500"
            placeholder="Ej: Cte. García / +54 11 0000-0000"
          />
          {errors.contactoOcpm && (
            <p className="text-red-400 text-xs">{errors.contactoOcpm.message}</p>
          )}
        </div>

        <div className="flex flex-col gap-1">
          <label className="text-xs font-semibold text-slate-400 uppercase tracking-wider">
            Nro. Inmarsat
          </label>
          <input
            {...register("nroInmarsat", { required: "Campo obligatorio." })}
            className="bg-slate-900 border border-slate-700 rounded-lg px-3 py-2 text-sm text-slate-200 placeholder-slate-600 focus:outline-none focus:ring-1 focus:ring-cyan-500"
            placeholder="Ej: 764XXXXXXX"
          />
          {errors.nroInmarsat && (
            <p className="text-red-400 text-xs">{errors.nroInmarsat.message}</p>
          )}
        </div>

        <div className="flex flex-col gap-1">
          <label className="text-xs font-semibold text-slate-400 uppercase tracking-wider">
            Arqueo Bruto (GT)
          </label>
          <input
            type="number"
            step="0.01"
            {...register("arqueoBruto", {
              required: "Campo obligatorio.",
              valueAsNumber: true,
              min: { value: 0, message: "Debe ser ≥ 0." },
            })}
            className="bg-slate-900 border border-slate-700 rounded-lg px-3 py-2 text-sm text-slate-200 placeholder-slate-600 focus:outline-none focus:ring-1 focus:ring-cyan-500"
          />
          {errors.arqueoBruto && (
            <p className="text-red-400 text-xs">{errors.arqueoBruto.message}</p>
          )}
        </div>

        <div className="flex flex-col gap-1">
          <label className="text-xs font-semibold text-slate-400 uppercase tracking-wider">
            Nivel de Protección
          </label>
          <select
            {...register("nivelProteccion", { valueAsNumber: true, required: true })}
            className="bg-slate-900 border border-slate-700 rounded-lg px-3 py-2 text-sm text-slate-300 focus:outline-none focus:ring-1 focus:ring-cyan-500"
          >
            {NIVELES_PROTECCION.map((n) => (
              <option key={n.value} value={n.value}>
                {n.label}
              </option>
            ))}
          </select>
        </div>
      </div>

      <div className="flex items-center gap-3 pt-2 border-t border-slate-700/50">
        <button
          type="submit"
          disabled={mutation.isPending || !isDirty}
          className="bg-emerald-700 hover:bg-emerald-600 disabled:opacity-50 disabled:cursor-not-allowed text-white text-sm font-semibold rounded-lg px-5 py-2 transition-colors flex items-center gap-2"
        >
          {mutation.isPending ? (
            <>
              <SpinnerIcon /> Guardando…
            </>
          ) : (
            "Guardar datos PBIP"
          )}
        </button>
        {mutation.isSuccess && (
          <span className="text-emerald-400 text-xs">✓ Datos PBIP actualizados.</span>
        )}
        {mutation.isError && (
          <span className="text-red-400 text-xs">
            Error: {(mutation.error as Error).message}
          </span>
        )}
      </div>
    </form>
  );
}

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

// ─── TABS CONFIG ──────────────────────────────────────────────────────────────

type TabId = "bitacora" | "agencias" | "pbip";

const TABS: { id: TabId; label: string; icon: string }[] = [
  { id: "bitacora", label: "Bitácora", icon: "📋" },
  { id: "agencias", label: "Agencias", icon: "🏢" },
  { id: "pbip", label: "Seguridad PBIP", icon: "🛡️" },
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
  } = useViajeComplementos(viajeId) as any;


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
                <TabAgencias agencias={data.agencias} />
              )}
              {activeTab === "pbip" && (
                <TabPbip viajeId={viajeId} datosPbip={data.datosPbip} />
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
