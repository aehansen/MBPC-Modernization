// src/components/Navbar.jsx
// ──────────────────────────────────────────────────────────────────────────────
// Navbar fijo del sistema MBPC Geo / Prefectura Naval Argentina
//
// Características:
//  - Header sticky, oscuro (slate-900), minimalista y tecnológico.
//  - Muestra: logo/título del sistema · indicador de estado · botón Logout.
//  - Logout STATELESS: solo limpia localStorage y redirige a /login.
//    No realiza ningún llamado a la API (arquitectura JWT sin invalidación
//    server-side en esta fase; el token expira por TTL en el backend).
//
// Props: ninguna — la Navbar lee contexto propio del sistema.
// ──────────────────────────────────────────────────────────────────────────────

import { useState, useEffect } from "react";

// ── Íconos inline ─────────────────────────────────────────────────────────────

const IconAnchor = () => (
  <svg className="w-5 h-5" fill="none" stroke="currentColor" strokeWidth={1.8} viewBox="0 0 24 24"
    aria-hidden="true">
    {/* Ancla estilizada */}
    <circle cx="12" cy="5" r="2" />
    <line x1="12" y1="7" x2="12" y2="19" strokeLinecap="round" />
    <line x1="7" y1="10" x2="17" y2="10" strokeLinecap="round" />
    <path d="M7 19 Q7 22 10 22" strokeLinecap="round" />
    <path d="M17 19 Q17 22 14 22" strokeLinecap="round" />
  </svg>
);

const IconLogout = () => (
  <svg className="w-4 h-4" fill="none" stroke="currentColor" strokeWidth={2} viewBox="0 0 24 24"
    aria-hidden="true">
    <path strokeLinecap="round" strokeLinejoin="round"
      d="M15.75 9V5.25A2.25 2.25 0 0 0 13.5 3h-6a2.25 2.25 0 0 0-2.25 2.25v13.5A2.25
         2.25 0 0 0 7.5 21h6a2.25 2.25 0 0 0 2.25-2.25V15M12 9l-3 3m0 0 3 3m-3-3h12.75" />
  </svg>
);

const IconSignal = () => (
  <svg className="w-3 h-3" fill="currentColor" viewBox="0 0 24 24" aria-hidden="true">
    <circle cx="12" cy="12" r="4" />
  </svg>
);

// ── Helpers ───────────────────────────────────────────────────────────────────

/**
 * Recupera el nombre de usuario guardado por el Login.
 * Retorna null si no está disponible (sesión sin nombre persistido).
 */
const getStoredUsername = () => {
  try {
    return localStorage.getItem("mbpc_usuario") || null;
  } catch {
    return null;
  }
};

/**
 * Recupera la costera activa de la sesión.
 */
const getStoredCostera = () => {
  try {
    return localStorage.getItem("mbpc_costera") || null;
  } catch {
    return null;
  }
};

