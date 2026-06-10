import React from 'react';
import { buildExternalUrl } from '../../utils/ssoLinks';

type SistemaExterno = 'DEB' | 'PBIP' | 'CIALA' | 'PROGEBU';

interface BuqueSeleccionado {
  matricula: string;
  nombre: string;
  omi?: string;
}

interface LaunchpadModalProps {
  isOpen: boolean;
  onClose: () => void;
  buqueSeleccionado: BuqueSeleccionado;
}

interface SistemaConfig {
  id: SistemaExterno;
  nombre: string;
  descripcion: string;
  icon: React.ReactNode;
  accentColor: string;
}

const SISTEMAS: SistemaConfig[] = [
  {
    id: 'DEB',
    nombre: 'DEB',
    descripcion: 'Despacho Electrónico de Buques',
    accentColor: 'hover:border-blue-400 hover:bg-blue-50',
    icon: (
      <svg className="w-8 h-8" fill="none" stroke="currentColor" strokeWidth="1.5" viewBox="0 0 24 24">
        <path strokeLinecap="round" strokeLinejoin="round"
          d="M9 12h3.75M9 15h3.75M9 18h3.75m3 .75H18a2.25 2.25 0 002.25-2.25V6.108c0-1.135-.845-2.098-1.976-2.192a48.424 48.424 0 00-1.123-.08m-5.801 0c-.065.21-.1.433-.1.664 0 .414.336.75.75.75h4.5a.75.75 0 00.75-.75 2.25 2.25 0 00-.1-.664m-5.8 0A2.251 2.251 0 0113.5 2.25H15c1.012 0 1.867.668 2.15 1.586m-5.8 0c-.376.023-.75.05-1.124.08C9.095 4.01 8.25 4.973 8.25 6.108V8.25m0 0H4.875c-.621 0-1.125.504-1.125 1.125v11.25c0 .621.504 1.125 1.125 1.125h9.75c.621 0 1.125-.504 1.125-1.125V9.375c0-.621-.504-1.125-1.125-1.125H8.25z" />
      </svg>
    ),
  },
  {
    id: 'PBIP',
    nombre: 'PBIP',
    descripcion: 'Protección de Buques e Instalaciones Portuarias',
    accentColor: 'hover:border-amber-400 hover:bg-amber-50',
    icon: (
      <svg className="w-8 h-8" fill="none" stroke="currentColor" strokeWidth="1.5" viewBox="0 0 24 24">
        <path strokeLinecap="round" strokeLinejoin="round"
          d="M9 12.75L11.25 15 15 9.75m-3-7.036A11.959 11.959 0 013.598 6 11.99 11.99 0 003 9.749c0 5.592 3.824 10.29 9 11.623 5.176-1.332 9-6.03 9-11.622 0-1.31-.21-2.571-.598-3.751h-.152c-3.196 0-6.1-1.248-8.25-3.285z" />
      </svg>
    ),
  },
  {
    id: 'CIALA',
    nombre: 'CIALA',
    descripcion: 'Centro de Información del Acuerdo Latinoamericano',
    accentColor: 'hover:border-green-400 hover:bg-green-50',
    icon: (
      <svg className="w-8 h-8" fill="none" stroke="currentColor" strokeWidth="1.5" viewBox="0 0 24 24">
        <path strokeLinecap="round" strokeLinejoin="round"
          d="M12 21a9.004 9.004 0 008.716-6.747M12 21a9.004 9.004 0 01-8.716-6.747M12 21c2.485 0 4.5-4.03 4.5-9S14.485 3 12 3m0 18c-2.485 0-4.5-4.03-4.5-9S9.515 3 12 3m0 0a8.997 8.997 0 017.843 4.582M12 3a8.997 8.997 0 00-7.843 4.582m15.686 0A11.953 11.953 0 0112 10.5c-2.998 0-5.74-1.1-7.843-2.918m15.686 0A8.959 8.959 0 0121 12c0 .778-.099 1.533-.284 2.253m0 0A17.919 17.919 0 0112 16.5c-3.162 0-6.133-.815-8.716-2.247m0 0A9.015 9.015 0 013 12c0-1.605.42-3.113 1.157-4.418" />
      </svg>
    ),
  },
  {
    id: 'PROGEBU',
    nombre: 'PROGEBU',
    descripcion: 'Consulta de Buques Nacionales - Proceso de gestión de buques',
    accentColor: 'hover:border-violet-400 hover:bg-violet-50',
    icon: (
      <svg className="w-8 h-8" fill="none" stroke="currentColor" strokeWidth="1.5" viewBox="0 0 24 24">
        <path strokeLinecap="round" strokeLinejoin="round"
          d="M3.75 21h16.5M4.5 3h15M5.25 3v18m13.5-18v18M9 6.75h1.5m-1.5 3h1.5m-1.5 3h1.5m3-6H15m-1.5 3H15m-1.5 3H15M9 21v-3.375c0-.621.504-1.125 1.125-1.125h3.75c.621 0 1.125.504 1.125 1.125V21" />
      </svg>
    ),
  },
];

