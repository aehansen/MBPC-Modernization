import { useState, useEffect, useRef, useCallback } from "react";
import { tipoCargaApi } from "../../axiosClient";

/**
 * TipoCargaAutocomplete
 *
 * Componente reutilizable de búsqueda de tipos de carga (naturaleza de mercadería).
 *
 * Props:
 *  @param {number|null}  value     – OracleId actualmente seleccionado.
 *  @param {Function}     onChange  – Callback: ({ oracleId, nombre }) => void
 *  @param {boolean}      error     – Si true, muestra borde y mensaje de error en rojo.
 *  @param {string}       [label]   – Etiqueta visible encima del input (opcional).
 *  @param {boolean}      [disabled]– Deshabilita el componente.
 */
export default function TipoCargaAutocomplete({
  value,
  onChange,
  error = false,
  label = "Tipo de Carga",
  disabled = false,
}) {
  const [inputValue, setInputValue] = useState("");
  const [opciones, setOpciones] = useState([]);
  const [cargando, setCargando] = useState(false);
  const [abierto, setAbierto] = useState(false);
  const [itemActivo, setItemActivo] = useState(-1);

  const debounceTimer = useRef(null);
  const wrapperRef = useRef(null);
  const inputRef = useRef(null);
  const listRef = useRef(null);

  // ── Cierra dropdown al hacer clic fuera ──────────────────────────────────
  useEffect(() => {
    const onClickOutside = (e) => {
      if (wrapperRef.current && !wrapperRef.current.contains(e.target)) {
        setAbierto(false);
      }
    };
    document.addEventListener("mousedown", onClickOutside);
    return () => document.removeEventListener("mousedown", onClickOutside);
  }, []);

  // ── Limpia debounce al desmontar ─────────────────────────────────────────
  useEffect(() => {
    return () => clearTimeout(debounceTimer.current);
  }, []);

  // ── Fetch a la API ────────────────────────────────────────────────────────
  const buscar = useCallback(async (query) => {
    if (!query || query.trim().length < 2) {
      setOpciones([]);
      setAbierto(false);
      return;
    }

    setCargando(true);
    try {
      const { data } = await tipoCargaApi.autocomplete(query.trim());
      setOpciones(Array.isArray(data) ? data : []);
      setAbierto(true);
      setItemActivo(-1);
    } catch (err) {
      console.error("[TipoCargaAutocomplete] Error al buscar:", err);
      setOpciones([]);
    } finally {
      setCargando(false);
    }
  }, []);

  // ── Cambio en el input ────────────────────────────────────────────────────
  const handleInputChange = (e) => {
    const val = e.target.value;
    setInputValue(val);

    if (!val) {
      setOpciones([]);
      setAbierto(false);
      onChange?.({ oracleId: null, nombre: "" });
    }

    clearTimeout(debounceTimer.current);
    debounceTimer.current = setTimeout(() => buscar(val), 350);
  };

  // ── Selección de un ítem ──────────────────────────────────────────────────
  const handleSeleccionar = (item) => {
    setInputValue(item.nombre);
    setOpciones([]);
    setAbierto(false);
    setItemActivo(-1);
    onChange?.({ oracleId: item.oracleId, nombre: item.nombre });
  };

  // ── Navegación con teclado ────────────────────────────────────────────────
  const handleKeyDown = (e) => {
    if (!abierto || opciones.length === 0) return;

    if (e.key === "ArrowDown") {
      e.preventDefault();
      setItemActivo((prev) => Math.min(prev + 1, opciones.length - 1));
    } else if (e.key === "ArrowUp") {
      e.preventDefault();
      setItemActivo((prev) => Math.max(prev - 1, 0));
    } else if (e.key === "Enter" && itemActivo >= 0) {
      e.preventDefault();
      handleSeleccionar(opciones[itemActivo]);
    } else if (e.key === "Escape") {
      setAbierto(false);
    }
  };

  // ── Clases del input ─────────────────────────────────────────────────────
  const inputBorderClass = error
    ? "border-red-500 focus:ring-red-400 focus:border-red-500"
    : "border-slate-300 focus:ring-sky-400 focus:border-sky-500";

  return (
    <div ref={wrapperRef} className="relative w-full">

      {/* Label */}
      {label && (
        <label className="mb-1 block text-xs font-semibold uppercase tracking-wide text-slate-500">
          {label}
        </label>
      )}

      {/* Input wrapper */}
      <div className="relative">
        <input
          ref={inputRef}
          type="text"
          value={inputValue}
          onChange={handleInputChange}
          onFocus={() => opciones.length > 0 && setAbierto(true)}
          onKeyDown={handleKeyDown}
          disabled={disabled}
          placeholder="Buscar por nombre o código..."
          autoComplete="off"
          className={`
            w-full rounded-lg border bg-white px-3 py-2 pr-10
            text-sm text-slate-800 placeholder:text-slate-400
            shadow-sm transition-colors
            focus:outline-none focus:ring-2
            disabled:cursor-not-allowed disabled:bg-slate-50 disabled:text-slate-400
            ${inputBorderClass}
          `}
        />

        {/* Icono derecho: spinner o lupa */}
        <span className="pointer-events-none absolute right-3 top-1/2 -translate-y-1/2 text-slate-400">
          {cargando ? (
            <svg
              className="h-4 w-4 animate-spin text-sky-500"
              xmlns="http://www.w3.org/2000/svg"
              fill="none"
              viewBox="0 0 24 24"
            >
              <circle
                className="opacity-25"
                cx="12" cy="12" r="10"
                stroke="currentColor" strokeWidth="4"
              />
              <path
                className="opacity-75"
                fill="currentColor"
                d="M4 12a8 8 0 018-8v8z"
              />
            </svg>
          ) : (
            <svg
              className="h-4 w-4"
              fill="none"
              stroke="currentColor"
              viewBox="0 0 24 24"
            >
              <path
                strokeLinecap="round"
                strokeLinejoin="round"
                strokeWidth={2}
                d="M21 21l-4.35-4.35M17 11A6 6 0 115 11a6 6 0 0112 0z"
              />
            </svg>
          )}
        </span>
      </div>

      {/* Mensaje de validación */}
      {error && (
        <p className="mt-1 flex items-center gap-1 text-xs text-red-500">
          <svg className="h-3.5 w-3.5 flex-shrink-0" fill="currentColor" viewBox="0 0 20 20">
            <path
              fillRule="evenodd"
              d="M18 10a8 8 0 11-16 0 8 8 0 0116 0zm-8-5a.75.75 0 01.75.75v4.5a.75.75 0 01-1.5 0v-4.5A.75.75 0 0110 5zm0 9a1 1 0 100-2 1 1 0 000 2z"
              clipRule="evenodd"
            />
          </svg>
          Seleccioná un tipo de carga válido.
        </p>
      )}

      {/* ── Dropdown ─────────────────────────────────────────────────────── */}
      {abierto && opciones.length > 0 && (
        <ul
          ref={listRef}
          className="absolute z-50 mt-1 w-full overflow-y-auto rounded-lg border border-slate-200 bg-white shadow-xl"
          style={{ maxHeight: "16rem" }}
          role="listbox"
        >
          {opciones.map((item, idx) => (
            <li
              key={item.oracleId}
              role="option"
              aria-selected={idx === itemActivo}
              onMouseDown={() => handleSeleccionar(item)}
              onMouseEnter={() => setItemActivo(idx)}
              className={`
                flex cursor-pointer items-center justify-between gap-3 px-3 py-2.5
                transition-colors
                ${idx === itemActivo ? "bg-sky-50" : "hover:bg-slate-50"}
                ${idx < opciones.length - 1 ? "border-b border-slate-100" : ""}
              `}
            >
              {/* Nombre + Código */}
              <div className="flex min-w-0 flex-col">
                <span className="truncate text-sm font-medium text-slate-800">
                  {item.nombre}
                </span>
                <span className="font-mono text-xs text-slate-400">
                  {item.codigo}
                </span>
              </div>

              {/* Badge "Peligrosa" */}
              {item.esPeligrosa && (
                <span className="inline-flex flex-shrink-0 items-center gap-1 rounded-full border border-red-200 bg-red-50 px-2 py-0.5 text-xs font-semibold text-red-600">
                  {/* Ícono triángulo advertencia */}
                  <svg
                    className="h-3 w-3"
                    fill="currentColor"
                    viewBox="0 0 20 20"
                  >
                    <path
                      fillRule="evenodd"
                      d="M8.485 2.495c.673-1.167 2.357-1.167 3.03 0l6.28 10.875c.673 1.167-.17 2.625-1.516 2.625H3.72c-1.347 0-2.189-1.458-1.515-2.625L8.485 2.495zM10 5a.75.75 0 01.75.75v3.5a.75.75 0 01-1.5 0v-3.5A.75.75 0 0110 5zm0 9a1 1 0 100-2 1 1 0 000 2z"
                      clipRule="evenodd"
                    />
                  </svg>
                  Peligrosa
                </span>
              )}
            </li>
          ))}
        </ul>
      )}

      {/* Sin resultados */}
      {abierto && !cargando && opciones.length === 0 && inputValue.trim().length >= 2 && (
        <div className="absolute z-50 mt-1 w-full rounded-lg border border-slate-200 bg-white px-3 py-3 shadow-xl">
          <p className="text-sm text-slate-500">
            Sin resultados para{" "}
            <span className="font-medium text-slate-700">"{inputValue}"</span>
          </p>
        </div>
      )}
    </div>
  );
}
