// axiosClient.js
// Capa de red centralizada para el sistema MBPC — Prefectura Naval Argentina.
//
// Responsabilidades:
//  1. Inyectar el JWT en cada request (interceptor de salida).
//  2. Redirigir al login y limpiar el token en respuestas 401/403 (interceptor de entrada).
//  3. Exponer helpers tipados por dominio (viajes, auth, mapa) para evitar
//     que los componentes conozcan las rutas de la API.

import axios from "axios";

// ── INSTANCIA BASE ───────────────────────────────────────────────────────────
const apiClient = axios.create({
  // En Vite, el proxy de desarrollo en vite.config.js redirige /api → backend.
  // En producción, la variable de entorno VITE_API_BASE_URL apunta al gateway.
  baseURL: import.meta.env.VITE_API_BASE_URL ?? "/api",
  timeout: 15_000,
  headers: {
    "Content-Type": "application/json",
    Accept: "application/json",
  },
});

// ── CONSTANTES ───────────────────────────────────────────────────────────────
const TOKEN_KEY    = "mbpc_token";
const LOGIN_ROUTE  = "/login";

// ── INTERCEPTOR DE REQUEST — Inyección de JWT ────────────────────────────────
//
// Lee el token desde localStorage y lo agrega como Bearer en cada petición.
// Si no existe token (usuario no autenticado), la request sale sin cabecera
// Authorization; el backend responderá 401 y el interceptor de respuesta
// se encarga de la redirección.
apiClient.interceptors.request.use(
  (config) => {
    const token = localStorage.getItem(TOKEN_KEY);
    if (token) {
      config.headers["Authorization"] = `Bearer ${token}`;
    }
    return config;
  },
  (error) => Promise.reject(error)
);

// ── INTERCEPTOR DE RESPONSE — Manejo de 401 / 403 ───────────────────────────
//
// 401 Unauthorized → el token expiró o es inválido. Se limpia y se redirige al login.
// 403 Forbidden    → el token es válido pero el usuario no tiene permisos (ej: intenta
//                    acceder a una costera que no le corresponde). Mismo tratamiento:
//                    se limpia sesión y se redirige para forzar nuevo login.
//
// En ambos casos se usa window.location.replace (en lugar de href) para que el
// browser no guarde la ruta protegida en el historial, evitando loops de redirección.
apiClient.interceptors.response.use(
  (response) => response,
  (error) => {
    const status = error?.response?.status;

    if (status === 401 || status === 403) {
      localStorage.removeItem(TOKEN_KEY);

      // Solo redirigimos si no estamos ya en la pantalla de login
      // para evitar bucles infinitos.
      if (!window.location.pathname.startsWith(LOGIN_ROUTE)) {
        window.location.replace(LOGIN_ROUTE);
      }
    }

    return Promise.reject(error);
  }
);

// ── HELPERS DE DOMINIO ───────────────────────────────────────────────────────
//
// Centralizamos las rutas aquí para que los componentes no tengan magic strings.
// Cada helper acepta los parámetros que necesita y devuelve la promesa de Axios.
// El costeraId se obtiene del token en el backend; no hace falta enviarlo
// explícitamente en el body — el backend lo extrae del claim JWT.

export const authApi = {
  /**
   * Autentica al usuario y devuelve { token, costeraId, nombreUsuario }.
   * @param {{ usuario: string, costeraId: string }} credentials
   */
  login: (credentials) => apiClient.post("/auth/login", credentials),
};

export const viajesApi = {
  /**
   * Lista de viajes paginada. El backend filtra por el costeraId del JWT.
   * @param {{ pagina?: number, tamanio?: number }} params
   */
  getViajes: (params = {}) =>
    apiClient.get("/viajes", { params }),

  /**
   * Última posición de un buque por MMSI.
   * @param {string} mmsi
   */
  getByMmsi: (mmsi) => apiClient.get(`/viajes/mmsi/${mmsi}`),

  /**
   * Barcos actualmente en puerto dentro de la jurisdicción del usuario.
   */
  getBarcosEnPuerto: () => apiClient.get("/viajes/en-puerto"),

  /**
   * Histórico filtrado. El backend agrega el costeraId del JWT al SP de Oracle.
   * @param {import('./types').FiltroHistorico} filtro
   */
  getHistorico: (filtro) => apiClient.get("/viajes/historico", { params: filtro }),

  /**
   * Inicia un nuevo viaje.
   * @param {import('./types').NuevoViajeDto} nuevoViaje
   */
  iniciarViaje: (nuevoViaje) => apiClient.post("/viajes", nuevoViaje),

  // ── Máquina de estados ────────────────────────────────────────────────────
  zarpar:   (id) => apiClient.put(`/viajes/${id}/zarpar`),
  amarrar:  (id) => apiClient.put(`/viajes/${id}/amarrar`),
  fondear:  (id) => apiClient.put(`/viajes/${id}/fondear`),
  reanudar: (id) => apiClient.put(`/viajes/${id}/reanudar`),
};

export const mapaApi = {
  /**
   * Puntos del mapa filtrados por costera del JWT.
   * @param {{ mmsi?: string, nombreBuque?: string }} params
   */
  getMapaViajes: (params = {}) =>
    apiClient.get("/viajes/mapa", { params }),
};

// Exportamos la instancia por si algún módulo necesita hacer requests ad-hoc.
export default apiClient;
