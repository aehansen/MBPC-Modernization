// axiosClient.js
// Capa de red centralizada para el sistema MBPC — Prefectura Naval Argentina.
//
// ── ÚNICA FUENTE DE VERDAD para Axios ────────────────────────────────────────
// Este módulo es el único lugar donde se configura Axios. Ningún otro módulo
// debe importar 'axios' directamente ni registrar interceptores propios.
//
// Responsabilidades:
//  1. Inyectar el JWT en cada request (interceptor de salida).
//  2. Redirigir al login y limpiar el token en respuestas 401/403 (interceptor de entrada).
//  3. Exponer helpers tipados por dominio (viajes, auth, mapa, carga, tipoCarga) para evitar
//     que los componentes conozcan las rutas de la API.

import axios from "axios";

const TOKEN_KEY = "mbpc_token";
const LOGIN_ROUTE = "/login";

export const apiClient = axios.create({
  baseURL: import.meta.env.VITE_API_BASE_URL ?? "/api",
  timeout: 30000,
});

apiClient.interceptors.request.use(
  (config) => {
    const token = localStorage.getItem(TOKEN_KEY);
    if (token) {
      config.headers.Authorization = `Bearer ${token}`;
    }
    return config;
  },
  (error) => Promise.reject(error)
);

apiClient.interceptors.response.use(
  (response) => response,
  (error) => {
    if (error.response?.status === 401 || error.response?.status === 403) {
      localStorage.removeItem(TOKEN_KEY);
      window.location.replace(LOGIN_ROUTE);
    }
    return Promise.reject(error);
  }
);

export const authApi = {
  login: (credentials) => apiClient.post("/auth/login", credentials),
};

export const viajesApi = {
  getAll: () => apiClient.get("/viajes"),
  getByMmsi: (mmsi) => apiClient.get(`/viajes/${encodeURIComponent(mmsi)}`),
  getEnPuerto: () => apiClient.get("/viajes/puerto"),
  getHistorico: () => apiClient.get("/viajes/historico"),
  iniciar: (nuevoViaje) => apiClient.post("/viajes", nuevoViaje),
  zarpar: (id) => apiClient.put(`/viajes/${id}/zarpar`),
  amarrar: (id) => apiClient.put(`/viajes/${id}/amarrar`),
  fondear: (id) => apiClient.put(`/viajes/${id}/fondear`),
  reanudar: (id) => apiClient.put(`/viajes/${id}/reanudar`),
  actualizarPosicion: (id, payload) => apiClient.put(`/viajes/${id}/posicion`, payload),
};

export const mapaApi = {
  getMapaViajes: (params = {}) => apiClient.get("/viajes/mapa", { params }),
};

export const cargaApi = {
  getByViaje: (viajeId) => apiClient.get(`/carga/viaje/${encodeURIComponent(viajeId)}`),
  update: (cargaId, payload) => apiClient.put(`/carga/${encodeURIComponent(cargaId)}`, payload),
  delete: (viajeId, cargaId) => apiClient.delete(`/carga/${encodeURIComponent(viajeId)}/${encodeURIComponent(cargaId)}`),
};

export const tipoCargaApi = {
  autocomplete: (query) => apiClient.get("/tipocarga/autocomplete", { params: { query } }),
};

export default apiClient;
