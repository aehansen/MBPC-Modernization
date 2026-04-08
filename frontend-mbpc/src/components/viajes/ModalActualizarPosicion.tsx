import { useState, useEffect, useId } from "react";
import { parseCoordinates, formatearDMS } from "../../utils/coordinates";
import { useActualizarPosicion } from "../../hooks/useActualizarPosicion";
import type { CoordenadasDecimales } from "../../utils/coordinates";

interface Props {
  viajeId: string;
  nombreBuque: string;
  onClose: () => void;
  onExito?: () => void;
}

export default function ModalActualizarPosicion({ viajeId, nombreBuque, onClose, onExito }: Props) {
  const inputId = useId();
  const [inputValor, setInputValor] = useState("");
  const [coordsParsed, setCoordsParsed] = useState<CoordenadasDecimales | null>(null);

  const mutation = useActualizarPosicion(viajeId);

  useEffect(() => {
    setCoordsParsed(parseCoordinates(inputValor));
    mutation.reset();
  }, [inputValor]);

  const handleSubmit = () => {
    if (!coordsParsed) return;
    
    mutation.mutate({
      latitud: coordsParsed.lat,
      longitud: coordsParsed.lng,
      fechaReporte: new Date().toISOString()
    }, {
      onSuccess: () => {
        setTimeout(() => {
          if (onExito) onExito();
          onClose();
        }, 2000);
      }
    });
  };

  const puedeEnviar = coordsParsed !== null && !mutation.isPending && !mutation.isSuccess;

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-gray-900/60 backdrop-blur-sm">
      <div className="bg-white rounded-2xl shadow-2xl w-full max-w-md overflow-hidden flex flex-col">
        {/* Header Institucional */}
        <div className="bg-[#002454] px-6 py-4 flex justify-between items-center">
          <div className="flex items-center gap-3">
            <img 
              src="https://www.argentina.gob.ar/sites/default/files/styles/isotipo/public/imagenEncabezado/prefectura-escudo.png?itok=EywBfOaV" 
              alt="Escudo PNA" 
              className="h-8 w-auto"
            />
            <div>
              <h2 className="text-white font-bold text-lg leading-tight">Actualizar Posición</h2>
              <p className="text-blue-200 text-xs font-medium tracking-wider uppercase">
                {nombreBuque}
              </p>
            </div>
          </div>
          <button onClick={onClose} disabled={mutation.isPending} className="text-blue-200 hover:text-white transition">
            <svg className="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path strokeLinecap="round" strokeLinejoin="round" strokeWidth="2" d="M6 18L18 6M6 6l12 12" /></svg>
          </button>
        </div>

        <div className="p-6 flex flex-col gap-5">
          <div>
            <label htmlFor={inputId} className="block text-xs font-bold text-gray-700 uppercase tracking-wide mb-2">
              Coordenadas (DMS o Decimal)
            </label>
            <input
              id={inputId}
              type="text"
              autoFocus
              disabled={mutation.isPending || mutation.isSuccess}
              placeholder="Ej: 34° 35' 29.15&quot; S, 58° 21' 22.75&quot; W"
              className="w-full px-4 py-3 bg-gray-50 border border-gray-300 rounded-xl text-gray-900 focus:bg-white focus:ring-2 focus:ring-[#104a8e] focus:border-transparent transition-all outline-none"
              value={inputValor}
              onChange={(e) => setInputValor(e.target.value)}
            />
          </div>

          {/* Feedback de Parsing */}
          {inputValor.trim().length > 0 && (
            <div className={`px-4 py-3 rounded-xl border flex items-start gap-3 transition-colors ${coordsParsed ? "bg-green-50 border-green-200" : "bg-red-50 border-red-200"}`}>
              {coordsParsed ? (
                <>
                  <span className="text-green-600 mt-0.5"><svg className="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path strokeLinecap="round" strokeLinejoin="round" strokeWidth="2" d="M9 12l2 2 4-4m6 2a9 9 0 11-18 0 9 9 0 0118 0z" /></svg></span>
                  <div>
                    <p className="text-green-800 text-sm font-semibold">Coordenadas válidas</p>
                    <p className="text-green-700 text-xs font-mono mt-1">Lat: {coordsParsed.lat.toFixed(6)} <br/> Lng: {coordsParsed.lng.toFixed(6)}</p>
                    <p className="text-green-600/80 text-[10px] mt-1">{formatearDMS(coordsParsed.lat, coordsParsed.lng)}</p>
                  </div>
                </>
              ) : (
                <>
                  <span className="text-red-500 mt-0.5"><svg className="w-5 h-5" fill="none" stroke="currentColor" viewBox="0 0 24 24"><path strokeLinecap="round" strokeLinejoin="round" strokeWidth="2" d="M12 8v4m0 4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z" /></svg></span>
                  <p className="text-red-700 text-sm font-medium leading-snug">Formato irreconocible. Revisa los símbolos o usa formato decimal.</p>
                </>
              )}
            </div>
          )}

          {/* Errores del Backend (Cinemática) */}
          {mutation.isError && (
             <div className="px-4 py-3 bg-red-50 border border-red-200 rounded-xl text-red-700 text-sm font-medium">
               {(mutation.error as any)?.response?.data?.mensaje || mutation.error.message || "Ocurrió un error al actualizar."}
             </div>
          )}

          {/* Feedback de Éxito */}
          {mutation.isSuccess && mutation.data && (
            <div className="px-4 py-3 bg-blue-50 border border-blue-200 rounded-xl">
              <p className="text-[#002454] font-bold text-sm mb-1">Posición actualizada correctamente.</p>
              <div className="flex gap-4 mt-2">
                <div className="bg-white px-3 py-1.5 rounded border border-blue-100">
                  <p className="text-[10px] text-gray-500 uppercase font-bold">Velocidad</p>
                  <p className="text-sm font-mono text-[#104a8e]">{mutation.data.velocidadCalculadaKn} kn</p>
                </div>
                <div className="bg-white px-3 py-1.5 rounded border border-blue-100">
                  <p className="text-[10px] text-gray-500 uppercase font-bold">Distancia</p>
                  <p className="text-sm font-mono text-[#104a8e]">{mutation.data.distanciaRecorridaNM} nm</p>
                </div>
              </div>
            </div>
          )}
        </div>

        <div className="px-6 py-4 bg-gray-50 border-t flex justify-end gap-3">
          <button onClick={onClose} disabled={mutation.isPending} className="px-4 py-2 text-sm font-medium rounded-lg border border-gray-300 text-gray-700 hover:bg-gray-100 transition-colors disabled:opacity-50">
            Cancelar
          </button>
          <button onClick={handleSubmit} disabled={!puedeEnviar} className={`px-5 py-2 text-sm font-semibold rounded-lg text-white transition-all disabled:opacity-40 ${puedeEnviar ? "bg-[#104a8e] hover:bg-[#002454]" : "bg-gray-500"}`}>
            {mutation.isPending ? "Actualizando…" : mutation.isSuccess ? "✓ Actualizado" : "Guardar Posición"}
          </button>
        </div>
      </div>
    </div>
  );
}