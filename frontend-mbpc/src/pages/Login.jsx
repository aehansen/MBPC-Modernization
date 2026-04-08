// src/pages/Login.jsx
// ──────────────────────────────────────────────────────────────────────────────
// Pantalla de autenticación — Sistema MBPC Geo / Prefectura Naval Argentina
//
// Flujo de autenticación (Stateless / JWT):
//  1. El operador selecciona la Costera/Sección desde el <select>.
//  2. Ingresa su contraseña operativa.
//  3. POST /api/auth/login  →  { costeraId: Number(id), password }
//  4. Si OK: token guardado en localStorage('mbpc_token') + redirect a '/'.
//  5. Si falla: banner de error con mensaje contextual por código HTTP.
//
// Dependencias esperadas:
//  - ../constants/costeras  →  export const COSTERAS = [{ id, etiqueta }]
//  - ../services/apiClient  →  instancia de Axios preconfigurada
// ──────────────────────────────────────────────────────────────────────────────

import { useState, useId } from "react";
import { COSTERAS } from "../constants/costeras";
import apiClient from "../services/apiClient";

// ── Íconos inline ─────────────────────────────────────────────────────────────
const IconLock = () => (
  <svg className="w-4 h-4" fill="none" stroke="currentColor" strokeWidth={2} viewBox="0 0 24 24">
    <path strokeLinecap="round" strokeLinejoin="round"
      d="M16.5 10.5V6.75a4.5 4.5 0 1 0-9 0v3.75m-.75 11.25h10.5a2.25 2.25 0 0 0
         2.25-2.25v-6.75a2.25 2.25 0 0 0-2.25-2.25H6.75a2.25 2.25 0 0 0-2.25
         2.25v6.75a2.25 2.25 0 0 0 2.25 2.25Z" />
  </svg>
);

const IconChevron = () => (
  <svg className="w-4 h-4 pointer-events-none" fill="none" stroke="currentColor" strokeWidth={2} viewBox="0 0 24 24">
    <path strokeLinecap="round" strokeLinejoin="round" d="m19.5 8.25-7.5 7.5-7.5-7.5" />
  </svg>
);

const IconAlert = () => (
  <svg className="w-4 h-4 shrink-0 mt-0.5" fill="none" stroke="currentColor" strokeWidth={2} viewBox="0 0 24 24">
    <path strokeLinecap="round" strokeLinejoin="round"
      d="M12 9v3.75m-9.303 3.376c-.866 1.5.217 3.374 1.948 3.374h14.71c1.73 0
         2.813-1.874 1.948-3.374L13.949 3.378c-.866-1.5-3.032-1.5-3.898
         0L2.697 16.126ZM12 15.75h.007v.008H12v-.008Z" />
  </svg>
);

const IconSpinner = () => (
  <svg className="animate-spin w-4 h-4" fill="none" viewBox="0 0 24 24">
    <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" />
    <path className="opacity-75" fill="currentColor"
      d="M4 12a8 8 0 0 1 8-8V0C5.373 0 0 5.373 0 12h4Z" />
  </svg>
);

const IconEye = ({ open }) =>
  open ? (
    <svg className="w-4 h-4" fill="none" stroke="currentColor" strokeWidth={2} viewBox="0 0 24 24">
      <path strokeLinecap="round" strokeLinejoin="round"
        d="M2.036 12.322a1.012 1.012 0 0 1 0-.639C3.423 7.51 7.36 4.5 12 4.5c4.638
           0 8.573 3.007 9.963 7.178.07.207.07.431 0 .639C20.577 16.49 16.64 19.5
           12 19.5c-4.638 0-8.573-3.007-9.963-7.178Z" />
      <path strokeLinecap="round" strokeLinejoin="round" d="M15 12a3 3 0 1 1-6 0 3 3 0 0 1 6 0Z" />
    </svg>
  ) : (
    <svg className="w-4 h-4" fill="none" stroke="currentColor" strokeWidth={2} viewBox="0 0 24 24">
      <path strokeLinecap="round" strokeLinejoin="round"
        d="M3.98 8.223A10.477 10.477 0 0 0 1.934 12C3.226 16.338 7.244 19.5 12
           19.5c.993 0 1.953-.138 2.863-.395M6.228 6.228A10.451 10.451 0 0 1 12
           4.5c4.756 0 8.773 3.162 10.065 7.498a10.522 10.522 0 0 1-4.293
           5.774M6.228 6.228 3 3m3.228 3.228 3.65 3.65m7.894 7.894L21 21m-3.228-3.228-3.65-3.65m0
           0a3 3 0 1 0-4.243-4.243m4.242 4.242L9.88 9.88" />
    </svg>
  );

