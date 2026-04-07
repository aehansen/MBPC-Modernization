// src/components/viajes/ModalNuevoViaje.tsx
import { useState, useEffect, useCallback } from 'react';
import { useCrearViaje } from '@/hooks/useViajes';

// ─── Tipos ────────────────────────────────────────────────────────────────────

type ProximoPuntoControl =
  | 'RPNA_AMARR_ELDO'
  | 'RPNA_PREFECT_ROS'
  | 'RPLATA_CANAL_MITRE';

type DeclaracionMalvinas =
  | 'NoVieneDeMalvinas_L'
  | 'VieneDeMalvinas_M'
  | 'NoAplica_NA'
  | 'EnTransito_T';

interface NuevoViajeDto {
  // Requeridos
  nombreBuque: string;
  origen: string;
  destino: string;
  proximoPuntoControl: ProximoPuntoControl;
  fechaPartida: string; // ISO
  eta: string;          // ISO
  declaracionMalvinas: DeclaracionMalvinas;
  // Opcionales
  muelleSalida: string | null;
  agenciaMaritima: string | null;
  motivoViaje: string | null;
  zoe: string | null;
  posicion: string | null;
  rioCanalKmPar: number | null;
}

interface ModalNuevoViajeProps {
  isOpen: boolean;
  onClose: () => void;
}

// ─── Constantes ───────────────────────────────────────────────────────────────

const PUNTOS_CONTROL: { value: ProximoPuntoControl; label: string }[] = [
  { value: 'RPNA_AMARR_ELDO', label: 'Amarras El Dorado (RPNA)' },
  { value: 'RPNA_PREFECT_ROS', label: 'Prefectura Rosario (RPNA)' },
  { value: 'RPLATA_CANAL_MITRE', label: 'Canal Mitre (R. de la Plata)' },
];

const DECLARACIONES_MALVINAS: { value: DeclaracionMalvinas; label: string }[] = [
  { value: 'NoVieneDeMalvinas_L', label: 'No viene de Malvinas (L)' },
  { value: 'VieneDeMalvinas_M',   label: 'Viene de Malvinas (M)' },
  { value: 'NoAplica_NA',         label: 'No aplica (NA)' },
  { value: 'EnTransito_T',        label: 'En tránsito (T)' },
];

const FORM_INICIAL: NuevoViajeDto = {
  nombreBuque:         '',
  origen:              '',
  destino:             '',
  proximoPuntoControl: 'RPNA_AMARR_ELDO',
  fechaPartida:        '',
  eta:                 '',
  declaracionMalvinas: 'NoVieneDeMalvinas_L',
  muelleSalida:        null,
  agenciaMaritima:     null,
  motivoViaje:         null,
  zoe:                 null,
  posicion:            null,
  rioCanalKmPar:       null,
};

// ─── Helpers ──────────────────────────────────────────────────────────────────

/** Convierte datetime-local string → ISO 8601, o null si está vacío */
const toIso = (value: string): string =>
  value ? new Date(value).toISOString() : '';

/** Convierte ISO 8601 → datetime-local string para el input */
const toLocalDatetime = (iso: string): string => {
  if (!iso) return '';
  const d = new Date(iso);
  const pad = (n: number) => String(n).padStart(2, '0');
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
};

// ─── Subcomponentes de UI ─────────────────────────────────────────────────────

interface FieldProps {
  label: string;
  required?: boolean;
  children: React.ReactNode;
  error?: string;
}

function Field({ label, required, children, error }: FieldProps) {
  return (
    <div className="flex flex-col gap-1">
      <label className="text-xs font-semibold uppercase tracking-widest text-slate-400">
        {label}
        {required && <span className="ml-1 text-cyan-400">*</span>}
      </label>
      {children}
      {error && <p className="text-xs text-red-400">{error}</p>}
    </div>
  );
}

const inputClass =
  'w-full rounded-md border border-slate-700 bg-slate-800/60 px-3 py-2 text-sm text-slate-100 placeholder-slate-500 outline-none transition focus:border-cyan-500 focus:ring-1 focus:ring-cyan-500 disabled:opacity-50';

// ─── Validación ───────────────────────────────────────────────────────────────

type FormErrors = Partial<Record<keyof NuevoViajeDto, string>>;

