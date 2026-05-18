import { useEffect, useState } from "react";
import { useEmbarcacionesAutocomplete } from "@/hooks/useEmbarcacionesAutocomplete";

/**
 * Selector con autocompletado según tipo de embarcación (buque / barcaza / remolcador).
 *
 * Props:
 *  - onSelect(embarcacion)  → callback cuando el usuario elige un ítem de la lista.
 *  - error (string)         → mensaje de validación de react-hook-form; pinta bordes rojos.
 *  - disabled (boolean)     → bloquea el input mientras el formulario está en envío.
 *  - allowedTipos (string[]) → tipos de embarcación visibles en las pestañas.
 */
export default function EmbarcacionSelect({
  onSelect,
  error,
  disabled = false,
  allowedTipos = ["buque", "barcaza", "remolcador"],
}) {
  const [tipo, setTipo] = useState(allowedTipos[0] || "buque");
  const [term, setTerm] = useState("");
  const [isOpen, setIsOpen] = useState(false);
  const { items, isLoading, error: fetchError, fetchSuggestions, clear } =
    useEmbarcacionesAutocomplete(tipo);

  useEffect(() => {
    const q = term.trim();
    if (q.length < 2) {
      clear();
      return;
    }
    const id = setTimeout(() => {
      void fetchSuggestions(q);
    }, 400);
    return () => clearTimeout(id);
  }, [term, tipo, fetchSuggestions, clear]);

  // Clases del input según estado (error / normal / disabled)
  const inputBase =
    "w-full rounded-md px-3 py-2 text-sm text-slate-100 placeholder:text-slate-500 " +
    "focus:outline-none focus:ring-1 transition-colors duration-150 " +
    "disabled:opacity-50 disabled:cursor-not-allowed ";

  const inputIdle =
    inputBase +
    "border border-slate-600 bg-slate-800 focus:border-cyan-500 focus:ring-cyan-500";

  const inputError =
    inputBase +
    "border border-red-500/70 bg-slate-800 focus:border-red-400 focus:ring-red-400/40";

  const tabs = [
    { value: "buque", label: "Buque" },
    { value: "barcaza", label: "Barcaza" },
    { value: "remolcador", label: "Remolcador" },
  ].filter((opt) => allowedTipos.includes(opt.value));

  return (
    <div className="space-y-3 rounded-lg border border-slate-700/60 bg-slate-800/40 p-4">
      {/* Selector de tipo */}
      <div className="flex flex-wrap gap-2">
        {tabs.map((opt) => (
          <button
            key={opt.value}
            type="button"
            disabled={disabled}
            onClick={() => {
              setTipo(opt.value);
              setTerm("");
              clear();
            }}
            className={`rounded-md px-3 py-1.5 text-xs font-semibold transition-colors
              disabled:opacity-50 disabled:cursor-not-allowed ${
                tipo === opt.value
                  ? "bg-cyan-600 text-white"
                  : "bg-slate-700 text-slate-300 hover:bg-slate-600"
              }`}
          >
            {opt.label}
          </button>
        ))}
      </div>

      {/* Input de búsqueda */}
      <div>
        <label
          htmlFor="emb-busqueda"
          className="mb-1 block text-xs font-medium text-slate-400"
        >
          Buscar (mín. 2 caracteres)
        </label>
        <input
          id="emb-busqueda"
          type="text"
          value={term}
          onChange={(e) => {
            setTerm(e.target.value);
            setIsOpen(true);
          }}
          onFocus={() => {
            if (term.trim().length >= 2) setIsOpen(true);
          }}
          placeholder="Nombre, matrícula u OMI…"
          autoComplete="off"
          disabled={disabled}
          className={error ? inputError : inputIdle}
          aria-invalid={!!error}
          aria-describedby={error ? "emb-error" : undefined}
        />
      </div>

      {/* Mensaje de error de validación (RHF) */}
      {error && (
        <p id="emb-error" className="mt-1 text-xs text-red-400 flex items-center gap-1">
          <span aria-hidden="true">⚠</span>
          {error}
        </p>
      )}

      {/* Estado de carga / error de fetch */}
      {isLoading && <p className="text-xs text-slate-400">Buscando…</p>}
      {fetchError && !isLoading && (
        <p className="text-xs text-red-400">{fetchError}</p>
      )}

      {/* Lista de resultados */}
      {isOpen && term.trim().length >= 2 && !isLoading && (
        <ul className="max-h-48 space-y-1 overflow-y-auto rounded-md border border-slate-700 bg-slate-950/50 p-2 scrollbar-thin scrollbar-track-slate-800 scrollbar-thumb-slate-600">
          {items.length === 0 ? (
            <li className="text-xs text-slate-500">Sin resultados.</li>
          ) : (
            items.map((b) => (
              <li key={`${b.idBuque}-${b.nombre}`}>
                <button
                  type="button"
                  disabled={disabled}
                  onClick={() => {
                    onSelect?.(b);
                    setTerm(b.nombre);
                    setIsOpen(false);
                    clear();
                  }}
                  className="w-full rounded px-2 py-1.5 text-left text-sm
                             hover:bg-slate-700 transition-colors duration-100
                             disabled:opacity-50 disabled:cursor-not-allowed"
                >
                  <span className="font-medium text-slate-100">{b.nombre}</span>
                  <span className="ml-2 text-xs text-slate-400">
                    {b.tipo} · {b.matricula || "—"} · OMI {b.omi || "—"}
                  </span>
                </button>
              </li>
            ))
          )}
        </ul>
      )}
    </div>
  );
}
