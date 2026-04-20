// src/components/ModalAmarrarBarcaza.tsx
// ──────────────────────────────────────────────────────────────────────────────
// Modal institucional para la acción "Amarrar Barcaza".
// UI sobria en tonos azul/gris de la Prefectura Naval Argentina.
// Dependencias externas: react-hot-toast (toast feedback).
// ──────────────────────────────────────────────────────────────────────────────

import React, { useCallback, useEffect, useRef, useState } from "react";
import { createPortal } from "react-dom";
import toast from "react-hot-toast";
import { useAmarrarBarcaza } from "../hooks/useAmarrarBarcaza";
import type { AmarrarBarcazaError } from "../types/amarrarBarcaza.types";

// ─── Tipos ────────────────────────────────────────────────────────────────────

interface ModalAmarrarBarcazaProps {
  /** Controla la visibilidad del modal. */
  isOpen: boolean;
  /** Callback para cerrar el modal (desde el padre). */
  onClose: () => void;
}

// ─── Opciones de muelle ───────────────────────────────────────────────────────

/**
 * Lista de muelles disponibles.
 * Reemplazar o extender con datos dinámicos si el backend los expone.
 */
const MUELLES_DISPONIBLES: readonly string[] = [
  "M-01",
  "M-02",
  "M-03",
  "M-04",
  "M-05",
  "M-06",
  "M-07",
  "M-08",
];

// ─── Estado inicial del formulario ────────────────────────────────────────────

interface FormState {
  idBarcaza: string;
  nuevoMuelle: string;
}

const FORM_INITIAL: FormState = {
  idBarcaza: "",
  nuevoMuelle: "",
};

// ─── Componente ───────────────────────────────────────────────────────────────

