// src/main.tsx
// ──────────────────────────────────────────────────────────────────────────────
// Punto de entrada de la aplicación.
//
// NOTA: La configuración de Axios (interceptores, baseURL, timeout) está
// centralizada en axiosClient.js. Este archivo NO debe importar ni configurar
// axios directamente.
// ──────────────────────────────────────────────────────────────────────────────

import React from "react";
import ReactDOM from "react-dom/client";
import { BrowserRouter, Routes, Route, Navigate } from "react-router-dom";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";

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
// CONSTANTE DE TOKEN — sincronizada con axiosClient.js y Login.jsx
// ─────────────────────────────────────────────────────────────────────────────
const TOKEN_KEY = "mbpc_token";

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
