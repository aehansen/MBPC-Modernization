import React from 'react';
import {
  useTransferenciasPendientes,
  useAprobarTransferencia,
  useRechazarTransferencia,
} from '../../hooks/useViajesApi';
import { getNombreCostera } from '../../constants/costeras';

export default function PanelTransferencias() {
  const { data: transferencias, isLoading, isError } = useTransferenciasPendientes();
  const { mutate: aprobar, isPending: isAprobando } = useAprobarTransferencia();
  const { mutate: rechazar, isPending: isRechazando } = useRechazarTransferencia();

  if (isLoading) {
    return (
      <div className="bg-[#002454]/40 border border-blue-900/50 backdrop-blur-md rounded-xl p-4 flex items-center justify-between animate-pulse">
        <div className="flex items-center gap-3">
          <div className="w-5 h-5 rounded-full bg-blue-500/30 flex items-center justify-center">
            <span className="animate-spin text-xs text-blue-400">🌀</span>
          </div>
          <span className="text-sm text-slate-300 font-medium">Buscando solicitudes de transferencia...</span>
        </div>
      </div>
    );
  }

  if (isError || !transferencias || transferencias.length === 0) {
    return null;
  }

  return (
    <div className="space-y-3">
      <div className="flex items-center gap-2">
        <span className="flex h-2 w-2 relative">
          <span className="animate-ping absolute inline-flex h-full w-full rounded-full bg-amber-400 opacity-75"></span>
          <span className="relative inline-flex rounded-full h-2 w-2 bg-amber-500"></span>
        </span>
        <h3 className="text-xs font-bold tracking-widest uppercase text-amber-500">
          Transferencias de Jurisdicción Requeridas
        </h3>
      </div>

      <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
        {transferencias.map((tr) => {
          const idCosteraActual = tr.costeraId ? Number(tr.costeraId) : null;
          const idCosteraDestino = tr.costeraIdPendiente ? Number(tr.costeraIdPendiente) : null;

          const nombreActual = idCosteraActual ? getNombreCostera(idCosteraActual) : 'Sin Costera';
          const nombreDestino = idCosteraDestino ? getNombreCostera(idCosteraDestino) : 'Sin Costera';

          return (
            <div
              key={tr.id}
              className="relative overflow-hidden bg-slate-900 border border-amber-500/40 rounded-xl p-4 shadow-lg shadow-black/30 flex flex-col justify-between transition-all hover:border-amber-500/70"
            >
              {/* Franja izquierda */}
              <div className="absolute left-0 top-0 bottom-0 w-1 bg-gradient-to-b from-amber-500 to-amber-600" />

              <div className="pl-2 flex justify-between items-start">
                <div>
                  <h4 className="text-sm font-bold text-slate-100 flex items-center gap-2">
                    <span className="text-lg">🚢</span> {tr.buque || tr.nombreBuque || 'Buque Desconocido'}
                  </h4>
                  <p className="text-xs text-slate-400 mt-1">
                    Cruce de geocerca detectado (Traspaso pendiente)
                  </p>
                </div>
                {tr.esConvoy && (
                  <span className="bg-blue-900/50 text-blue-300 border border-blue-800 text-[10px] uppercase font-bold tracking-wider px-2 py-0.5 rounded-full">
                    Convoy
                  </span>
                )}
              </div>

              {/* Jurisdicciones origen/destino */}
              <div className="pl-2 my-4 flex items-center justify-between bg-slate-950/40 border border-slate-800 rounded-lg p-2.5">
                <div className="flex-1 text-center">
                  <span className="block text-[10px] uppercase font-semibold text-slate-500 tracking-wider">
                    Origen (Actual)
                  </span>
                  <span className="block text-xs font-bold text-slate-300 truncate" title={nombreActual}>
                    {nombreActual}
                  </span>
                </div>
                <div className="px-2 text-amber-500 font-bold text-lg animate-pulse">
                  ➜
                </div>
                <div className="flex-1 text-center">
                  <span className="block text-[10px] uppercase font-semibold text-slate-500 tracking-wider">
                    Destino
                  </span>
                  <span className="block text-xs font-bold text-amber-400 truncate" title={nombreDestino}>
                    {nombreDestino}
                  </span>
                </div>
              </div>

              {/* Botonera de aprobación */}
              <div className="pl-2 flex justify-end gap-2 mt-2">
                <button
                  type="button"
                  disabled={isAprobando || isRechazando}
                  onClick={() => rechazar(tr.id)}
                  className="px-3 py-1.5 text-xs font-semibold text-slate-400 hover:text-slate-200 border border-slate-700 hover:border-slate-500 rounded transition duration-150 disabled:opacity-40 disabled:cursor-not-allowed"
                >
                  Rechazar
                </button>
                <button
                  type="button"
                  disabled={isAprobando || isRechazando}
                  onClick={() => aprobar(tr.id)}
                  className="px-3.5 py-1.5 text-xs font-bold bg-amber-600 hover:bg-amber-500 text-white rounded shadow transition duration-150 disabled:bg-amber-900 disabled:text-amber-700 disabled:cursor-not-allowed flex items-center gap-1.5"
                >
                  {isAprobando ? (
                    <>
                      <span className="animate-spin text-xs">🌀</span>
                      <span>Procesando...</span>
                    </>
                  ) : (
                    <>
                      <span>Aprobar Traspaso</span>
                    </>
                  )}
                </button>
              </div>
            </div>
          );
        })}
      </div>
    </div>
  );
}
