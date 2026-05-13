// src/components/viajes/ModalPersonalExterno.tsx
import { useState } from "react";
import { useForm, type SubmitHandler } from "react-hook-form";
import { 
  useObtenerPersonal, 
  useEmbarcarPersonal, 
  useDesembarcarPersonal 
} from "@/hooks/usePersonalExterno";
import type { EmbarcarPersonalDto, PersonalItemDto } from "@/types/viajes.types";

interface ModalPersonalExternoProps {
  isOpen: boolean;
  onClose: () => void;
  viajeId: string;
}

export function ModalPersonalExterno({ isOpen, onClose, viajeId }: ModalPersonalExternoProps) {
  const { data: personalData, isLoading: isLoadingPersonal } = useObtenerPersonal(viajeId);
  const { mutate: embarcarMutate, isPending: isEmbarcando } = useEmbarcarPersonal();
  const { mutate: desembarcarMutate, isPending: isDesembarcando } = useDesembarcarPersonal();

  const [globalError, setGlobalError] = useState<string | null>(null);

  const { register, handleSubmit, reset, formState: { errors } } = useForm<EmbarcarPersonalDto>({
    defaultValues: {
      dni: "",
      nombreApellido: "",
      tipoPersonal: "Inspector",
    },
  });

  const onSubmit: SubmitHandler<EmbarcarPersonalDto> = (data) => {
    setGlobalError(null);
    embarcarMutate(
      { viajeId, payload: data },
      {
        onSuccess: () => {
          reset();
        },
        onError: (err) => {
          setGlobalError(err.message);
        },
      }
    );
  };

  const handleDesembarcar = (dni: string, tipoPersonal: "Inspector" | "Practico") => {
    if (!window.confirm(`¿Seguro que desea registrar el desembarque del ${tipoPersonal} con DNI ${dni}?`)) return;
    
    setGlobalError(null);
    desembarcarMutate({
      viajeId,
      dni,
      payload: { tipoPersonal }
    }, {
      onError: (err) => {
        setGlobalError(err.message);
      }
    });
  };

  const renderPersonalRow = (item: PersonalItemDto, tipo: "Inspector" | "Practico") => (
    <tr key={`${item.documento}-${item.fechaEmbarque}`} className="border-b border-slate-700/50 hover:bg-slate-800/30">
      <td className="py-2 px-3 text-sm text-slate-300">{item.documento}</td>
      <td className="py-2 px-3 text-sm text-slate-300">{item.nombreApellido}</td>
      <td className="py-2 px-3 text-sm text-slate-300">{new Date(item.fechaEmbarque).toLocaleString()}</td>
      <td className="py-2 px-3 text-sm">
        {item.estaABordo ? (
          <button 
            onClick={() => handleDesembarcar(item.documento, tipo)}
            disabled={isDesembarcando}
            className="px-3 py-1 bg-red-600/80 hover:bg-red-500 text-white rounded text-xs transition-colors"
          >
            {isDesembarcando ? "..." : "Desembarcar"}
          </button>
        ) : (
          <span className="text-slate-500">{item.fechaDesembarque ? new Date(item.fechaDesembarque).toLocaleString() : ""}</span>
        )}
      </td>
    </tr>
  );

  if (!isOpen) return null;

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-900/80 backdrop-blur-sm overflow-y-auto">
      <div className="bg-slate-800 rounded-lg shadow-xl w-full max-w-4xl border border-slate-700 overflow-hidden flex flex-col max-h-[90vh]">
        
        <div className="flex justify-between items-center px-6 py-4 border-b border-slate-700 bg-slate-800/50">
          <h2 className="text-xl font-bold text-slate-100 flex items-center gap-2">
            <span>Gestión de Personal Externo</span>
          </h2>
          <button onClick={onClose} className="text-slate-400 hover:text-white transition-colors text-2xl font-light">&times;</button>
        </div>

        {/* Formulario fijo bajo el título; las tablas quedan en la zona con scroll */}
        <section className="shrink-0 px-6 py-4 border-b border-slate-700 bg-slate-900/40">
          <h3 className="text-sm font-semibold text-cyan-400 uppercase tracking-widest mb-3">
            Embarcar nuevo personal
          </h3>
          <form
            onSubmit={handleSubmit(onSubmit)}
            className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-12 gap-4 items-end"
          >
            <div className="sm:col-span-1 lg:col-span-3">
              <label htmlFor="embarcar-dni" className="block text-xs font-semibold text-slate-400 mb-1">
                DNI
              </label>
              <input
                id="embarcar-dni"
                type="text"
                autoComplete="off"
                {...register("dni", { required: "El DNI es requerido" })}
                className="w-full bg-slate-800 border border-slate-600 rounded px-3 py-2 text-sm text-slate-200 focus:border-cyan-500 focus:ring-1 focus:ring-cyan-500 focus:outline-none"
              />
              {errors.dni && (
                <span className="text-red-400 text-xs mt-1 block">{errors.dni.message}</span>
              )}
            </div>

            <div className="sm:col-span-1 lg:col-span-4">
              <label htmlFor="embarcar-nombre" className="block text-xs font-semibold text-slate-400 mb-1">
                Nombre y apellido
              </label>
              <input
                id="embarcar-nombre"
                type="text"
                autoComplete="name"
                {...register("nombreApellido", { required: "El nombre es requerido" })}
                className="w-full bg-slate-800 border border-slate-600 rounded px-3 py-2 text-sm text-slate-200 focus:border-cyan-500 focus:ring-1 focus:ring-cyan-500 focus:outline-none"
              />
              {errors.nombreApellido && (
                <span className="text-red-400 text-xs mt-1 block">{errors.nombreApellido.message}</span>
              )}
            </div>

            <div className="sm:col-span-2 lg:col-span-3">
              <label htmlFor="embarcar-tipo" className="block text-xs font-semibold text-slate-400 mb-1">
                Tipo de personal
              </label>
              <select
                id="embarcar-tipo"
                {...register("tipoPersonal")}
                className="w-full bg-slate-800 border border-slate-600 rounded px-3 py-2 text-sm text-slate-200 focus:border-cyan-500 focus:ring-1 focus:ring-cyan-500 focus:outline-none"
              >
                <option value="Inspector">Inspector</option>
                <option value="Practico">Práctico</option>
              </select>
            </div>

            <div className="sm:col-span-2 lg:col-span-2 flex justify-end sm:justify-end lg:block">
              <button
                type="submit"
                disabled={isEmbarcando}
                className="w-full sm:w-auto lg:w-full bg-teal-600 text-white hover:bg-teal-700 px-4 py-2 rounded text-sm font-semibold transition-colors disabled:opacity-50 disabled:cursor-not-allowed"
              >
                {isEmbarcando ? "Embarcando…" : "Embarcar Personal"}
              </button>
            </div>
          </form>
        </section>

        <div className="flex-1 overflow-y-auto p-6 flex flex-col gap-6 min-h-0">
          {globalError && (
            <div className="bg-red-900/40 border border-red-500/50 text-red-200 p-4 rounded-md text-sm">
              <p className="font-semibold mb-1">Error</p>
              {globalError}
            </div>
          )}

          {isLoadingPersonal ? (
            <div className="text-center py-8 text-slate-400 animate-pulse">Cargando personal a bordo...</div>
          ) : (
            <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
              
              {/* Inspectores */}
              <section className="bg-slate-900/50 p-4 rounded-lg border border-slate-700">
                <h3 className="text-sm font-semibold text-cyan-400 uppercase tracking-widest mb-4">Inspectores</h3>
                {personalData?.inspectores && personalData.inspectores.length > 0 ? (
                  <div className="overflow-x-auto">
                    <table className="w-full text-left border-collapse">
                      <thead>
                        <tr className="border-b border-slate-600 text-xs text-slate-400 uppercase tracking-wider">
                          <th className="py-2 px-3">DNI</th>
                          <th className="py-2 px-3">Nombre</th>
                          <th className="py-2 px-3">Embarque</th>
                          <th className="py-2 px-3">Acción</th>
                        </tr>
                      </thead>
                      <tbody>
                        {personalData.inspectores.map(i => renderPersonalRow(i, "Inspector"))}
                      </tbody>
                    </table>
                  </div>
                ) : (
                  <p className="text-sm text-slate-500 italic">No hay inspectores registrados.</p>
                )}
              </section>

              {/* Prácticos */}
              <section className="bg-slate-900/50 p-4 rounded-lg border border-slate-700">
                <h3 className="text-sm font-semibold text-cyan-400 uppercase tracking-widest mb-4">Prácticos</h3>
                {personalData?.practicos && personalData.practicos.length > 0 ? (
                  <div className="overflow-x-auto">
                    <table className="w-full text-left border-collapse">
                      <thead>
                        <tr className="border-b border-slate-600 text-xs text-slate-400 uppercase tracking-wider">
                          <th className="py-2 px-3">DNI</th>
                          <th className="py-2 px-3">Nombre</th>
                          <th className="py-2 px-3">Embarque</th>
                          <th className="py-2 px-3">Acción</th>
                        </tr>
                      </thead>
                      <tbody>
                        {personalData.practicos.map(p => renderPersonalRow(p, "Practico"))}
                      </tbody>
                    </table>
                  </div>
                ) : (
                  <p className="text-sm text-slate-500 italic">No hay prácticos registrados.</p>
                )}
              </section>

            </div>
          )}
        </div>

        <div className="px-6 py-4 border-t border-slate-700 bg-slate-900/80 flex justify-end">
          <button 
            onClick={onClose}
            className="px-4 py-2 text-sm font-medium text-slate-300 hover:text-white border border-slate-600 hover:border-slate-400 rounded transition-colors"
          >
            Cerrar
          </button>
        </div>
      </div>
    </div>
  );
}
