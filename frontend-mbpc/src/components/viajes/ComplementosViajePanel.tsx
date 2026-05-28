import { useEffect, useMemo, useState } from 'react';
import { useForm, type SubmitHandler } from 'react-hook-form';

import {
  useActualizarDatosPbip,
  useAgregarNotaBitacora,
  useViajeComplementos,
} from '../../hooks/viajes/useViajeComplementos';
import type { ActualizarDatosPbipDto } from '../../types/complementos.types';

interface ComplementosViajePanelProps {
  viajeId: string;
}

const EMPTY_PBIP_FORM: ActualizarDatosPbipDto = {
  contactoOcpm: '',
  nroInmarsat: '',
  arqueoBruto: 0,
  nivelProteccion: 1,
};

function formatFecha(fechaIso: string): string {
  const date = new Date(fechaIso);
  if (Number.isNaN(date.getTime())) return fechaIso;
  return date.toLocaleString();
}

export default function ComplementosViajePanel({ viajeId }: ComplementosViajePanelProps) {
  const [notaNueva, setNotaNueva] = useState('');
  const {
    data: complementos,
    isLoading,
    isError,
    error,
  } = useViajeComplementos(viajeId);

  const agregarNotaMutation = useAgregarNotaBitacora(viajeId);
  const actualizarPbipMutation = useActualizarDatosPbip(viajeId);

  const secretarias = useMemo(() => complementos?.notasBitacora ?? [], [complementos?.notasBitacora]);
  const agencias = useMemo(() => complementos?.agencias ?? [], [complementos?.agencias]);

  const {
    register,
    handleSubmit,
    reset,
    formState: { errors, isDirty },
  } = useForm<ActualizarDatosPbipDto>({
    defaultValues: EMPTY_PBIP_FORM,
  });

  useEffect(() => {
    if (!complementos?.datosPbip) return;
    const { contactoOcpm, nroInmarsat, arqueoBruto, nivelProteccion } = complementos.datosPbip;
    reset({
      contactoOcpm: contactoOcpm || '',
      nroInmarsat: nroInmarsat || '',
      arqueoBruto: arqueoBruto || 0,
      nivelProteccion: nivelProteccion || 1,
    });
  }, [complementos, reset]);

  const handleAgregarNota = async () => {
    const texto = notaNueva.trim();
    if (!texto) return;
    await agregarNotaMutation.mutateAsync({ texto });
    setNotaNueva('');
  };

  const onSubmitPbip: SubmitHandler<ActualizarDatosPbipDto> = async (values) => {
    values.nivelProteccion = Number(values.nivelProteccion);
    values.arqueoBruto = Number(values.arqueoBruto);
    await actualizarPbipMutation.mutateAsync(values);
  };

  if (isLoading) {
    return (
      <section className="rounded-xl border border-slate-800 bg-slate-900 p-6 text-slate-200 shadow-lg">
        <p className="animate-pulse text-sm text-slate-400">Cargando panel de complementos de seguridad...</p>
      </section>
    );
  }

  if (isError) {
    return (
      <section className="rounded-xl border border-red-900/60 bg-red-950/40 p-6 shadow-lg">
        <h3 className="text-base font-semibold text-red-200">Error al cargar complementos</h3>
        <p className="mt-2 text-sm text-red-300">{error instanceof Error ? error.message : 'Error desconocido'}</p>
      </section>
    );
  }

  return (
    <section className="rounded-xl border border-slate-800 bg-slate-900 p-5 shadow-xl">
      <header className="mb-5 flex items-center justify-between border-b border-slate-800 pb-4">
        <div>
          <h2 className="text-lg font-semibold text-slate-100">Trazabilidad, Auditoría y PBIP</h2>
          <p className="mt-1 text-xs uppercase tracking-wider text-slate-400">Modernización MBPC — Datos de Control</p>
        </div>
        <span className="rounded-md border border-slate-700 bg-slate-800 px-3 py-1 text-xs font-medium text-slate-300">
          Viaje: {complementos?.vesselName || viajeId}
        </span>
      </header>

      <div className="grid grid-cols-1 gap-5 xl:grid-cols-3">
        <article className="rounded-lg border border-slate-800 bg-slate-950/60 p-4 xl:col-span-1">
          <h3 className="text-sm font-semibold uppercase tracking-wider text-cyan-300">Notas de Bitácora</h3>

          <div className="mt-3 h-72 overflow-y-auto rounded-md border border-slate-800 bg-slate-950 p-3">
            {secretarias.length === 0 ? (
              <p className="text-sm italic text-slate-500">Sin novedades registradas.</p>
            ) : (
              <ul className="space-y-3">
                {secretarias.map((nota) => (
                  <li key={nota.id} className="rounded-md border border-slate-800 bg-slate-900 p-3">
                    <p className="text-sm text-slate-200">{nota.texto}</p>
                    <div className="mt-2 flex items-center justify-between text-xs text-slate-400">
                      <span className="font-medium text-slate-300">👤 {nota.usuario}</span>
                      <span>📅 {formatFecha(nota.fecha)}</span>
                    </div>
                  </li>
                ))}
              </ul>
            )}
          </div>

          <div className="mt-4 flex gap-2">
            <input
              type="text"
              value={notaNueva}
              onChange={(event) => setNotaNueva(event.target.value)}
              placeholder="Registrar novedad en bitácora..."
              className="flex-1 rounded-md border border-slate-700 bg-slate-900 px-3 py-2 text-sm text-slate-100 outline-none transition focus:border-cyan-500"
              disabled={agregarNotaMutation.isPending}
            />
            <button
              type="button"
              onClick={handleAgregarNota}
              disabled={agregarNotaMutation.isPending || notaNueva.trim().length === 0}
              className="rounded-md bg-cyan-600 px-4 py-2 text-sm font-semibold text-white transition hover:bg-cyan-500 disabled:opacity-50"
            >
              {agregarNotaMutation.isPending ? 'Guardando...' : 'Asentar'}
            </button>
          </div>
        </article>

        <article className="rounded-lg border border-slate-800 bg-slate-950/60 p-4 xl:col-span-1">
          <h3 className="text-sm font-semibold uppercase tracking-wider text-emerald-300">Seguridad PBIP</h3>

          <form className="mt-4 space-y-4" onSubmit={handleSubmit(onSubmitPbip)}>
            <div>
              <label htmlFor="nivelProteccion" className="mb-1 block text-xs font-semibold text-slate-400">
                Nivel de Protección
              </label>
              <select
                id="nivelProteccion"
                {...register('nivelProteccion', { required: true })}
                className="w-full rounded-md border border-slate-700 bg-slate-900 px-3 py-2 text-sm text-slate-100 outline-none focus:border-emerald-500"
              >
                <option value={1}>Nivel de Protección 1 (Normal)</option>
                <option value={2}>Nivel de Protección 2 (Reforzado)</option>
                <option value={3}>Nivel de Protección 3 (Excepcional)</option>
              </select>
            </div>

            <div>
              <label htmlFor="contactoOcpm" className="mb-1 block text-xs font-semibold text-slate-400">
                Contacto Oficial OCPM
              </label>
              <input
                id="contactoOcpm"
                type="text"
                {...register('contactoOcpm', { required: 'El contacto OCPM es requerido' })}
                className="w-full rounded-md border border-slate-700 bg-slate-900 px-3 py-2 text-sm text-slate-100 outline-none focus:border-emerald-500"
              />
              {errors.contactoOcpm && (
                <span className="mt-1 block text-xs text-red-400">{errors.contactoOcpm.message}</span>
              )}
            </div>

            <div>
              <label htmlFor="nroInmarsat" className="mb-1 block text-xs font-semibold text-slate-400">
                Nro de Terminal INMARSAT
              </label>
              <input
                id="nroInmarsat"
                type="text"
                {...register('nroInmarsat')}
                className="w-full rounded-md border border-slate-700 bg-slate-900 px-3 py-2 text-sm text-slate-100 outline-none focus:border-emerald-500"
              />
            </div>

            <div>
              <label htmlFor="arqueoBruto" className="mb-1 block text-xs font-semibold text-slate-400">
                Arqueo Bruto (TRG)
              </label>
              <input
                id="arqueoBruto"
                type="number"
                step="any"
                {...register('arqueoBruto')}
                className="w-full rounded-md border border-slate-700 bg-slate-900 px-3 py-2 text-sm text-slate-100 outline-none focus:border-emerald-500"
              />
            </div>

            <button
              type="submit"
              disabled={actualizarPbipMutation.isPending || !isDirty}
              className="w-full rounded-md bg-emerald-600 px-4 py-2 text-sm font-semibold text-white transition hover:bg-emerald-500 disabled:opacity-50"
            >
              {actualizarPbipMutation.isPending ? 'Sincronizando...' : 'Actualizar Datos PBIP'}
            </button>
          </form>
        </article>

        <article className="rounded-lg border border-slate-800 bg-slate-950/60 p-4 xl:col-span-1">
          <h3 className="text-sm font-semibold uppercase tracking-wider text-indigo-300">Agencias Designadas</h3>

          <div className="mt-4 overflow-hidden rounded-md border border-slate-800">
            <div className="max-h-80 overflow-auto">
              <table className="w-full text-left text-sm">
                <thead className="sticky top-0 bg-slate-900 text-xs uppercase text-slate-400">
                  <tr>
                    <th className="px-3 py-2">Rol / Agencia</th>
                    <th className="px-3 py-2">Información de Contacto</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-slate-800">
                  {agencias.length === 0 ? (
                    <tr>
                      <td colSpan={2} className="px-3 py-6 text-center text-sm italic text-slate-500">
                        No hay agencias marítimas registradas para este viaje.
                      </td>
                    </tr>
                  ) : (
                    agencias.map((agencia, index) => (
                      <tr key={index} className="bg-slate-950/50 text-slate-200">
                        <td className="px-3 py-3">
                          <span className="inline-block rounded bg-indigo-950 px-2 py-0.5 text-xs font-medium text-indigo-300">
                            {agencia.rol}
                          </span>
                          <div className="font-medium text-slate-100">{agencia.nombre}</div>
                        </td>
                        <td className="whitespace-pre-line px-3 py-3 text-xs text-slate-300">
                          {agencia.contacto || 'Sin datos de contacto'}
                        </td>
                      </tr>
                    ))
                  )}
                </tbody>
              </table>
            </div>
          </div>
        </article>
      </div>
    </section>
  );
}
