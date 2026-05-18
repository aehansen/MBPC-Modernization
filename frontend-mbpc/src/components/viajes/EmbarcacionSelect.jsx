import { useEffect, useState } from "react";
import { useEmbarcacionesAutocomplete } from "@/hooks/useEmbarcacionesAutocomplete";

/**
 * Ejemplo de selector con autocompletado según tipo de embarcación (buque / barcaza / remolcador).
 * Integrar en formularios reemplazando el `console.log` por la mutación o `setValue` de RHF.
 */
export default function EmbarcacionSelect({ onSelect }) {
  const [tipo, setTipo] = useState("buque");
  const [term, setTerm] = useState("");
  const { items, isLoading, error, fetchSuggestions, clear } = useEmbarcacionesAutocomplete(tipo);

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

  return (
    <div className="max-w-md space-y-3 rounded-lg border border-slate-600 bg-slate-900 p-4 text-slate-100">
      <h3 className="text-sm font-semibold text-cyan-400 uppercase tracking-wide">
        Embarcación (ejemplo Hito 10.2)
      </h3>

      <div className="flex flex-wrap gap-2">
        {[
          { value: "buque", label: "Buque" },
          { value: "barcaza", label: "Barcaza" },
          { value: "remolcador", label: "Remolcador" },
        ].map((opt) => (
          <button
            key={opt.value}
            type="button"
            onClick={() => {
              setTipo(opt.value);
              setTerm("");
              clear();
            }}
            className={`rounded-md px-3 py-1.5 text-xs font-semibold transition-colors ${
              tipo === opt.value
                ? "bg-cyan-600 text-white"
                : "bg-slate-800 text-slate-300 hover:bg-slate-700"
            }`}
          >
            {opt.label}
          </button>
        ))}
      </div>

      <div>
        <label htmlFor="emb-busqueda" className="mb-1 block text-xs font-medium text-slate-400">
          Buscar (mín. 2 caracteres)
        </label>
        <input
          id="emb-busqueda"
          type="text"
          value={term}
          onChange={(e) => setTerm(e.target.value)}
          placeholder="Nombre, matrícula u OMI…"
          autoComplete="off"
          className="w-full rounded-md border border-slate-600 bg-slate-800 px-3 py-2 text-sm text-slate-100 placeholder:text-slate-500 focus:border-cyan-500 focus:outline-none focus:ring-1 focus:ring-cyan-500"
        />
      </div>

      {isLoading && <p className="text-xs text-slate-400">Buscando…</p>}
      {error && <p className="text-xs text-red-400">{error}</p>}

      <ul className="max-h-48 space-y-1 overflow-y-auto rounded-md border border-slate-700 bg-slate-950/50 p-2">
        {items.length === 0 && !isLoading && term.trim().length >= 2 && (
          <li className="text-xs text-slate-500">Sin resultados.</li>
        )}
        {items.map((b) => (
          <li key={`${b.idBuque}-${b.nombre}`}>
            <button
              type="button"
              onClick={() => {
                onSelect?.(b);
                console.log("Seleccionado:", b);
              }}
              className="w-full rounded px-2 py-1.5 text-left text-sm hover:bg-slate-800"
            >
              <span className="font-medium text-slate-100">{b.nombre}</span>
              <span className="ml-2 text-xs text-slate-400">
                {b.tipo} · {b.matricula || "—"} · OMI {b.omi || "—"}
              </span>
            </button>
          </li>
        ))}
      </ul>
    </div>
  );
}
