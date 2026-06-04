// src/components/viajes/ModalPersonalExterno.tsx
import { useState } from "react";
import { useForm, type SubmitHandler } from "react-hook-form";
import { 
  useObtenerPersonal, 
  useEmbarcarPractico, 
  useDesembarcarPractico,
  useEmbarcarInspector,
  useDesembarcarInspector
} from "@/hooks/usePersonalExterno";
import type { PersonalItemDto } from "@/types/viajes.types";

interface ModalPersonalExternoProps {
  isOpen: boolean;
  onClose: () => void;
  viajeId: string;
}

interface FormValues {
  dni: string;
  nombreApellido: string;
}

export function ModalPersonalExterno({ isOpen, onClose, viajeId }: ModalPersonalExternoProps) {
  const { data: personalData, isLoading: isLoadingPersonal } = useObtenerPersonal(viajeId);
  const { mutate: embarcarPractico, isPending: isEmbarcandoPractico } = useEmbarcarPractico();
  const { mutate: desembarcarPractico, isPending: isDesembarcandoPractico } = useDesembarcarPractico();
  const { mutate: embarcarInspector, isPending: isEmbarcandoInspector } = useEmbarcarInspector();
  const { mutate: desembarcarInspector, isPending: isDesembarcandoInspector } = useDesembarcarInspector();

  const [activeTab, setActiveTab] = useState<"Inspectores" | "Practicos">("Inspectores");
  const [globalError, setGlobalError] = useState<string | null>(null);

  const { register, handleSubmit, reset, formState: { errors } } = useForm<FormValues>({
    defaultValues: {
      dni: "",
      nombreApellido: "",
    },
  });

  const isEmbarcando = isEmbarcandoPractico || isEmbarcandoInspector;
  const isDesembarcando = isDesembarcandoPractico || isDesembarcandoInspector;

  const onSubmit: SubmitHandler<FormValues> = (data) => {
    setGlobalError(null);
    if (activeTab === "Inspectores") {
      embarcarInspector(
        { viajeId, payload: { dni: data.dni, nombreApellido: data.nombreApellido } },
        {
          onSuccess: () => {
            reset();
          },
          onError: (err) => {
            setGlobalError(err.message);
          },
        }
      );
    } else {
      embarcarPractico(
        { viajeId, payload: { dni: data.dni, nombreApellido: data.nombreApellido } },
        {
          onSuccess: () => {
            reset();
          },
          onError: (err) => {
            setGlobalError(err.message);
          },
        }
      );
    }
  };

  const handleDesembarcarPracticoClick = (dni: string) => {
    if (!window.confirm(`¿Seguro que desea registrar el desembarque del práctico con DNI ${dni}?`)) return;
    
    setGlobalError(null);
    desembarcarPractico({
      viajeId,
      dni,
      payload: {}
    }, {
      onError: (err) => {
        setGlobalError(err.message);
      }
    });
  };

  const handleDesembarcarInspectorClick = (dni: string) => {
    if (!window.confirm(`¿Seguro que desea registrar el desembarque del inspector con DNI ${dni}?`)) return;
    
    setGlobalError(null);
    desembarcarInspector({
      viajeId,
      dni,
      payload: {}
    }, {
      onError: (err) => {
        setGlobalError(err.message);
      }
    });
  };

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

        {/* Selector de Pestañas / Secciones */}
        <div className="flex border-b border-slate-700 bg-slate-800/30 shrink-0">
          <button
            onClick={() => { setActiveTab("Inspectores"); setGlobalError(null); reset(); }}
            className={`flex-1 py-3 text-sm font-semibold border-b-2 transition-all ${
              activeTab === "Inspectores"
                ? "border-cyan-500 text-cyan-400 bg-cyan-500/5"
                : "border-transparent text-slate-400 hover:text-slate-200 hover:bg-slate-800/30"
            }`}
          >
            Inspectores
          </button>
          <button
            onClick={() => { setActiveTab("Practicos"); setGlobalError(null); reset(); }}
            className={`flex-1 py-3 text-sm font-semibold border-b-2 transition-all ${
              activeTab === "Practicos"
                ? "border-cyan-500 text-cyan-400 bg-cyan-500/5"
                : "border-transparent text-slate-400 hover:text-slate-200 hover:bg-slate-800/30"
            }`}
          >
            Prácticos
          </button>
        </div>

        {/* Formulario Embarcar */}
        <section className="shrink-0 px-6 py-4 border-b border-slate-700 bg-slate-900/40">
          <h3 className="text-sm font-semibold text-cyan-400 uppercase tracking-widest mb-3">
            Embarcar {activeTab === "Inspectores" ? "Inspector" : "Práctico"}
          </h3>
          <form
            onSubmit={handleSubmit(onSubmit)}
            className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-12 gap-4 items-end"
          >
            <div className="sm:col-span-1 lg:col-span-4">
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

            <div className="sm:col-span-1 lg:col-span-6">
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

            <div className="sm:col-span-2 lg:col-span-2 flex justify-end sm:justify-end lg:block">
              <button
                type="submit"
                disabled={isEmbarcando}
                className="w-full sm:w-auto lg:w-full bg-teal-600 text-white hover:bg-teal-700 px-4 py-2 rounded text-sm font-semibold transition-colors disabled:opacity-50 disabled:cursor-not-allowed"
              >
                {isEmbarcando ? "Embarcando…" : "Embarcar"}
              </button>
            </div>
          </form>
        </section>

        {/* Listado de Personal */}
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
            <div className="bg-slate-900/50 p-4 rounded-lg border border-slate-700">
              {activeTab === "Inspectores" ? (
                <>
                  <h3 className="text-sm font-semibold text-cyan-400 uppercase tracking-widest mb-4">Inspectores a Bordo</h3>
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
                          {personalData.inspectores.map((item) => (
                            <tr key={`${item.documento}-${item.fechaEmbarque}`} className="border-b border-slate-700/50 hover:bg-slate-800/30">
                              <td className="py-2 px-3 text-sm text-slate-300">{item.documento}</td>
                              <td className="py-2 px-3 text-sm text-slate-300">{item.nombreApellido}</td>
                              <td className="py-2 px-3 text-sm text-slate-300">{new Date(item.fechaEmbarque).toLocaleString()}</td>
                              <td className="py-2 px-3 text-sm">
                                {item.estaABordo ? (
                                  <button 
                                    onClick={() => handleDesembarcarInspectorClick(item.documento)}
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
                          ))}
                        </tbody>
                      </table>
                    </div>
                  ) : (
                    <p className="text-sm text-slate-500 italic">No hay inspectores registrados.</p>
                  )}
                </>
              ) : (
                <>
                  <h3 className="text-sm font-semibold text-cyan-400 uppercase tracking-widest mb-4">Prácticos a Bordo</h3>
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
                          {personalData.practicos.map((item) => (
                            <tr key={`${item.documento}-${item.fechaEmbarque}`} className="border-b border-slate-700/50 hover:bg-slate-800/30">
                              <td className="py-2 px-3 text-sm text-slate-300">{item.documento}</td>
                              <td className="py-2 px-3 text-sm text-slate-300">{item.nombreApellido}</td>
                              <td className="py-2 px-3 text-sm text-slate-300">{new Date(item.fechaEmbarque).toLocaleString()}</td>
                              <td className="py-2 px-3 text-sm">
                                {item.estaABordo ? (
                                  <button 
                                    onClick={() => handleDesembarcarPracticoClick(item.documento)}
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
                          ))}
                        </tbody>
                      </table>
                    </div>
                  ) : (
                    <p className="text-sm text-slate-500 italic">No hay prácticos registrados.</p>
                  )}
                </>
              )}
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
