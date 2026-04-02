// src/services/apiClient.js
import axios from 'axios';

// Instancia centralizada de Axios
const apiClient = axios.create({
    // IMPORTANTE: Ajustá este puerto al puerto real donde levanta tu consola de .NET 8
    baseURL: 'http://localhost:5009', 
    headers: {
        'Content-Type': 'application/json'
    }
});

// ── Interceptor de SALIDA (Request) ──
// Busca el token en localStorage y lo inyecta en el header Authorization
apiClient.interceptors.request.use((config) => {
    const token = localStorage.getItem('mbpc_token');
    if (token) {
        config.headers.Authorization = `Bearer ${token}`;
    }
    return config;
});

// ── Interceptor de ENTRADA (Response) ──
// Escucha todas las respuestas del backend. Si el backend rechaza el token (401),
// purga la memoria y patea al operador a la pantalla de Login.
apiClient.interceptors.response.use(
    (response) => response,
    (error) => {
        if (error.response && error.response.status === 401) {
            console.warn("Sesión expirada o inválida. Redirigiendo al Login...");
            localStorage.removeItem('mbpc_token');
            // Evitamos loop infinito si ya estamos en el login
            if (window.location.pathname !== '/login') {
                window.location.href = '/login';
            }
        }
        return Promise.reject(error);
    }
);

export default apiClient;