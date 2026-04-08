// src/main.tsx
// ──────────────────────────────────────────────────────────────────────────────
// Punto de entrada de la aplicación.
//
// Correcciones aplicadas respecto a la versión anterior:
//   - BUGFIX (loop infinito): El interceptor de respuesta ahora busca y remueve
//     el token usando la key CORRECTA "mbpc_token" (tal como la guarda Login.jsx
//     con localStorage.setItem("mbpc_token", data.token)).
//     La versión anterior usaba una key distinta, lo que provocaba que el token
//     nunca se eliminara en un 401, generando un loop infinito de redirecciones.
//   - ROUTING: La ruta /dashboard renderiza <MainLayout><ViajesPage /></MainLayout>
//     aplicando el patrón de Layout como wrapper (Strangler Fig).
// ──────────────────────────────────────────────────────────────────────────────

import React from "react";
import ReactDOM from "react-dom/client";
import { BrowserRouter, Routes, Route, Navigate } from "react-router-dom";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";

import axios from "axios";

// ── Páginas y Layouts ──────────────────────────────────────────────────────
import Login       from "./pages/Login";
import MainLayout  from "./components/layout/MainLayout";
import ViajesPage  from "./pages/ViajesPage";

// ── CSS global ────────────────────────────────────────────────────────────
import "./index.css";

// ─────────────────────────────────────────────────────────────────────────────
// CONFIGURACIÓN DE REACT QUERY
// ─────────────────────────────────────────────────────────────────────────────
const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      retry: 1,
      staleTime: 30_000, // 30 segundos
      refetchOnWindowFocus: false,
    },
  },
});

// ─────────────────────────────────────────────────────────────────────────────
// INTERCEPTOR DE AXIOS — Inyección JWT + Manejo de 401
//
// IMPORTANTE: La key del localStorage DEBE ser "mbpc_token".
// Login.jsx persiste el token con:
//   localStorage.setItem("mbpc_token", data.token)   ← línea 123 de Login.jsx
//
// Si se usa cualquier otra key aquí (ej: "token", "authToken", "jwt"),
// el interceptor de REQUEST no encuentra el token → las peticiones se envían
// sin Authorization → el backend responde 401 → el interceptor de RESPONSE
// intenta remover una key inexistente → NO limpia nada → redirige al login →
// Login vuelve a cargar → el token sigue en localStorage con la key original →
// LOOP INFINITO.
// ─────────────────────────────────────────────────────────────────────────────
const TOKEN_KEY = "mbpc_token"; // ← key única, sincronizada con Login.jsx

// Interceptor de REQUEST: adjunta el Bearer token a cada petición saliente.
axios.interceptors.request.use(
  (config) => {
    const token = localStorage.getItem(TOKEN_KEY);
    if (token) {
      config.headers = config.headers ?? {};
      config.headers["Authorization"] = `Bearer ${token}`;
    }
    return config;
  },
  (error) => Promise.reject(error)
);

// Interceptor de RESPONSE: ante un 401, limpia sesión y redirige al login.
axios.interceptors.response.use(
  (response) => response,
  (error) => {
    if (error?.response?.status === 401) {
      // Eliminar el token con la MISMA key con la que fue guardado.
      localStorage.removeItem(TOKEN_KEY);
      // Hard redirect: limpia el estado de React Query y de toda la app.
      window.location.href = "/login";
    }
    return Promise.reject(error);
  }
);

// ─────────────────────────────────────────────────────────────────────────────
// GUARD DE AUTENTICACIÓN
// Redirige al login si no hay token en localStorage.
// ─────────────────────────────────────────────────────────────────────────────
function RequireAuth({ children }: { children: React.ReactNode }) {
  const token = localStorage.getItem(TOKEN_KEY);
  if (!token) {
    return <Navigate to="/login" replace />;
  }
  return <>{children}</>;
}

// ─────────────────────────────────────────────────────────────────────────────
// ÁRBOL DE RUTAS
// ─────────────────────────────────────────────────────────────────────────────
ReactDOM.createRoot(document.getElementById("root") as HTMLElement).render(
  <React.StrictMode>
    <QueryClientProvider client={queryClient}>
      <BrowserRouter>
        <Routes>
          {/* Ruta pública */}
          <Route path="/login" element={<Login />} />

          {/* Rutas protegidas */}
          <Route
            path="/dashboard"
            element={
              <RequireAuth>
                {/*
                  MainLayout actúa como el "cascarón": Navbar + footer.
                  ViajesPage es el controlador de vistas (dashboard / mapa AIS).
                  Este es el patrón correcto del Strangler Fig:
                  el layout persiste mientras se migran las páginas internas.
                */}
                <MainLayout>
                  <ViajesPage />
                </MainLayout>
              </RequireAuth>
            }
          />

          {/* Raíz: redirige al dashboard si hay sesión, sino al login */}
          <Route
            path="/"
            element={
              localStorage.getItem(TOKEN_KEY)
                ? <Navigate to="/dashboard" replace />
                : <Navigate to="/login" replace />
            }
          />

          {/* Catch-all */}
          <Route path="*" element={<Navigate to="/" replace />} />
        </Routes>
      </BrowserRouter>
    </QueryClientProvider>
  </React.StrictMode>
);
