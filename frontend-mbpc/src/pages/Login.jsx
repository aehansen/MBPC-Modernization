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

// ── Escudo SVG de la Prefectura Naval Argentina ───────────────────────────────
// Representación esquemática para uso en interfaz digital.
// No reproduce artwork oficial con fines de lucro.
const EscudoPNA = ({ size = 72 }) => (
  <svg
    width={size}
    height={size}
    viewBox="0 0 80 80"
    aria-label="Escudo Prefectura Naval Argentina"
    role="img"
  >
    {/* Base: círculo exterior dorado */}
    <circle cx="40" cy="40" r="39" fill="#0a1628" stroke="#C8A84B" strokeWidth="2.5" />
    {/* Fondo interior azul profundo */}
    <circle cx="40" cy="40" r="33" fill="#0d1f3c" />
    {/* Ancla vertical */}
    <line x1="40" y1="16" x2="40" y2="58" stroke="#C8A84B" strokeWidth="2.8" strokeLinecap="round" />
    {/* Travesaño */}
    <line x1="27" y1="25" x2="53" y2="25" stroke="#C8A84B" strokeWidth="2.4" strokeLinecap="round" />
    {/* Argolla superior */}
    <circle cx="40" cy="18" r="3.5" fill="none" stroke="#C8A84B" strokeWidth="2.2" />
    {/* Brazos del ancla */}
    <path d="M27 52 Q25 60 34 62" fill="none" stroke="#C8A84B" strokeWidth="2.2" strokeLinecap="round" />
    <path d="M53 52 Q55 60 46 62" fill="none" stroke="#C8A84B" strokeWidth="2.2" strokeLinecap="round" />
    {/* Olas decorativas */}
    <path
      d="M20 70 Q26 66 32 70 Q38 74 44 70 Q50 66 56 70 Q60 72 60 70"
      fill="none"
      stroke="#C8A84B"
      strokeWidth="1.6"
      strokeLinecap="round"
    />
    {/* Segunda ola */}
    <path
      d="M22 75 Q28 71 34 75 Q40 79 46 75 Q52 71 58 75"
      fill="none"
      stroke="#C8A84B"
      strokeWidth="1"
      strokeLinecap="round"
      opacity="0.5"
    />
    {/* Estrella en la parte superior */}
    <polygon
      points="40,8 41.5,12.5 46,12.5 42.5,15 43.8,19.5 40,17 36.2,19.5 37.5,15 34,12.5 38.5,12.5"
      fill="#C8A84B"
      opacity="0.9"
    />
  </svg>
);

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
    <div className="min-h-screen bg-slate-900 flex items-center justify-center px-4 relative overflow-hidden">

      {/* ── Fondo: gradiente radial + patrón de puntos ── */}
      <div
        className="absolute inset-0 pointer-events-none"
        aria-hidden="true"
        style={{
          background:
            "radial-gradient(ellipse 80% 60% at 50% -10%, rgba(14,30,70,0.9) 0%, transparent 70%), " +
            "radial-gradient(ellipse 60% 40% at 80% 110%, rgba(10,22,50,0.7) 0%, transparent 65%)",
        }}
      />

      {/* Patrón de grilla sutil */}
      <div
        className="absolute inset-0 pointer-events-none opacity-[0.03]"
        aria-hidden="true"
        style={{
          backgroundImage:
            "linear-gradient(rgba(200,168,75,1) 1px, transparent 1px), " +
            "linear-gradient(90deg, rgba(200,168,75,1) 1px, transparent 1px)",
          backgroundSize: "40px 40px",
        }}
      />

      {/* Línea de acento superior */}
      <div
        className="absolute top-0 left-0 right-0 h-[2px] pointer-events-none"
        aria-hidden="true"
        style={{
          background:
            "linear-gradient(90deg, transparent 0%, #C8A84B 30%, #e8c86b 50%, #C8A84B 70%, transparent 100%)",
        }}
      />

      {/* ── Olas decorativas inferiores ── */}
      <div className="absolute bottom-0 left-0 w-full pointer-events-none" aria-hidden="true">
        <svg viewBox="0 0 1440 130" preserveAspectRatio="none" className="w-full opacity-[0.06]">
          <path
            d="M0,80 C240,130 480,30 720,80 C960,130 1200,30 1440,80 L1440,130 L0,130 Z"
            fill="#C8A84B"
          />
        </svg>
        <svg viewBox="0 0 1440 100" preserveAspectRatio="none"
          className="w-full opacity-[0.04] absolute bottom-0">
          <path
            d="M0,60 C360,20 720,100 1080,60 C1260,40 1380,80 1440,60 L1440,100 L0,100 Z"
            fill="#ffffff"
          />
        </svg>
      </div>

      {/* ── Card principal ── */}
      <div className="relative w-full max-w-[420px]">

        {/* Borde luminoso decorativo */}
        <div
          className="absolute -inset-[1px] rounded-2xl pointer-events-none"
          aria-hidden="true"
          style={{
            background:
              "linear-gradient(135deg, rgba(200,168,75,0.35) 0%, rgba(200,168,75,0.05) 40%, rgba(200,168,75,0.05) 60%, rgba(200,168,75,0.2) 100%)",
            borderRadius: "inherit",
          }}
        />

        <div className="relative bg-slate-900/90 backdrop-blur-xl border border-white/[0.06] rounded-2xl shadow-2xl overflow-hidden">

          {/* Banda superior dorada */}
          <div className="h-[3px] w-full bg-gradient-to-r from-transparent via-[#C8A84B] to-transparent" />

          <div className="px-8 py-9">

            {/* ── Encabezado institucional ── */}
            <header className="flex flex-col items-center gap-4 mb-9">
              <EscudoPNA size={72} />

              <div className="text-center space-y-0.5">
                <p className="text-[#C8A84B] text-[10px] font-bold tracking-[0.3em] uppercase">
                  República Argentina
                </p>
                <h1 className="text-white text-[1.05rem] font-bold tracking-wide leading-snug mt-1">
                  Prefectura Naval Argentina
                </h1>
                <p className="text-slate-400 text-xs tracking-wide mt-1">
                  Sistema MBPC Geo — Control de Tráfico Fluvial
                </p>
              </div>

              {/* Separador */}
              <div className="flex items-center gap-3 w-full mt-1">
                <div className="flex-1 h-px bg-gradient-to-r from-transparent to-white/10" />
                <span className="text-white/20 text-[10px] tracking-[0.2em] uppercase font-medium">
                  Acceso Operativo
                </span>
                <div className="flex-1 h-px bg-gradient-to-l from-transparent to-white/10" />
              </div>
            </header>

            {/* ── Formulario ── */}
            <form onSubmit={handleSubmit} noValidate className="space-y-5">

              {/* Campo: Costera / Sección */}
              <div>
                <label
                  htmlFor={costeraFieldId}
                  className="block text-slate-300 text-[11px] font-semibold tracking-widest uppercase mb-2"
                >
                  Costera / Sección
                </label>
                <div className="relative">
                  {/* Ícono de ancla / ubicación */}
                  <span className="absolute left-3 top-1/2 -translate-y-1/2 text-slate-500 pointer-events-none">
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
                      bg-slate-800/70 border border-slate-700/80 rounded-lg
                      pl-10 pr-9 py-2.5
                      text-sm text-white
                      focus:outline-none focus:ring-2 focus:ring-[#C8A84B]/60 focus:border-[#C8A84B]/60
                      disabled:opacity-50 disabled:cursor-not-allowed
                      transition duration-150
                    "
                    style={{ colorScheme: "dark" }}
                  >
                    {/* Opción placeholder */}
                    <option value="" disabled className="bg-slate-800 text-slate-400">
                      — Seleccioná tu sección —
                    </option>

                    {/* ── Super Admin: inyectado manualmente, value=0 ── */}
                    <option value={0} className="bg-slate-800 font-semibold text-amber-400">
                      🏢 DIRECCIÓN DE TRÁFICO MARÍTIMO (ADMIN)
                    </option>

                    {/* ── Costeras desde el catálogo importado ── */}
                    {COSTERAS.map(({ id, etiqueta }) => (
                      <option key={id} value={id} className="bg-slate-800 text-white">
                        {etiqueta}
                      </option>
                    ))}
                  </select>

                  {/* Chevron decorativo */}
                  <span className="absolute right-3 top-1/2 -translate-y-1/2 text-slate-500">
                    <IconChevron />
                  </span>
                </div>
              </div>

              {/* Campo: Contraseña */}
              <div>
                <label
                  htmlFor={passwordFieldId}
                  className="block text-slate-300 text-[11px] font-semibold tracking-widest uppercase mb-2"
                >
                  Contraseña Operativa
                </label>
                <div className="relative">
                  <span className="absolute left-3 top-1/2 -translate-y-1/2 text-slate-500 pointer-events-none">
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
                      w-full bg-slate-800/70 border border-slate-700/80 rounded-lg
                      pl-10 pr-10 py-2.5
                      text-sm text-white placeholder-slate-600
                      focus:outline-none focus:ring-2 focus:ring-[#C8A84B]/60 focus:border-[#C8A84B]/60
                      disabled:opacity-50 disabled:cursor-not-allowed
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
                      text-slate-500 hover:text-slate-300
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
                    bg-red-950/60 border border-red-500/30 rounded-lg
                    px-4 py-3
                  "
                >
                  <span className="text-red-400 mt-0.5">
                    <IconAlert />
                  </span>
                  <p className="text-red-300 text-sm leading-snug">{error}</p>
                </div>
              )}

              {/* Botón de submit */}
              <button
                type="submit"
                disabled={loading}
                className="
                  w-full mt-1
                  bg-[#C8A84B] hover:bg-[#d4b45a] active:bg-[#b8943c]
                  text-slate-900 font-bold text-sm tracking-wide
                  py-3 rounded-lg
                  flex items-center justify-center gap-2
                  transition-all duration-150 ease-in-out
                  disabled:opacity-60 disabled:cursor-not-allowed
                  shadow-lg shadow-amber-900/20
                  focus:outline-none focus:ring-2 focus:ring-[#C8A84B] focus:ring-offset-2 focus:ring-offset-slate-900
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
            <footer className="mt-7 pt-5 border-t border-white/[0.06] text-center space-y-1">
              <p className="text-slate-600 text-[11px]">
                Acceso restringido a personal autorizado · PNA © {new Date().getFullYear()}
              </p>
              <p className="text-slate-700 text-[10px]">
                MBPC Geo v2 — Dirección de Modernización Digital
              </p>
            </footer>
          </div>
        </div>
      </div>
    </div>
  );
}
