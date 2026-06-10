import React, { useState, useEffect, useRef } from 'react';
import { useForm } from 'react-hook-form';
import { useCrearInspeccion, useModificarInspeccion } from '../../hooks/useInspecciones';
import { useViajes } from '../../hooks/useViajesApi';
import type { InspeccionDto, CrearInspeccionDto, ModificarInspeccionDto } from '../../types/inspecciones.types';

interface InspeccionModalProps {
  isOpen: boolean;
  inspeccion: InspeccionDto | null;
  viajeId?: string; // Preselected voyage ID if coming from a context
  onClose: () => void;
}

interface InspeccionFormValues {
  viajeId: string;
  fechaInspeccion: string;
  tipoInspeccion: string;
  resultado: string;
  observaciones: string;
  inspectorDatos: string;
  lugarInspeccion: string;
}

export default function InspeccionModal({ isOpen, inspeccion, viajeId, onClose }: InspeccionModalProps) {
  const isEdit = Boolean(inspeccion);
  const { mutate: crearInspeccion, isPending: isCreando } = useCrearInspeccion(viajeId);
  const { mutate: modificarInspeccion, isPending: isModificando } = useModificarInspeccion(viajeId);

  // Autocomplete de Buques Activos en Jurisdicción
  const [buqueSearch, setBuqueSearch] = useState('');
  const [showDropdown, setShowDropdown] = useState(false);
  const dropdownRef = useRef<HTMLDivElement>(null);

  // Consultamos los viajes activos filtrados por el término de búsqueda
  const { data: viajes = [], isLoading: loadingViajes } = useViajes(1, 50, buqueSearch);

  const {
    register,
    handleSubmit,
    setValue,
    reset,
    formState: { errors },
  } = useForm<InspeccionFormValues>({
    defaultValues: {
      viajeId: viajeId ?? '',
      fechaInspeccion: new Date().toISOString().substring(0, 10),
      tipoInspeccion: '',
      resultado: 'Sin Deficiencias',
      observaciones: '',
      inspectorDatos: '',
      lugarInspeccion: '',
    },
  });

  useEffect(() => {
    if (inspeccion) {
      setValue('viajeId', inspeccion.viajeId);
      setValue('fechaInspeccion', new Date(inspeccion.fechaInspeccion).toISOString().substring(0, 10));
      setValue('tipoInspeccion', inspeccion.tipoInspeccion);
      setValue('resultado', inspeccion.resultado);
      setValue('observaciones', inspeccion.observaciones);
      setValue('inspectorDatos', inspeccion.inspectorDatos);
      setValue('lugarInspeccion', inspeccion.lugarInspeccion);
      
      // Si estamos editando, cargamos el nombre del buque en el buscador
      setBuqueSearch(inspeccion.viajeId); // Fallback id o buscaremos
    } else {
      reset({
        viajeId: viajeId ?? '',
        fechaInspeccion: new Date().toISOString().substring(0, 10),
        tipoInspeccion: '',
        resultado: 'Sin Deficiencias',
        observaciones: '',
        inspectorDatos: '',
        lugarInspeccion: '',
      });
      setBuqueSearch('');
    }
  }, [inspeccion, viajeId, setValue, reset]);

  // Cerrar el dropdown al hacer click afuera
  useEffect(() => {
    const clickOutside = (e: MouseEvent) => {
      if (dropdownRef.current && !dropdownRef.current.contains(e.target as Node)) {
        setShowDropdown(false);
      }
    };
    document.addEventListener('mousedown', clickOutside);
    return () => document.removeEventListener('mousedown', clickOutside);
  }, []);

  if (!isOpen) return null;

  const onSubmit = (data: InspeccionFormValues) => {
    const payloadDate = new Date(data.fechaInspeccion).toISOString();
    
    if (isEdit && inspeccion) {
      const payload: ModificarInspeccionDto = {
        viajeId: data.viajeId,
        fechaInspeccion: payloadDate,
        tipoInspeccion: data.tipoInspeccion,
        resultado: data.resultado,
        observaciones: data.observaciones || '',
        inspectorDatos: data.inspectorDatos,
        lugarInspeccion: data.lugarInspeccion,
      };
      modificarInspeccion(
        { id: inspeccion.id, body: payload },
        {
          onSuccess: () => {
            onClose();
          },
          onError: (err: any) => {
            alert(`❌ Error al modificar inspección: ${err?.response?.data?.mensaje || err.message}`);
          },
        }
      );
    } else {
      const payload: CrearInspeccionDto = {
        viajeId: data.viajeId,
        fechaInspeccion: payloadDate,
        tipoInspeccion: data.tipoInspeccion,
        resultado: data.resultado,
        observaciones: data.observaciones || '',
        inspectorDatos: data.inspectorDatos,
        lugarInspeccion: data.lugarInspeccion,
      };
      crearInspeccion(payload, {
        onSuccess: () => {
          onClose();
        },
        onError: (err: any) => {
          alert(`❌ Error al crear inspección: ${err?.response?.data?.mensaje || err.message}`);
        },
      });
    }
  };

  const isPending = isCreando || isModificando;

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black bg-opacity-65 backdrop-blur-sm">
      <div className="bg-slate-900 border border-slate-700/60 rounded-lg shadow-2xl w-full max-w-lg mx-4 overflow-hidden">
        <div className="h-1 w-full bg-gradient-to-r from-blue-700 to-sky-500" />
        
        <div className="px-6 py-4 border-b border-slate-700/60 bg-slate-800 flex justify-between items-center text-white">
          <h3 className="text-base font-bold">
            {isEdit ? 'Editar Inspección' : 'Nueva Inspección'}
          </h3>
          <button
            onClick={onClose}
            className="text-slate-400 hover:text-white font-bold text-xl leading-none"
          >
            &times;
          </button>
        </div>

        <form onSubmit={handleSubmit(onSubmit)}>
          <div className="p-6 space-y-4 max-h-[70vh] overflow-y-auto">
            
            {/* Buscador de Buques (Viaje en Jurisdicción) */}
            <div className="relative" ref={dropdownRef}>
              <label className="block text-xs font-semibold uppercase text-slate-400 mb-1">
                Buque (Con viaje activo en jurisdicción)
              </label>
              
              <input
                type="hidden"
                {...register('viajeId', { required: 'Debe seleccionar un buque activo.' })}
              />

              <input
                type="text"
                disabled={isEdit || Boolean(viajeId)}
                placeholder="Escriba para buscar buque..."
                value={buqueSearch}
                onChange={(e) => {
                  setBuqueSearch(e.target.value);
                  setValue('viajeId', ''); // Limpia la selección previa
                  setShowDropdown(true);
                }}
                onFocus={() => {
                  if (!isEdit && !viajeId) setShowDropdown(true);
                }}
                className="w-full bg-slate-800 border border-slate-600/50 text-slate-100 text-sm rounded px-3 py-2 focus:outline-none focus:border-blue-500 disabled:opacity-50"
              />
              
              {errors.viajeId && <p className="text-red-400 text-xs mt-1">{errors.viajeId.message}</p>}

              {/* Dropdown de autocompletado */}
              {showDropdown && !isEdit && !viajeId && (
                <div className="absolute z-50 w-full mt-1 bg-slate-950 border border-slate-800 rounded-md shadow-lg max-h-48 overflow-y-auto">
                  {loadingViajes ? (
                    <div className="px-4 py-2 text-xs text-slate-400">Buscando buques...</div>
                  ) : viajes.length > 0 ? (
                    <ul>
                      {viajes.map((v) => (
                        <li key={v.id}>
                          <button
                            type="button"
                            onClick={() => {
                              setValue('viajeId', v.id);
                              setBuqueSearch(v.buque);
                              setShowDropdown(false);
                            }}
                            className="w-full text-left px-4 py-2 text-xs text-slate-300 hover:bg-slate-800 hover:text-white transition-colors"
                          >
                            <span className="font-bold">{v.buque}</span> — {v.ruta}
                          </button>
                        </li>
                      ))}
                    </ul>
                  ) : (
                    <div className="px-4 py-2 text-xs text-slate-500">No se encontraron buques activos en tu jurisdicción.</div>
                  )}
                </div>
              )}
            </div>

            {/* Fecha */}
            <div>
              <label className="block text-xs font-semibold uppercase text-slate-400 mb-1">
                Fecha de Inspección
              </label>
              <input
                type="date"
                {...register('fechaInspeccion', { required: 'La fecha de inspección es requerida.' })}
                className="w-full bg-slate-800 border border-slate-600/50 text-slate-100 text-sm rounded px-3 py-2 focus:outline-none focus:border-blue-500"
              />
              {errors.fechaInspeccion && <p className="text-red-400 text-xs mt-1">{errors.fechaInspeccion.message}</p>}
            </div>

            {/* Tipo Inspeccion */}
            <div>
              <label className="block text-xs font-semibold uppercase text-slate-400 mb-1">
                Tipo de Inspección
              </label>
              <input
                type="text"
                placeholder="Ej. PBIP, Técnica, Sanitaria"
                {...register('tipoInspeccion', { 
                  required: 'El tipo de inspección es requerido.',
                  maxLength: { value: 100, message: 'El tipo no puede superar los 100 caracteres.' }
                })}
                className="w-full bg-slate-800 border border-slate-600/50 text-slate-100 text-sm rounded px-3 py-2 focus:outline-none focus:border-blue-500"
              />
              {errors.tipoInspeccion && <p className="text-red-400 text-xs mt-1">{errors.tipoInspeccion.message}</p>}
            </div>

            {/* Lugar Inspeccion */}
            <div>
              <label className="block text-xs font-semibold uppercase text-slate-400 mb-1">
                Lugar de la Inspección
              </label>
              <input
                type="text"
                placeholder="Ej. Puerto de Buenos Aires, Km 200..."
                {...register('lugarInspeccion', { 
                  required: 'El lugar de la inspección es requerido.',
                  maxLength: { value: 200, message: 'El lugar no puede superar los 200 caracteres.' }
                })}
                className="w-full bg-slate-800 border border-slate-600/50 text-slate-100 text-sm rounded px-3 py-2 focus:outline-none focus:border-blue-500"
              />
              {errors.lugarInspeccion && <p className="text-red-400 text-xs mt-1">{errors.lugarInspeccion.message}</p>}
            </div>

            {/* Datos del Inspector */}
            <div>
              <label className="block text-xs font-semibold uppercase text-slate-400 mb-1">
                Datos del Inspector (Grado, Apellido y Nombre, Destino)
              </label>
              <input
                type="text"
                placeholder="Ej. Oficial Principal Gómez Carlos - DICO"
                {...register('inspectorDatos', { 
                  required: 'Los datos del inspector son requeridos.',
                  maxLength: { value: 200, message: 'Los datos del inspector no pueden superar los 200 caracteres.' }
                })}
                className="w-full bg-slate-800 border border-slate-600/50 text-slate-100 text-sm rounded px-3 py-2 focus:outline-none focus:border-blue-500"
              />
              {errors.inspectorDatos && <p className="text-red-400 text-xs mt-1">{errors.inspectorDatos.message}</p>}
            </div>

            {/* Resultado */}
            <div>
              <label className="block text-xs font-semibold uppercase text-slate-400 mb-1">
                Resultado
              </label>
              <select
                {...register('resultado', { required: 'El resultado es requerido.' })}
                className="w-full bg-slate-800 border border-slate-600/50 text-slate-100 text-sm rounded px-3 py-2 focus:outline-none focus:border-blue-500"
              >
                <option value="Sin Deficiencias">Sin Deficiencias</option>
                <option value="Con Deficiencias">Con Deficiencias</option>
              </select>
              {errors.resultado && <p className="text-red-400 text-xs mt-1">{errors.resultado.message}</p>}
            </div>

            {/* Observaciones */}
            <div>
              <label className="block text-xs font-semibold uppercase text-slate-400 mb-1">
                Observaciones
              </label>
              <textarea
                rows={3}
                placeholder="Detalles adicionales..."
                {...register('observaciones', {
                  maxLength: { value: 500, message: 'Las observaciones no pueden superar los 500 caracteres.' }
                })}
                className="w-full bg-slate-800 border border-slate-600/50 text-slate-100 text-sm rounded px-3 py-2 focus:outline-none focus:border-blue-500 resize-none"
              />
              {errors.observaciones && <p className="text-red-400 text-xs mt-1">{errors.observaciones.message}</p>}
            </div>
          </div>

          <div className="px-6 py-4 bg-slate-800/40 border-t border-slate-700/60 flex justify-end gap-3">
            <button
              type="button"
              onClick={onClose}
              className="px-4 py-2 text-sm font-medium text-slate-400 hover:text-slate-200 border border-slate-600/50 hover:border-slate-500 rounded transition-colors"
            >
              Cancelar
            </button>
            <button
              type="submit"
              disabled={isPending}
              className="px-5 py-2 text-sm font-semibold rounded bg-blue-600 hover:bg-blue-500 text-white disabled:opacity-50 disabled:cursor-not-allowed transition-colors"
            >
              {isPending ? 'Guardando...' : 'Confirmar'}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}
