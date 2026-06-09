import React from "react";
import { Routes, Route, Navigate } from "react-router-dom";

// ── Páginas y Layouts ──────────────────────────────────────────────────────
import Login from "./pages/Login";
import MainLayout from "./components/layout/MainLayout";
import ViajesPage from "./pages/ViajesPage";
import Catalogos from "./pages/Catalogos";

// ─────────────────────────────────────────────────────────────────────────────
// CONSTANTE DE TOKEN — sincronizada con axiosClient.js, main.tsx y Login.jsx
// ─────────────────────────────────────────────────────────────────────────────
const TOKEN_KEY = "mbpc_token";

// ─────────────────────────────────────────────────────────────────────────────
// GUARD DE AUTENTICACIÓN
// Redirige al login si no hay token en localStorage.
// ─────────────────────────────────────────────────────────────────────────────
function RequireAuth({ children }) {
  const token = localStorage.getItem(TOKEN_KEY);
  if (!token) {
    return <Navigate to="/login" replace />;
  }
  return <>{children}</>;
}

export default function App() {
  return (
    <Routes>
      {/* Ruta pública */}
      <Route path="/login" element={<Login />} />

      {/* Rutas protegidas dentro de MainLayout */}
      <Route
        path="/dashboard"
        element={
          <RequireAuth>
            <MainLayout>
              <ViajesPage />
            </MainLayout>
          </RequireAuth>
        }
      />

      <Route
        path="/catalogos"
        element={
          <RequireAuth>
            <MainLayout>
              <Catalogos />
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
  );
}