function validarFormulario(form: NuevoViajeDto): FormErrors {
  const errors: FormErrors = {};
  if (!form.nombreBuque.trim())    errors.nombreBuque    = 'El nombre del buque es requerido.';
  if (!form.origen.trim())         errors.origen         = 'El origen es requerido.';
  if (!form.destino.trim())        errors.destino        = 'El destino es requerido.';
  if (!form.fechaPartida)          errors.fechaPartida   = 'La fecha de partida es requerida.';
  if (!form.eta)                   errors.eta            = 'El ETA es requerido.';
  if (form.fechaPartida && form.eta && new Date(form.eta) <= new Date(form.fechaPartida)) {
    errors.eta = 'El ETA debe ser posterior a la fecha de partida.';
  }
  return errors;
}

// ─── Componente Principal ─────────────────────────────────────────────────────

export function ModalNuevoViaje({ isOpen, onClose }: ModalNuevoViajeProps) {
  const crearViaje = useCrearViaje();
  const [form, setForm]       = useState<NuevoViajeDto>(FORM_INICIAL);
  const [errors, setErrors]   = useState<FormErrors>({});
  const [touched, setTouched] = useState(false);

  // Resetear al abrir/cerrar
  useEffect(() => {
    if (!isOpen) {
      setForm(FORM_INICIAL);
      setErrors({});
      setTouched(false);
    }
  }, [isOpen]);

  // Cerrar con Escape
  useEffect(() => {
    if (!isOpen) return;
    const handleKey = (e: KeyboardEvent) => {
      if (e.key === 'Escape' && !crearViaje.isPending) onClose();
    };
    window.addEventListener('keydown', handleKey);
    return () => window.removeEventListener('keydown', handleKey);
  }, [isOpen, onClose, crearViaje.isPending]);

  const handleChange = useCallback(
    <K extends keyof NuevoViajeDto>(field: K, value: NuevoViajeDto[K]) => {
      setForm((prev) => {
        const next = { ...prev, [field]: value };
        if (touched) setErrors(validarFormulario(next));
        return next;
      });
    },
    [touched],
  );

  const handleOptionalString = (field: keyof NuevoViajeDto, raw: string) =>
    handleChange(field, raw.trim() === '' ? null : (raw as NuevoViajeDto[typeof field]));

  const handleOptionalNumber = (field: keyof NuevoViajeDto, raw: string) =>
    handleChange(field, raw === '' ? null : (Number(raw) as NuevoViajeDto[typeof field]));

  const handleSubmit = () => {
    setTouched(true);
    const newErrors = validarFormulario(form);
    setErrors(newErrors);
    if (Object.keys(newErrors).length > 0) return;

    const payload: NuevoViajeDto = {
      ...form,
      fechaPartida: toIso(form.fechaPartida),
      eta:          toIso(form.eta),
    };

    crearViaje.mutate(payload, {
      onSuccess: () => {
        onClose();
      },
    });
  };

  if (!isOpen) return null;

  return (
    /* Overlay */
    <div
      className="fixed inset-0 z-50 flex items-center justify-center bg-black/70 backdrop-blur-sm"
      onClick={(e) => { if (e.target === e.currentTarget && !crearViaje.isPending) onClose(); }}
    >
      {/* Panel */}
      <div className="relative flex max-h-[90vh] w-full max-w-2xl flex-col rounded-xl border border-slate-700 bg-slate-900 shadow-2xl shadow-black/60">

        {/* Header */}
        <div className="flex items-center justify-between border-b border-slate-700/60 px-6 py-4">
          <div className="flex items-center gap-3">
            <span className="flex h-8 w-8 items-center justify-center rounded-lg bg-cyan-500/10 text-cyan-400">
              {/* Ícono de barco */}
              <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth={1.8} className="h-5 w-5">
                <path strokeLinecap="round" strokeLinejoin="round" d="M3 18l1.5-9h15L21 18M3 18H2m1 0h18m0 0h1M12 3v6m-4 0h8" />
                <path strokeLinecap="round" strokeLinejoin="round" d="M5 21c1.333-1 2.667-1 4 0s2.667 1 4 0 2.667-1 4 0" />
              </svg>
            </span>
            <div>
              <h2 className="text-base font-semibold text-slate-100">Iniciar Nuevo Viaje</h2>
              <p className="text-xs text-slate-500">Complete los datos para registrar el viaje</p>
            </div>
          </div>
          <button
            onClick={onClose}
            disabled={crearViaje.isPending}
            className="rounded-md p-1.5 text-slate-500 transition hover:bg-slate-800 hover:text-slate-300 disabled:opacity-40"
          >
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth={2} className="h-4 w-4">
              <path strokeLinecap="round" strokeLinejoin="round" d="M6 18L18 6M6 6l12 12" />
            </svg>
          </button>
        </div>

        {/* Body (scrollable) */}
        <div className="flex-1 overflow-y-auto px-6 py-5">

          {/* Error global de mutación */}
          {crearViaje.isError && (
            <div className="mb-5 flex items-start gap-2 rounded-lg border border-red-500/30 bg-red-500/10 p-3 text-sm text-red-300">
              <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth={2} className="mt-0.5 h-4 w-4 shrink-0">
                <circle cx="12" cy="12" r="10" /><path strokeLinecap="round" d="M12 8v4m0 4h.01" />
              </svg>
              <span>
                {crearViaje.error instanceof Error
                  ? crearViaje.error.message
                  : 'Ocurrió un error al crear el viaje. Intente nuevamente.'}
              </span>
            </div>
          )}

          <div className="space-y-5">

            {/* ── Sección: Identificación ── */}
            <section>
              <SectionTitle>Identificación del Buque</SectionTitle>
              <div className="mt-3 grid grid-cols-1 gap-4 sm:grid-cols-2">
                <Field label="Nombre del Buque" required error={errors.nombreBuque}>
                  <input
                    type="text"
                    className={inputClass}
                    placeholder="Ej: MV Patagonia"
                    value={form.nombreBuque}
                    onChange={(e) => handleChange('nombreBuque', e.target.value)}
                  />
                </Field>
                <Field label="Agencia Marítima">
                  <input
                    type="text"
                    className={inputClass}
                    placeholder="Ej: Maruba S.A."
                    value={form.agenciaMaritima ?? ''}
                    onChange={(e) => handleOptionalString('agenciaMaritima', e.target.value)}
                  />
                </Field>
              </div>
            </section>

            {/* ── Sección: Ruta ── */}
            <section>
              <SectionTitle>Ruta del Viaje</SectionTitle>
              <div className="mt-3 grid grid-cols-1 gap-4 sm:grid-cols-2">
                <Field label="Origen" required error={errors.origen}>
                  <input
                    type="text"
                    className={inputClass}
                    placeholder="Ej: Puerto de Buenos Aires"
                    value={form.origen}
                    onChange={(e) => handleChange('origen', e.target.value)}
                  />
                </Field>
                <Field label="Destino" required error={errors.destino}>
                  <input
                    type="text"
                    className={inputClass}
                    placeholder="Ej: Puerto de Rosario"
                    value={form.destino}
                    onChange={(e) => handleChange('destino', e.target.value)}
                  />
                </Field>
                <Field label="Muelle de Salida">
                  <input
                    type="text"
                    className={inputClass}
                    placeholder="Ej: Muelle 5"
                    value={form.muelleSalida ?? ''}
                    onChange={(e) => handleOptionalString('muelleSalida', e.target.value)}
                  />
                </Field>
                <Field label="Próximo Punto de Control" required>
                  <select
                    className={inputClass}
                    value={form.proximoPuntoControl}
                    onChange={(e) => handleChange('proximoPuntoControl', e.target.value as ProximoPuntoControl)}
                  >
                    {PUNTOS_CONTROL.map((p) => (
                      <option key={p.value} value={p.value}>{p.label}</option>
                    ))}
                  </select>
                </Field>
                <Field label="Río/Canal Km Par">
                  <input
                    type="number"
                    className={inputClass}
                    placeholder="Ej: 342"
                    min={0}
                    step={0.1}
                    value={form.rioCanalKmPar ?? ''}
                    onChange={(e) => handleOptionalNumber('rioCanalKmPar', e.target.value)}
                  />
                </Field>
                <Field label="Posición">
                  <input
                    type="text"
                    className={inputClass}
                    placeholder="Ej: 34°35'S 58°22'W"
                    value={form.posicion ?? ''}
                    onChange={(e) => handleOptionalString('posicion', e.target.value)}
                  />
                </Field>
              </div>
            </section>

            {/* ── Sección: Fechas ── */}
            <section>
              <SectionTitle>Fechas</SectionTitle>
              <div className="mt-3 grid grid-cols-1 gap-4 sm:grid-cols-2">
                <Field label="Fecha de Partida" required error={errors.fechaPartida}>
                  <input
                    type="datetime-local"
                    className={inputClass}
                    value={toLocalDatetime(form.fechaPartida)}
                    onChange={(e) => handleChange('fechaPartida', e.target.value)}
                  />
                </Field>
                <Field label="ETA (Llegada Estimada)" required error={errors.eta}>
                  <input
                    type="datetime-local"
                    className={inputClass}
                    value={toLocalDatetime(form.eta)}
                    onChange={(e) => handleChange('eta', e.target.value)}
                  />
                </Field>
              </div>
            </section>

            {/* ── Sección: Información Adicional ── */}
            <section>
              <SectionTitle>Información Adicional</SectionTitle>
              <div className="mt-3 grid grid-cols-1 gap-4 sm:grid-cols-2">
                <Field label="Declaración Malvinas" required>
                  <select
                    className={inputClass}
                    value={form.declaracionMalvinas}
                    onChange={(e) => handleChange('declaracionMalvinas', e.target.value as DeclaracionMalvinas)}
                  >
                    {DECLARACIONES_MALVINAS.map((d) => (
                      <option key={d.value} value={d.value}>{d.label}</option>
                    ))}
                  </select>
                </Field>
                <Field label="ZOE">
                  <input
                    type="text"
                    className={inputClass}
                    placeholder="Código ZOE"
                    value={form.zoe ?? ''}
                    onChange={(e) => handleOptionalString('zoe', e.target.value)}
                  />
                </Field>
                <Field label="Motivo del Viaje" error={undefined}>
                  <input
                    type="text"
                    className={`${inputClass} sm:col-span-2`}
                    placeholder="Descripción breve del motivo"
                    value={form.motivoViaje ?? ''}
                    onChange={(e) => handleOptionalString('motivoViaje', e.target.value)}
                  />
                </Field>
              </div>
            </section>
          </div>
        </div>

        {/* Footer */}
        <div className="flex items-center justify-between border-t border-slate-700/60 px-6 py-4">
          <p className="text-xs text-slate-500">
            <span className="text-cyan-400">*</span> Campos requeridos
          </p>
          <div className="flex gap-3">
            <button
              type="button"
              onClick={onClose}
              disabled={crearViaje.isPending}
              className="rounded-lg border border-slate-700 bg-slate-800 px-4 py-2 text-sm font-medium text-slate-300 transition hover:border-slate-600 hover:text-slate-100 disabled:opacity-40"
            >
              Cancelar
            </button>
            <button
              type="button"
              onClick={handleSubmit}
              disabled={crearViaje.isPending}
              className="flex items-center gap-2 rounded-lg bg-cyan-600 px-5 py-2 text-sm font-semibold text-white transition hover:bg-cyan-500 disabled:cursor-not-allowed disabled:opacity-60"
            >
              {crearViaje.isPending ? (
                <>
                  <svg className="h-4 w-4 animate-spin" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth={2}>
                    <path strokeLinecap="round" strokeLinejoin="round" d="M12 2v4m0 12v4M4.93 4.93l2.83 2.83m8.48 8.48 2.83 2.83M2 12h4m12 0h4M4.93 19.07l2.83-2.83m8.48-8.48 2.83-2.83" />
                  </svg>
                  Guardando…
                </>
              ) : (
                <>
                  <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth={2} className="h-4 w-4">
                    <path strokeLinecap="round" strokeLinejoin="round" d="M5 12l5 5L20 7" />
                  </svg>
                  Iniciar Viaje
                </>
              )}
            </button>
          </div>
        </div>
      </div>
    </div>
  );
}

// ─── Helper interno ───────────────────────────────────────────────────────────

function SectionTitle({ children }: { children: React.ReactNode }) {
  return (
    <h3 className="flex items-center gap-2 text-xs font-semibold uppercase tracking-widest text-cyan-500/80">
      <span className="h-px flex-1 bg-slate-700/60" />
      {children}
      <span className="h-px flex-1 bg-slate-700/60" />
    </h3>
  );
}
