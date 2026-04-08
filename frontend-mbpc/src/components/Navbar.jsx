// src/components/Navbar.jsx
// ──────────────────────────────────────────────────────────────────────────────
// Navbar fijo del sistema MBPC Geo / Prefectura Naval Argentina
//
// Características:
//  - Header sticky con color institucional PNA (#002454) y logo oficial.
//  - Muestra: escudo PNA · título del sistema · indicador de estado · avatar
//    con iniciales del usuario · botón Logout.
//  - Logout STATELESS: solo limpia localStorage y redirige a /login.
//    No realiza ningún llamado a la API (arquitectura JWT sin invalidación
//    server-side en esta fase; el token expira por TTL en el backend).
//
// Props: ninguna — la Navbar lee contexto propio del sistema.
// ──────────────────────────────────────────────────────────────────────────────

import { useState, useEffect } from "react";

// ── Íconos inline ─────────────────────────────────────────────────────────────

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

const getStoredUsername = () => {
  try { return localStorage.getItem("mbpc_usuario") || null; } catch { return null; }
};

const getStoredCostera = () => {
  try { return localStorage.getItem("mbpc_costera") || null; } catch { return null; }
};

/**
 * Genera las iniciales del nombre de usuario para el avatar.
 * Ej: "Ana Neri" → "AN" | "Carlos" → "CA" | null → "OP"
 */
const getInitials = (nombre) => {
  if (!nombre) return "OP";
  const partes = nombre.trim().split(/\s+/);
  if (partes.length === 1) return partes[0].slice(0, 2).toUpperCase();
  return (partes[0][0] + partes[1][0]).toUpperCase();
};