// ── Mapeo de errores HTTP a mensajes operativos ───────────────────────────────
const resolveError = (err) => {
  const status = err?.response?.status;
  if (status === 401) return "Contraseña incorrecta. Verificá tus credenciales e intentá nuevamente.";
  if (status === 403) return "Sin autorización para acceder a esa Costera. Contactá al administrador.";
  if (status === 404) return "Costera no encontrada. Seleccioná una sección válida.";
  if (status >= 500) return "El servidor no está disponible. Intentá en unos minutos o contactá a soporte.";
  if (!navigator.onLine) return "Sin conexión a la red. Verificá tu acceso a la intranet PNA.";
  return "No se pudo establecer conexión con el servidor MBPC. Intentá nuevamente.";
};

// ── Componente principal ──────────────────────────────────────────────────────
export default function Login() {
  // IDs accesibles para los labels
  const costeraFieldId  = useId();
  const passwordFieldId = useId();

  const [costeraId, setCosteraId]     = useState("");
  const [password, setPassword]       = useState("");
  const [showPassword, setShowPassword] = useState(false);
  const [loading, setLoading]         = useState(false);
  const [error, setError]             = useState(null);

  // Limpiar error al modificar cualquier campo
  const clearError = () => { if (error) setError(null); };

  const handleSubmit = async (e) => {
    e.preventDefault();

    // ── Validación client-side ────────────────────────────────────────────────
    if (!costeraId && costeraId !== 0) {
      setError("Seleccioná una Costera o sección antes de continuar.");
      return;
    }
    if (!password.trim()) {
      setError("Ingresá tu contraseña operativa.");
      return;
    }

    setLoading(true);
    setError(null);

    try {
      const { data } = await apiClient.post("/api/auth/login", {
        costeraId: Number(costeraId),
        password,
      });

      // Persistir JWT — el interceptor de Axios lo leerá en cada request
      localStorage.setItem("mbpc_token", data.token);

      // Redirigir al dashboard principal (hard redirect para flush de estado)
      window.location.href = "/";
    } catch (err) {
      setError(resolveError(err));
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="min-h-screen bg-gray-50 flex items-center justify-center px-4 relative overflow-hidden">

      {/* ── Fondo: patrón de puntos sutil ── */}
      <div
        className="absolute inset-0 pointer-events-none opacity-[0.05]"
        aria-hidden="true"
        style={{
          backgroundImage:
            "linear-gradient(#002454 1px, transparent 1px), " +
            "linear-gradient(90deg, #002454 1px, transparent 1px)",
          backgroundSize: "40px 40px",
        }}
      />

      {/* ── Card principal ── */}
      <div className="relative w-full max-w-[420px]">
        <div className="relative bg-white border border-gray-200 rounded-2xl shadow-2xl overflow-hidden">

          {/* Banda superior azul marino institucional */}
          <div className="h-[4px] w-full bg-[#002454]" />

          <div className="px-8 py-9">

            {/* ── Encabezado institucional ── */}
            <header className="flex flex-col items-center gap-4 mb-9">
              {/* Escudo Oficial PNA desde URL */}
              <img 
                src="https://www.argentina.gob.ar/sites/default/files/styles/isotipo/public/imagenEncabezado/prefectura-escudo.png?itok=EywBfOaV" 
                alt="Escudo Prefectura Naval Argentina" 
                className="h-[72px] w-auto object-contain"
              />

              <div className="text-center space-y-0.5">
                <p className="text-gray-500 text-[10px] font-bold tracking-[0.3em] uppercase">
                  República Argentina
                </p>
                <h1 className="text-[#002454] text-[1.05rem] font-bold tracking-wide leading-snug mt-1">
                  Prefectura Naval Argentina
                </h1>
                <p className="text-gray-500 text-xs tracking-wide mt-1">
                  Sistema MBPC Geo H2
                </p>
              </div>

              {/* Separador */}
              <div className="flex items-center gap-3 w-full mt-1">
                <div className="flex-1 h-px bg-gray-200" />
                <span className="text-gray-400 text-[10px] tracking-[0.2em] uppercase font-bold">
                  Acceso Operativo
                </span>
                <div className="flex-1 h-px bg-gray-200" />
              </div>
            </header>

            {/* ── Formulario ── */}
            <form onSubmit={handleSubmit} noValidate className="space-y-5">

              {/* Campo: Costera / Sección */}
              <div>
                <label
                  htmlFor={costeraFieldId}
                  className="block text-gray-700 text-[11px] font-bold tracking-widest uppercase mb-2"
                >
                  Costera / Sección
                </label>
                <div className="relative">
                  {/* Ícono de ubicación */}
                  <span className="absolute left-3 top-1/2 -translate-y-1/2 text-gray-400 pointer-events-none">
                    <svg className="w-4 h-4" fill="none" stroke="currentColor" strokeWidth={1.8} viewBox="0 0 24 24">
                      <path strokeLinecap="round" strokeLinejoin="round"
                        d="M15 10.5a3 3 0 1 1-6 0 3 3 0 0 1 6 0Z" />
                      <path strokeLinecap="round" strokeLinejoin="round"
                        d="M19.5 10.5c0 7.142-7.5 11.25-7.5 11.25S4.5 17.642 4.5 10.5a7.5 7.5 0 1 1 15 0Z" />
                    </svg>
                  </span>

                  <select
                    id={costeraFieldId}
                    value={costeraId}
                    onChange={(e) => { setCosteraId(e.target.value); clearError(); }}
                    disabled={loading}
                    className="
                      w-full appearance-none
                      bg-white border border-gray-300 rounded-lg
                      pl-10 pr-9 py-2.5
                      text-sm text-gray-900
                      focus:outline-none focus:ring-2 focus:ring-[#104a8e] focus:border-transparent
                      disabled:opacity-50 disabled:bg-gray-50 disabled:cursor-not-allowed
                      transition duration-150
                    "
                  >
                    {/* Opción placeholder */}
                    <option value="" disabled className="text-gray-400">
                      — Seleccioná tu sección —
                    </option>

                    {/* ── Super Admin: inyectado manualmente, value=0 ── */}
                    <option value={0} className="font-semibold text-[#002454]">
                      🏢 DIRECCIÓN DE TRÁFICO MARÍTIMO (ADMIN)
                    </option>

                    {/* ── Costeras desde el catálogo importado ── */}
                    {COSTERAS.map(({ id, etiqueta }) => (
                      <option key={id} value={id}>
                        {etiqueta}
                      </option>
                    ))}
                  </select>

                  {/* Chevron decorativo */}
                  <span className="absolute right-3 top-1/2 -translate-y-1/2 text-gray-400">
                    <IconChevron />
                  </span>
                </div>
              </div>

              {/* Campo: Contraseña */}
              <div>
                <label
                  htmlFor={passwordFieldId}
                  className="block text-gray-700 text-[11px] font-bold tracking-widest uppercase mb-2"
                >
                  Contraseña Operativa
                </label>
                <div className="relative">
                  <span className="absolute left-3 top-1/2 -translate-y-1/2 text-gray-400 pointer-events-none">
                    <IconLock />
                  </span>
                  <input
                    id={passwordFieldId}
                    type={showPassword ? "text" : "password"}
                    autoComplete="current-password"
                    value={password}
                    onChange={(e) => { setPassword(e.target.value); clearError(); }}
                    placeholder="••••••••"
                    disabled={loading}
                    className="
                      w-full bg-white border border-gray-300 rounded-lg
                      pl-10 pr-10 py-2.5
                      text-sm text-gray-900 placeholder-gray-400
                      focus:outline-none focus:ring-2 focus:ring-[#104a8e] focus:border-transparent
                      disabled:opacity-50 disabled:bg-gray-50 disabled:cursor-not-allowed
                      transition duration-150
                    "
                  />
                  {/* Toggle visibilidad contraseña */}
                  <button
                    type="button"
                    aria-label={showPassword ? "Ocultar contraseña" : "Mostrar contraseña"}
                    onClick={() => setShowPassword((v) => !v)}
                    disabled={loading}
                    className="
                      absolute right-3 top-1/2 -translate-y-1/2
                      text-gray-400 hover:text-gray-600
                      transition-colors duration-150 disabled:pointer-events-none
                    "
                  >
                    <IconEye open={showPassword} />
                  </button>
                </div>
              </div>

              {/* Banner de error */}
              {error && (
                <div
                  role="alert"
                  aria-live="assertive"
                  className="
                    flex items-start gap-2.5
                    bg-red-50 border border-red-200 rounded-lg
                    px-4 py-3
                  "
                >
                  <span className="text-red-500 mt-0.5">
                    <IconAlert />
                  </span>
                  <p className="text-red-700 text-sm leading-snug">{error}</p>
                </div>
              )}

              {/* Botón de submit */}
              <button
                type="submit"
                disabled={loading}
                className="
                  w-full mt-1
                  bg-[#104a8e] hover:bg-[#002454] active:bg-[#001a3d]
                  text-white font-bold text-sm tracking-wide
                  py-3 rounded-lg
                  flex items-center justify-center gap-2
                  transition-all duration-150 ease-in-out
                  disabled:opacity-60 disabled:cursor-not-allowed
                  shadow-md shadow-blue-900/20
                  focus:outline-none focus:ring-2 focus:ring-[#104a8e] focus:ring-offset-2 focus:ring-offset-white
                "
              >
                {loading ? (
                  <>
                    <IconSpinner />
                    <span>Autenticando…</span>
                  </>
                ) : (
                  <>
                    <IconLock />
                    <span>Ingresar al Sistema</span>
                  </>
                )}
              </button>
            </form>

            {/* ── Footer ── */}
            <footer className="mt-7 pt-5 border-t border-gray-100 text-center space-y-1">
              <p className="text-gray-400 text-[11px]">
                Acceso restringido a personal autorizado · PNA © {new Date().getFullYear()}
              </p>
              <p className="text-gray-400 text-[10px]">
                MBPC Geo H2 — Dirección de Informática y Comunicaciones <br /> Divisón Sistemas de Información Geográfica
              </p>
            </footer>
          </div>
        </div>
      </div>
    </div>
  );
}