// ── Componente principal ──────────────────────────────────────────────────────
export default function Navbar() {
  const [usuario, setUsuario]   = useState(null);
  const [costera, setCostera]   = useState(null);
  const [isOnline, setIsOnline] = useState(navigator.onLine);
  const [logoutHover, setLogoutHover] = useState(false);

  // Leer datos de sesión al montar
  useEffect(() => {
    setUsuario(getStoredUsername());
    setCostera(getStoredCostera());
  }, []);

  // Suscribir a cambios de conectividad
  useEffect(() => {
    const handleOnline  = () => setIsOnline(true);
    const handleOffline = () => setIsOnline(false);
    window.addEventListener("online",  handleOnline);
    window.addEventListener("offline", handleOffline);
    return () => {
      window.removeEventListener("online",  handleOnline);
      window.removeEventListener("offline", handleOffline);
    };
  }, []);

  // ── Handler de Logout (Stateless) ──────────────────────────────────────────
  // En arquitectura JWT sin denylist, el logout es puramente client-side.
  // El token en el servidor expira por su propio TTL (exp claim).
  const handleLogout = () => {
    try {
      localStorage.removeItem("mbpc_token");
      localStorage.removeItem("mbpc_usuario");
      localStorage.removeItem("mbpc_costera");
    } catch {
      // Si localStorage falla (modo privado estricto), continuar de todas formas
    }
    window.location.href = "/login";
  };

  return (
    <header
      className="
        sticky top-0 z-50
        bg-slate-900/95 backdrop-blur-md
        border-b border-white/[0.06]
        shadow-[0_1px_20px_rgba(0,0,0,0.5)]
      "
      role="banner"
    >
      {/* Línea de acento superior */}
      <div
        className="h-[2px] w-full"
        aria-hidden="true"
        style={{
          background:
            "linear-gradient(90deg, transparent 0%, #C8A84B 25%, #e8c86b 50%, #C8A84B 75%, transparent 100%)",
        }}
      />

      <div className="max-w-screen-2xl mx-auto px-4 sm:px-6 lg:px-8">
        <div className="flex items-center justify-between h-14">

          {/* ── Lado izquierdo: Logo + Título ── */}
          <div className="flex items-center gap-3 min-w-0">
            {/* Ícono de ancla */}
            <div
              className="flex items-center justify-center w-8 h-8 rounded-md shrink-0"
              style={{ background: "rgba(200,168,75,0.12)", border: "1px solid rgba(200,168,75,0.25)" }}
              aria-hidden="true"
            >
              <span className="text-[#C8A84B]">
                <IconAnchor />
              </span>
            </div>

            {/* Título del sistema */}
            <div className="min-w-0">
              <div className="flex items-baseline gap-2 flex-wrap">
                <span
                  className="text-white font-bold text-base tracking-wide leading-none"
                  style={{ fontVariantNumeric: "tabular-nums" }}
                >
                  MBPC
                </span>
                <span className="text-[#C8A84B] font-semibold text-base leading-none">
                  Geo
                </span>
                {/* Separador vertical */}
                <span className="hidden sm:inline text-white/10 text-sm select-none">|</span>
                {/* Subtítulo sección activa — oculto en mobile */}
                <span className="hidden sm:inline text-slate-500 text-xs tracking-wide truncate max-w-[200px]">
                  {costera ? costera : "Control de Tráfico Fluvial"}
                </span>
              </div>
            </div>
          </div>

          {/* ── Lado derecho: Status + Usuario + Logout ── */}
          <div className="flex items-center gap-3 sm:gap-4 shrink-0">

            {/* Indicador de conectividad */}
            <div
              className="hidden xs:flex items-center gap-1.5 px-2.5 py-1 rounded-full"
              style={{
                background: isOnline
                  ? "rgba(34,197,94,0.08)"
                  : "rgba(239,68,68,0.08)",
                border: `1px solid ${isOnline ? "rgba(34,197,94,0.2)" : "rgba(239,68,68,0.2)"}`,
              }}
              title={isOnline ? "Conectado a la red PNA" : "Sin conexión"}
              aria-label={`Estado: ${isOnline ? "En línea" : "Sin conexión"}`}
            >
              {/* Punto pulsante */}
              <span
                className={`relative flex h-2 w-2 ${isOnline ? "text-green-400" : "text-red-400"}`}
                aria-hidden="true"
              >
                {isOnline && (
                  <span
                    className="animate-ping absolute inline-flex h-full w-full rounded-full bg-green-400 opacity-50"
                  />
                )}
                <IconSignal />
              </span>
              <span
                className={`text-[11px] font-semibold tracking-wide ${
                  isOnline ? "text-green-400" : "text-red-400"
                }`}
              >
                {isOnline ? "En Línea" : "Sin Red"}
              </span>
            </div>

            {/* Separador */}
            <div className="hidden sm:block h-5 w-px bg-white/10" aria-hidden="true" />

            {/* Nombre de usuario (si está disponible) */}
            {usuario && (
              <div className="hidden md:flex flex-col items-end leading-none gap-0.5">
                <span className="text-white text-xs font-semibold truncate max-w-[120px]">
                  {usuario}
                </span>
                <span className="text-slate-600 text-[10px] tracking-wide">Operador</span>
              </div>
            )}

            {/* ── Botón de Logout (Stateless) ── */}
            <button
              type="button"
              onClick={handleLogout}
              onMouseEnter={() => setLogoutHover(true)}
              onMouseLeave={() => setLogoutHover(false)}
              aria-label="Cerrar sesión y volver al login"
              className="
                flex items-center gap-2
                px-3 py-1.5 rounded-md
                border border-slate-700/80 hover:border-red-500/40
                bg-slate-800/60 hover:bg-red-950/40
                text-slate-400 hover:text-red-400
                text-xs font-medium tracking-wide
                transition-all duration-200 ease-in-out
                focus:outline-none focus:ring-2 focus:ring-red-500/40 focus:ring-offset-2 focus:ring-offset-slate-900
                group
              "
            >
              <IconLogout />
              <span className="hidden sm:inline">
                {logoutHover ? "¿Cerrar sesión?" : "Salir"}
              </span>
            </button>
          </div>
        </div>
      </div>
    </header>
  );
}