export default function LaunchpadModal({ isOpen, onClose, buqueSeleccionado }: LaunchpadModalProps) {
  if (!isOpen) return null;

  const handleLanzar = (sistemaId: SistemaExterno) => {
    const omiVal = buqueSeleccionado.omi?.trim();
    const matriculaVal = buqueSeleccionado.matricula?.trim();

    const isOmiMissing = !omiVal || omiVal === '-' || omiVal.toLowerCase() === 'no posee' || omiVal.toLowerCase() === 's/d';
    const isMatriculaMissing = !matriculaVal || matriculaVal === '-' || matriculaVal.toLowerCase() === 'no posee' || matriculaVal.toLowerCase() === 's/d' || matriculaVal.toLowerCase() === 'null';

    if (['DEB', 'PBIP', 'CIALA'].includes(sistemaId) && isOmiMissing) {
      alert(`Error: El buque no posee número OMI. No es posible consultar la plataforma ${sistemaId}.`);
      return;
    }

    if (sistemaId === 'PROGEBU' && isMatriculaMissing) {
      alert(`Error: El buque no posee número de Matrícula. No es posible consultar la plataforma PROGEBU.`);
      return;
    }

    const url = buildExternalUrl(sistemaId, buqueSeleccionado);
    window.open(url, '_blank');
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black bg-opacity-65 backdrop-blur-sm">
      <div className="bg-white border border-slate-200/80 rounded-2xl shadow-2xl w-full max-w-xl mx-4 overflow-hidden">

        {/* Franja superior */}
        <div className="h-1 w-full bg-gradient-to-r from-blue-600 to-sky-400" />

        {/* Cabecera */}
        <div className="px-6 py-4 border-b border-slate-200/80 flex justify-between items-start">
          <div>
            <h3 className="text-base font-bold text-slate-900">Sistemas Externos</h3>
            <p className="text-xs text-slate-500 mt-0.5">
              Buque:{' '}
              <span className="font-semibold text-slate-700">
                {buqueSeleccionado.nombre}
              </span>
              {' '}—{' '}
              <span className="font-mono text-slate-600">{buqueSeleccionado.matricula}</span>
            </p>
          </div>
          <button
            onClick={onClose}
            aria-label="Cerrar"
            className="text-slate-400 hover:text-slate-700 text-xl font-bold leading-none mt-0.5 transition-colors"
          >
            &times;
          </button>
        </div>

        {/* Grilla 2×2 */}
        <div className="p-6 grid grid-cols-2 gap-4">
          {SISTEMAS.map((sistema) => (
            <button
              key={sistema.id}
              type="button"
              onClick={() => handleLanzar(sistema.id)}
              className={`
                group flex flex-col items-center justify-center gap-3
                p-5 rounded-xl border-2 border-slate-200
                text-slate-700 transition-all duration-150
                ${sistema.accentColor}
              `}
            >
              <span className="text-slate-500 group-hover:text-slate-800 transition-colors">
                {sistema.icon}
              </span>
              <span className="font-bold text-sm text-slate-900">{sistema.nombre}</span>
              <span className="text-xs text-slate-500 text-center leading-snug">
                {sistema.descripcion}
              </span>
              <span className="flex items-center gap-1 text-xs font-medium text-blue-600 opacity-0 group-hover:opacity-100 transition-opacity">
                Abrir
                <svg className="w-3 h-3" fill="none" stroke="currentColor" strokeWidth="2" viewBox="0 0 24 24">
                  <path strokeLinecap="round" strokeLinejoin="round" d="M13.5 6H5.25A2.25 2.25 0 003 8.25v10.5A2.25 2.25 0 005.25 21h10.5A2.25 2.25 0 0018 18.75V10.5m-10.5 6L21 3m0 0h-5.25M21 3v5.25" />
                </svg>
              </span>
            </button>
          ))}
        </div>

        {/* Pie */}
        <div className="px-6 py-3 bg-slate-50 border-t border-slate-200/80 flex justify-between items-center">
          <p className="text-xs text-slate-400">
            Se abrirá una nueva pestaña con sesión SSO pre-autenticada.
          </p>
          <button
            type="button"
            onClick={onClose}
            className="px-4 py-1.5 text-xs font-medium text-slate-500 hover:text-slate-800 border border-slate-300 hover:border-slate-400 rounded-lg transition-colors"
          >
            Cerrar
          </button>
        </div>
      </div>
    </div>
  );
}