export default function ModalAmarrarBarcaza({
  isOpen,
  onClose,
}: ModalAmarrarBarcazaProps) {
  const [form, setForm] = useState<FormState>(FORM_INITIAL);
  const firstInputRef = useRef<HTMLInputElement>(null);

  const { mutate, isPending, reset } = useAmarrarBarcaza();

  // Resetear formulario y estado de mutación al abrir/cerrar.
  useEffect(() => {
    if (isOpen) {
      setForm(FORM_INITIAL);
      reset();
      // Focus al primer input tras la apertura.
      setTimeout(() => firstInputRef.current?.focus(), 50);
    }
  }, [isOpen, reset]);

  // Cerrar con Escape.
  useEffect(() => {
    const handleKeyDown = (e: KeyboardEvent) => {
      if (e.key === "Escape" && isOpen && !isPending) onClose();
    };
    document.addEventListener("keydown", handleKeyDown);
    return () => document.removeEventListener("keydown", handleKeyDown);
  }, [isOpen, isPending, onClose]);

  // ── Handlers ──────────────────────────────────────────────────────────────

  const handleChange = useCallback(
    (e: React.ChangeEvent<HTMLInputElement | HTMLSelectElement>) => {
      const { name, value } = e.target;
      setForm((prev) => ({ ...prev, [name]: value }));
    },
    []
  );

  const isFormValid =
    form.idBarcaza.trim().length > 0 && form.nuevoMuelle.trim().length > 0;

  const handleConfirmar = useCallback(() => {
    if (!isFormValid || isPending) return;

    mutate(
      { id: form.idBarcaza.trim(), nuevoMuelle: form.nuevoMuelle.trim() },
      {
        onSuccess: (data) => {
          toast.success(data.mensaje, {
            duration: 4000,
            position: "top-right",
            style: {
              background: "#1e3a5f",
              color: "#e2e8f0",
              border: "1px solid #2d5a8f",
              fontFamily: "inherit",
              fontSize: "0.875rem",
            },
            iconTheme: { primary: "#38bdf8", secondary: "#fff" },
          });
          onClose();
        },
        onError: (error: AmarrarBarcazaError) => {
          toast.error(error.mensaje, {
            duration: 5000,
            position: "top-right",
            style: {
              background: "#1e1e2e",
              color: "#f1c0b0",
              border: "1px solid #7f3b3b",
              fontFamily: "inherit",
              fontSize: "0.875rem",
            },
          });
        },
      }
    );
  }, [form, isFormValid, isPending, mutate, onClose]);

  // ── Render ────────────────────────────────────────────────────────────────

  if (!isOpen) return null;

  const modalContent = (
    // Backdrop
    <div
      role="dialog"
      aria-modal="true"
      aria-labelledby="modal-amarrar-titulo"
      className="fixed inset-0 z-50 flex items-center justify-center"
    >
      {/* Overlay semitransparente */}
      <div
        className="absolute inset-0 bg-slate-900/70 backdrop-blur-sm"
        onClick={!isPending ? onClose : undefined}
        aria-hidden="true"
      />

      {/* Panel del modal */}
      <div className="relative z-10 w-full max-w-md mx-4">
        {/* Borde superior decorativo institucional */}
        <div className="h-1 w-full bg-gradient-to-r from-blue-800 via-blue-500 to-sky-400 rounded-t-lg" />

        <div className="bg-white rounded-b-lg shadow-2xl shadow-slate-900/40">

          {/* ── Header ─────────────────────────────────────────────────── */}
          <div className="flex items-center justify-between px-6 py-4 bg-slate-800 rounded-b-none">
            <div className="flex items-center gap-3">
              <img
                src="https://www.argentina.gob.ar/sites/default/files/styles/isotipo/public/imagenEncabezado/prefectura-escudo.png?itok=EywBfOaV"
                alt="Escudo PNA"
                className="h-9 w-auto flex-shrink-0"
              />
              <div>
                <h2
                  id="modal-amarrar-titulo"
                  className="text-sm font-semibold text-slate-100 tracking-wide"
                >
                  Amarrar Barcaza
                </h2>
                <p className="text-xs text-slate-400 mt-0.5">
                  Prefectura Naval Argentina — MBPC Geo H2
                </p>
              </div>
            </div>
            <button
              onClick={onClose}
              disabled={isPending}
              aria-label="Cerrar modal"
              className="text-slate-400 hover:text-slate-200 disabled:opacity-40 transition-colors p-1 rounded"
            >
              <svg
                xmlns="http://www.w3.org/2000/svg"
                className="w-4 h-4"
                fill="none"
                viewBox="0 0 24 24"
                stroke="currentColor"
                strokeWidth={2}
              >
                <path
                  strokeLinecap="round"
                  strokeLinejoin="round"
                  d="M6 18L18 6M6 6l12 12"
                />
              </svg>
            </button>
          </div>

          {/* ── Body ───────────────────────────────────────────────────── */}
          <div className="px-6 py-5 space-y-5">

            {/* Aviso institucional */}
            <div className="flex gap-2 items-start p-3 bg-blue-50 border border-blue-200 rounded-md">
              <svg
                xmlns="http://www.w3.org/2000/svg"
                className="w-4 h-4 text-blue-600 mt-0.5 shrink-0"
                fill="none"
                viewBox="0 0 24 24"
                stroke="currentColor"
                strokeWidth={2}
              >
                <path
                  strokeLinecap="round"
                  strokeLinejoin="round"
                  d="M13 16h-1v-4h-1m1-4h.01M12 2a10 10 0 100 20A10 10 0 0012 2z"
                />
              </svg>
              <p className="text-xs text-blue-700 leading-relaxed">
                Esta acción registrará el amarre de la barcaza en el muelle
                seleccionado y actualizará el estado operativo del viaje
                asociado.
              </p>
            </div>

            {/* Campo: ID Barcaza */}
            <div className="space-y-1.5">
              <label
                htmlFor="idBarcaza"
                className="block text-xs font-semibold text-slate-600 uppercase tracking-wider"
              >
                ID de la Barcaza
                <span className="text-red-500 ml-0.5">*</span>
              </label>
              <input
                ref={firstInputRef}
                id="idBarcaza"
                name="idBarcaza"
                type="text"
                value={form.idBarcaza}
                onChange={handleChange}
                disabled={isPending}
                placeholder="Ej: BRC-001"
                autoComplete="off"
                spellCheck={false}
                className="
                  w-full px-3 py-2.5 text-sm text-slate-800
                  border border-slate-300 rounded-md
                  bg-slate-50 placeholder-slate-400
                  focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500
                  disabled:opacity-60 disabled:bg-slate-100 disabled:cursor-not-allowed
                  transition-colors
                "
              />
            </div>

            {/* Campo: Nuevo Muelle */}
            <div className="space-y-1.5">
              <label
                htmlFor="nuevoMuelle"
                className="block text-xs font-semibold text-slate-600 uppercase tracking-wider"
              >
                Nuevo Muelle
                <span className="text-red-500 ml-0.5">*</span>
              </label>
              <select
                id="nuevoMuelle"
                name="nuevoMuelle"
                value={form.nuevoMuelle}
                onChange={handleChange}
                disabled={isPending}
                className="
                  w-full px-3 py-2.5 text-sm text-slate-800
                  border border-slate-300 rounded-md
                  bg-slate-50
                  focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500
                  disabled:opacity-60 disabled:bg-slate-100 disabled:cursor-not-allowed
                  transition-colors appearance-none
                "
              >
                <option value="" disabled>
                  — Seleccione un muelle —
                </option>
                {MUELLES_DISPONIBLES.map((muelle) => (
                  <option key={muelle} value={muelle}>
                    {muelle}
                  </option>
                ))}
              </select>
            </div>
          </div>

          {/* ── Footer / Acciones ──────────────────────────────────────── */}
          <div className="flex items-center justify-end gap-3 px-6 py-4 bg-slate-50 border-t border-slate-200 rounded-b-lg">
            <button
              type="button"
              onClick={onClose}
              disabled={isPending}
              className="
                px-4 py-2 text-sm font-medium text-slate-600
                border border-slate-300 rounded-md bg-white
                hover:bg-slate-100 hover:text-slate-800
                disabled:opacity-50 disabled:cursor-not-allowed
                transition-colors
              "
            >
              Cancelar
            </button>

            <button
              type="button"
              onClick={handleConfirmar}
              disabled={!isFormValid || isPending}
              className="
                inline-flex items-center gap-2
                px-5 py-2 text-sm font-semibold text-white
                bg-blue-700 hover:bg-blue-800
                rounded-md shadow-sm
                disabled:opacity-50 disabled:cursor-not-allowed
                focus:outline-none focus:ring-2 focus:ring-blue-500 focus:ring-offset-2
                transition-colors
              "
            >
              {isPending ? (
                <>
                  {/* Spinner */}
                  <svg
                    className="animate-spin w-4 h-4 text-white"
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
                      d="M4 12a8 8 0 018-8v8H4z"
                    />
                  </svg>
                  Procesando…
                </>
              ) : (
                <>
                  <svg
                    xmlns="http://www.w3.org/2000/svg"
                    className="w-4 h-4"
                    viewBox="0 0 24 24"
                    fill="none"
                    stroke="currentColor"
                    strokeWidth={2}
                    strokeLinecap="round"
                    strokeLinejoin="round"
                  >
                    <polyline points="20 6 9 17 4 12" />
                  </svg>
                  Confirmar Amarre
                </>
              )}
            </button>
          </div>
        </div>
      </div>
    </div>
  );

  return createPortal(modalContent, document.body);
}
