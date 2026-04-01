// Login.jsx
// Pantalla de autenticación — Sistema MBPC / Prefectura Naval Argentina.
//
// Flujo:
//  1. El usuario ingresa Usuario y CosteraId (mock temporal de credenciales).
//  2. Se hace POST a /api/auth/login con { usuario, costeraId }.
//  3. El backend devuelve { token, costeraId, nombreUsuario }.
//  4. Se guarda el token en localStorage con la clave 'mbpc_token'.
//  5. Se redirige al dashboard principal.
//
// Nota: costeraId es parte de las credenciales temporales mientras se
// implementa el directorio de usuarios en Oracle. En producción, el
// backend lo deriva de la base de usuarios y lo embebe en el JWT.

import { useState } from "react";
import { useNavigate } from "react-router-dom";
import { authApi } from "./axiosClient";

// ── SVG: Escudo de la Prefectura Naval Argentina ─────────────────────────────
// Representación simplificada para uso en UI; no reproduce artwork oficial.
const EscudoPNA = () => (
  <svg
    viewBox="0 0 80 80"
    className="w-20 h-20 drop-shadow-lg"
    aria-label="Escudo Prefectura Naval Argentina"
    role="img"
  >
    <circle cx="40" cy="40" r="38" fill="#003087" stroke="#C8A84B" strokeWidth="3" />
    <circle cx="40" cy="40" r="30" fill="#002060" stroke="#C8A84B" strokeWidth="1.5" />
    {/* Ancla estilizada */}
    <line x1="40" y1="18" x2="40" y2="58" stroke="#C8A84B" strokeWidth="3" strokeLinecap="round" />
    <line x1="28" y1="26" x2="52" y2="26" stroke="#C8A84B" strokeWidth="2.5" strokeLinecap="round" />
    <circle cx="40" cy="21" r="4" fill="none" stroke="#C8A84B" strokeWidth="2.5" />
    <path d="M28 54 Q28 62 36 62" fill="none" stroke="#C8A84B" strokeWidth="2.5" strokeLinecap="round" />
    <path d="M52 54 Q52 62 44 62" fill="none" stroke="#C8A84B" strokeWidth="2.5" strokeLinecap="round" />
    {/* Olas decorativas */}
    <path
      d="M22 68 Q27 64 32 68 Q37 72 42 68 Q47 64 52 68 Q57 72 58 68"
      fill="none"
      stroke="#C8A84B"
      strokeWidth="1.5"
      strokeLinecap="round"
    />
  </svg>
);