// ── Componente principal ──────────────────────────────────────────────────────
export default function Navbar() {
  const [usuario, setUsuario]       = useState(null);
  const [costera, setCostera]       = useState(null);
  const [isOnline, setIsOnline]     = useState(navigator.onLine);
  const [logoutHover, setLogoutHover] = useState(false);
  const [showUserMenu, setShowUserMenu] = useState(false);

  useEffect(() => {
    setUsuario(getStoredUsername());
    setCostera(getStoredCostera());
  }, []);

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

  // Cerrar menú de usuario al hacer click fuera
  useEffect(() => {
    if (!showUserMenu) return;
    const close = () => setShowUserMenu(false);
    window.addEventListener("click", close);
    return () => window.removeEventListener("click", close);
  }, [showUserMenu]);

  // ── Handler de Logout (Stateless) ──────────────────────────────────────────
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

  const initials = getInitials(usuario);

  return (
    <header
      className="sticky top-0 z-50 shadow-lg"
      style={{ backgroundColor: "#002454" }}
      role="banner"
    >
      {/* Línea de acento dorada superior */}
      <div
        className="h-[2px] w-full"
        aria-hidden="true"
        style={{
          background:
            "linear-gradient(90deg, transparent 0%, #C8A84B 25%, #e8c86b 50%, #C8A84B 75%, transparent 100%)",
        }}
      />

      <div className="max-w-screen-2xl mx-auto px-4 sm:px-6 lg:px-8">
        <div className="flex items-center justify-between h-16">

          {/* ── Lado izquierdo: Escudo PNA + Título ── */}
          <div className="flex items-center gap-4 min-w-0">
            <img
              src="https://www.argentina.gob.ar/sites/default/files/styles/isotipo/public/imagenEncabezado/prefectura-escudo.png?itok=EywBfOaV"
              alt="Escudo Prefectura Naval Argentina"
              className="h-10 w-auto shrink-0"
            />

            <div className="h-8 w-px bg-white/20 shrink-0" aria-hidden="true" />

            <div className="min-w-0">
              <h1 className="text-white font-bold text-lg tracking-tight leading-none">
                MBPC{" "}
                <span style={{ color: "#C8A84B" }}>Geo</span>
                <span className="hidden sm:inline text-white/40 font-normal text-base">
                  {" "}· H2
                </span>
              </h1>
              <p className="text-blue-200 text-xs mt-0.5 truncate max-w-[300px]">
                Prefectura Naval Argentina
                {costera ? ` · ${costera}` : " · DICO - DSIG"}
              </p>
            </div>
          </div>

          {/* ── Lado derecho: Conectividad + Avatar + Logout ── */}
          <div className="flex items-center gap-3 sm:gap-4 shrink-0">

            {/* Indicador de conectividad */}
            <div
              className="flex items-center gap-1.5 px-2.5 py-1 rounded-full"
              style={{
                background: isOnline ? "rgba(34,197,94,0.12)" : "rgba(239,68,68,0.12)",
                border: `1px solid ${isOnline ? "rgba(34,197,94,0.3)" : "rgba(239,68,68,0.3)"}`,
              }}
              title={isOnline ? "Conectado a la red PNA" : "Sin conexión"}
              aria-label={`Estado: ${isOnline ? "En línea" : "Sin conexión"}`}
            >
              <span
                className={`relative flex h-2 w-2 ${isOnline ? "text-green-400" : "text-red-400"}`}
                aria-hidden="true"
              >
                {isOnline && (
                  <span className="animate-ping absolute inline-flex h-full w-full rounded-full bg-green-400 opacity-50" />
                )}
                <IconSignal />
              </span>
              <span
                className={`text-[11px] font-mono font-semibold tracking-wide ${
                  isOnline ? "text-green-400" : "text-red-400"
                }`}
              >
                {isOnline ? "BACKEND ONLINE" : "SIN RED"}
              </span>
            </div>

            <div className="hidden sm:block h-5 w-px bg-white/20" aria-hidden="true" />

            {/* ── Avatar con iniciales + menú desplegable ── */}
            <div className="relative">
              <button
                type="button"
                onClick={(e) => { e.stopPropagation(); setShowUserMenu((v) => !v); }}
                aria-label={`Usuario: ${usuario || "Operador"}. Abrir menú`}
                aria-expanded={showUserMenu}
                className="flex items-center gap-2 group focus:outline-none"
              >
                {/* Avatar circular con iniciales */}
                <div
                  className="w-9 h-9 rounded-full flex items-center justify-center font-bold text-sm shrink-0 transition-all duration-200 group-hover:ring-2 group-hover:ring-[#C8A84B]/60"
                  style={{
                    backgroundColor: "#1a3a6b",
                    border: "2px solid #3b5fa0",
                    color: "#C8A84B",
                    letterSpacing: "0.05em",
                  }}
                  aria-hidden="true"
                >
                  {initials}
                </div>

                {/* Nombre y rol — solo desktop */}
                {usuario && (
                  <div className="hidden md:flex flex-col items-start leading-none gap-0.5">
                    <span className="text-white text-xs font-semibold truncate max-w-[110px]">
                      {usuario}
                    </span>
                    <span className="text-blue-300 text-[10px] tracking-wide">Operador</span>
                  </div>
                )}
              </button>

              {/* Menú desplegable del usuario */}
              {showUserMenu && (
                <div
                  className="absolute right-0 mt-2 w-48 rounded-lg shadow-xl overflow-hidden"
                  style={{
                    backgroundColor: "#001840",
                    border: "1px solid rgba(200,168,75,0.2)",
                    top: "100%",
                  }}
                  onClick={(e) => e.stopPropagation()}
                >
                  <div
                    className="px-4 py-3 border-b"
                    style={{ borderColor: "rgba(255,255,255,0.08)" }}
                  >
                    <p className="text-white text-xs font-semibold truncate">
                      {usuario || "Operador"}
                    </p>
                    <p className="text-blue-300 text-[10px] mt-0.5">
                      {costera || "Sin costera asignada"}
                    </p>
                  </div>

                  <button
                    type="button"
                    onClick={handleLogout}
                    className="w-full flex items-center gap-2 px-4 py-2.5 text-xs text-red-400 hover:bg-red-950/40 hover:text-red-300 transition-colors duration-150"
                  >
                    <IconLogout />
                    Cerrar sesión
                  </button>
                </div>
              )}
            </div>

            <div className="hidden sm:block h-5 w-px bg-white/20" aria-hidden="true" />

            {/* ── Botón de Logout directo (Stateless) ── */}
            <button
              type="button"
              onClick={handleLogout}
              onMouseEnter={() => setLogoutHover(true)}
              onMouseLeave={() => setLogoutHover(false)}
              aria-label="Cerrar sesión y volver al login"
              className="
                flex items-center gap-2
                px-3 py-1.5 rounded-md
                bg-red-600 hover:bg-red-700
                text-white text-xs font-semibold tracking-wide
                border border-red-500 hover:border-red-400
                transition-colors duration-200 ease-in-out
                shadow-sm
                focus:outline-none focus:ring-2 focus:ring-red-500/60 focus:ring-offset-2
                focus:ring-offset-[#002454]
              "
            >
              <IconLogout />
              <span className="hidden sm:inline">
                {logoutHover ? "¿Salir?" : "Salir"}
              </span>
            </button>

          </div>
        </div>
      </div>
    </header>
  );
}
