// src/components/layout/MainLayout.tsx
// ──────────────────────────────────────────────────────────────────────────────
// Layout principal de la aplicación MBPC Geo H2.
//
// Este componente reemplaza el rol que cumplía MbpcDashboard.jsx como "cascarón"
// de la app: contiene el Navbar en la parte superior, un <main> flexible para
// renderizar los children (páginas), y el footer institucional idéntico al que
// tenía el componente legacy (extraído de MbpcDashboard.jsx líneas 817-821).
//
// Úsalo como wrapper en el router:
//   <MainLayout><ViajesPage /></MainLayout>
//   <MainLayout><OtraPage /></MainLayout>
// ──────────────────────────────────────────────────────────────────────────────

import React from "react";
import Navbar from "../Navbar";

// ─────────────────────────────────────────────────────────────────────────────
// TIPOS
// ─────────────────────────────────────────────────────────────────────────────
interface MainLayoutProps {
  children: React.ReactNode;
}

// ─────────────────────────────────────────────────────────────────────────────
// COMPONENTE
// ─────────────────────────────────────────────────────────────────────────────
export default function MainLayout({ children }: MainLayoutProps) {
  return (
    <div className="min-h-screen bg-gray-50 flex flex-col font-sans text-gray-900">

      {/* ── ENCABEZADO ──────────────────────────────────────────────────────
          Navbar importado desde src/components/Navbar.jsx.
          Idéntico al que renderizaba MbpcDashboard.jsx (línea 506).
      ─────────────────────────────────────────────────────────────────────── */}
      <Navbar />

      {/* ── CONTENIDO DE LA PÁGINA ──────────────────────────────────────────
          flex-grow garantiza que el main ocupe todo el espacio disponible
          entre el Navbar y el footer, evitando que el footer "flote" arriba
          en páginas con poco contenido.
          El padding lo controla cada página según su necesidad; ViajesPage,
          por ejemplo, necesita padding 0 en la vista de mapa pero p-6 en la
          vista de dashboard.
      ─────────────────────────────────────────────────────────────────────── */}
      <main className="flex-grow flex flex-col">
        {children}
      </main>

      {/* ── PIE DE PÁGINA ───────────────────────────────────────────────────
          Copiado textualmente de MbpcDashboard.jsx (líneas 818-821).
          Se eliminó el condicional vistaActual === 'dashboard' porque el
          footer ahora es responsabilidad del layout, no de la página.
          Si una página específica (ej: MapaAIS fullscreen) necesita ocultar
          el footer, puede hacerse extendiendo MainLayout con una prop
          hideFooter o creando un MapLayout alternativo.
      ─────────────────────────────────────────────────────────────────────── */}
      <footer className="border-t mt-12 p-6 bg-white text-center text-xs text-gray-400">
        <p>
          &copy; {new Date().getFullYear()} Prefectura Naval Argentina - Dirección de Informática y Comunicaciones.
          <br /> Divisón Sistemas de Información Geográfica
        </p>
        <p className="mt-1">
          MBPC Geo H2
        </p>
      </footer>

    </div>
  );
}