// ── COMPONENTE PRINCIPAL ─────────────────────────────────────────────────────
export default function Login() {
  const navigate = useNavigate();

  const [form, setForm] = useState({ usuario: "", costeraId: "" });
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState(null);

  const handleChange = (e) => {
    const { name, value } = e.target;
    setForm((prev) => ({ ...prev, [name]: value }));
    // Limpiar error al comenzar a corregir
    if (error) setError(null);
  };

  const handleSubmit = async (e) => {
    e.preventDefault();

    const usuario    = form.usuario.trim();
    const costeraId  = form.costeraId.trim();

    if (!usuario || !costeraId) {
      setError("Completá ambos campos para continuar.");
      return;
    }

    setLoading(true);
    setError(null);

    try {
      const { data } = await authApi.login({ usuario, costeraId });

      // Guardar el token JWT con la clave esperada por el interceptor de Axios
      localStorage.setItem("mbpc_token", data.token);

      // Opcional: persistir datos de sesión no sensibles para uso en la UI
      if (data.nombreUsuario) {
        localStorage.setItem("mbpc_usuario", data.nombreUsuario);
      }
      if (data.costeraId) {
        localStorage.setItem("mbpc_costera", data.costeraId);
      }

      navigate("/dashboard", { replace: true });
    } catch (err) {
      const status = err?.response?.status;

      if (status === 401) {
        setError("Credenciales incorrectas. Verificá tu usuario y costera.");
      } else if (status === 403) {
        setError("No tenés permisos para acceder a esa costera.");
      } else if (status >= 500) {
        setError("El servidor no está disponible. Intentá nuevamente en unos minutos.");
      } else {
        setError("No se pudo conectar con el servidor. Verificá tu red.");
      }
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="min-h-screen bg-gradient-to-br from-[#001a4d] via-[#003087] to-[#001a4d] flex items-center justify-center px-4">

      {/* Patrón de olas decorativo en el fondo */}
      <div className="absolute inset-0 overflow-hidden pointer-events-none" aria-hidden="true">
        <svg
          className="absolute bottom-0 left-0 w-full opacity-10"
          viewBox="0 0 1440 200"
          preserveAspectRatio="none"
        >
          <path
            d="M0,100 C240,160 480,40 720,100 C960,160 1200,40 1440,100 L1440,200 L0,200 Z"
            fill="#C8A84B"
          />
        </svg>
        <svg
          className="absolute bottom-0 left-0 w-full opacity-5"
          viewBox="0 0 1440 200"
          preserveAspectRatio="none"
        >
          <path
            d="M0,120 C360,60 720,180 1080,120 C1260,90 1380,140 1440,120 L1440,200 L0,200 Z"
            fill="#ffffff"
          />
        </svg>
      </div>

      {/* Card de login */}
      <div className="relative w-full max-w-md">
        <div className="bg-white/5 backdrop-blur-md border border-white/10 rounded-2xl shadow-2xl p-8">

          {/* Encabezado institucional */}
          <div className="flex flex-col items-center gap-3 mb-8">
            <EscudoPNA />
            <div className="text-center">
              <p className="text-[#C8A84B] text-xs font-semibold tracking-widest uppercase">
                República Argentina
              </p>
              <h1 className="text-white text-xl font-bold tracking-wide leading-tight mt-1">
                Prefectura Naval Argentina
              </h1>
              <p className="text-blue-200 text-sm mt-1">
                Sistema MBPC — Control de Tráfico Fluvial
              </p>
            </div>
          </div>

          {/* Separador */}
          <div className="flex items-center gap-3 mb-6">
            <div className="flex-1 h-px bg-white/10" />
            <span className="text-white/30 text-xs tracking-widest uppercase">Acceso Operativo</span>
            <div className="flex-1 h-px bg-white/10" />
          </div>

          {/* Formulario */}
          <form onSubmit={handleSubmit} noValidate className="space-y-4">

            {/* Campo: Usuario */}
            <div>
              <label
                htmlFor="usuario"
                className="block text-blue-100 text-sm font-medium mb-1.5"
              >
                Usuario
              </label>
              <div className="relative">
                <span className="absolute left-3 top-1/2 -translate-y-1/2 text-blue-300 pointer-events-none">
                  {/* Ícono de usuario */}
                  <svg className="w-4 h-4" fill="none" stroke="currentColor" strokeWidth={2} viewBox="0 0 24 24">
                    <path strokeLinecap="round" strokeLinejoin="round" d="M15.75 6a3.75 3.75 0 1 1-7.5 0 3.75 3.75 0 0 1 7.5 0ZM4.501 20.118a7.5 7.5 0 0 1 14.998 0A17.933 17.933 0 0 1 12 21.75c-2.676 0-5.216-.584-7.499-1.632Z" />
                  </svg>
                </span>
                <input
                  id="usuario"
                  name="usuario"
                  type="text"
                  autoComplete="username"
                  value={form.usuario}
                  onChange={handleChange}
                  placeholder="Ingresá tu usuario"
                  disabled={loading}
                  className="w-full bg-white/10 border border-white/20 rounded-lg pl-10 pr-4 py-2.5
                             text-white placeholder-white/30 text-sm
                             focus:outline-none focus:ring-2 focus:ring-[#C8A84B] focus:border-transparent
                             disabled:opacity-50 disabled:cursor-not-allowed
                             transition duration-150"
                />
              </div>
            </div>

            {/* Campo: CosteraId */}
            <div>
              <label
                htmlFor="costeraId"
                className="block text-blue-100 text-sm font-medium mb-1.5"
              >
                Costera / Sección
                <span className="ml-2 text-white/30 text-xs font-normal">(credencial temporal)</span>
              </label>
              <div className="relative">
                <span className="absolute left-3 top-1/2 -translate-y-1/2 text-blue-300 pointer-events-none">
                  {/* Ícono de ubicación */}
                  <svg className="w-4 h-4" fill="none" stroke="currentColor" strokeWidth={2} viewBox="0 0 24 24">
                    <path strokeLinecap="round" strokeLinejoin="round" d="M15 10.5a3 3 0 1 1-6 0 3 3 0 0 1 6 0Z" />
                    <path strokeLinecap="round" strokeLinejoin="round" d="M19.5 10.5c0 7.142-7.5 11.25-7.5 11.25S4.5 17.642 4.5 10.5a7.5 7.5 0 1 1 15 0Z" />
                  </svg>
                </span>
                <input
                  id="costeraId"
                  name="costeraId"
                  type="text"
                  autoComplete="off"
                  value={form.costeraId}
                  onChange={handleChange}
                  placeholder="Ej: COSTERAS-RIO-PARANA"
                  disabled={loading}
                  className="w-full bg-white/10 border border-white/20 rounded-lg pl-10 pr-4 py-2.5
                             text-white placeholder-white/30 text-sm
                             focus:outline-none focus:ring-2 focus:ring-[#C8A84B] focus:border-transparent
                             disabled:opacity-50 disabled:cursor-not-allowed
                             transition duration-150"
                />
              </div>
            </div>

            {/* Mensaje de error */}
            {error && (
              <div
                role="alert"
                className="flex items-start gap-2 bg-red-500/15 border border-red-400/30 rounded-lg px-4 py-3"
              >
                <svg className="w-4 h-4 text-red-400 mt-0.5 shrink-0" fill="none" stroke="currentColor" strokeWidth={2} viewBox="0 0 24 24">
                  <path strokeLinecap="round" strokeLinejoin="round" d="M12 9v3.75m-9.303 3.376c-.866 1.5.217 3.374 1.948 3.374h14.71c1.73 0 2.813-1.874 1.948-3.374L13.949 3.378c-.866-1.5-3.032-1.5-3.898 0L2.697 16.126ZM12 15.75h.007v.008H12v-.008Z" />
                </svg>
                <p className="text-red-300 text-sm leading-snug">{error}</p>
              </div>
            )}

            {/* Botón de submit */}
            <button
              type="submit"
              disabled={loading}
              className="w-full bg-[#C8A84B] hover:bg-[#b8943c] active:bg-[#a07f30]
                         text-[#001a4d] font-bold text-sm py-3 rounded-lg
                         transition duration-150 ease-in-out
                         disabled:opacity-60 disabled:cursor-not-allowed
                         flex items-center justify-center gap-2 mt-2"
            >
              {loading ? (
                <>
                  <svg className="animate-spin w-4 h-4" fill="none" viewBox="0 0 24 24">
                    <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" />
                    <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 0 1 8-8V0C5.373 0 0 5.373 0 12h4Z" />
                  </svg>
                  Autenticando…
                </>
              ) : (
                <>
                  <svg className="w-4 h-4" fill="none" stroke="currentColor" strokeWidth={2} viewBox="0 0 24 24">
                    <path strokeLinecap="round" strokeLinejoin="round" d="M16.5 10.5V6.75a4.5 4.5 0 1 0-9 0v3.75m-.75 11.25h10.5a2.25 2.25 0 0 0 2.25-2.25v-6.75a2.25 2.25 0 0 0-2.25-2.25H6.75a2.25 2.25 0 0 0-2.25 2.25v6.75a2.25 2.25 0 0 0 2.25 2.25Z" />
                  </svg>
                  Ingresar al Sistema
                </>
              )}
            </button>
          </form>

          {/* Footer */}
          <div className="mt-6 pt-5 border-t border-white/10 text-center">
            <p className="text-white/25 text-xs">
              Acceso restringido a personal autorizado · PNA © {new Date().getFullYear()}
            </p>
            <p className="text-white/15 text-xs mt-1">
              Sistema MBPC v2 — Modernización Digital
            </p>
          </div>
        </div>
      </div>
    </div>
  );
}
