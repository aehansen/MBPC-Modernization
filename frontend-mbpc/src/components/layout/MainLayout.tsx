import React from "react";
import { Toaster } from "react-hot-toast";
import Navbar from "../Navbar";
import ChatFloatingWindow from "../chat/ChatFloatingWindow"; // <-- NUEVA IMPORTACIÓN

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

      {/* ── TOASTER ─────────────────────────────────────────────────────────
          Proveedor global de toasts. Se posiciona aquí para que esté
          disponible desde cualquier componente hijo sin necesidad de
          un contexto adicional.
      ─────────────────────────────────────────────────────────────────────── */}
      <Toaster />

      {/* ── ENCABEZADO ──────────────────────────────────────────────────────
          Navbar importado desde src/components/Navbar.jsx.
          Idéntico al que renderizaba MbpcDashboard.jsx (línea 506).
      ─────────────────────────────────────────────────────────────────────── */}
      <Navbar />
      
      {/* ── CONTENIDO DE LA PÁGINA ──────────────────────────────────────────
          flex-grow garantiza que el main ocupe todo el espacio disponible
          entre la botonera y el footer.
          El padding lo controla cada página según su necesidad.
      ─────────────────────────────────────────────────────────────────────── */}
      <main className="flex-grow flex flex-col">
        {children}
      </main>

      {/* ── PIE DE PÁGINA ───────────────────────────────────────────────────
          Copiado textualmente de MbpcDashboard.jsx (líneas 818-821).
      ─────────────────────────────────────────────────────────────────────── */}
      <footer className="border-t mt-12 p-6 bg-white text-center text-xs text-gray-400">
        <p>
          &copy; {new Date().getFullYear()} Prefectura Naval Argentina - Dirección de Informática y Comunicaciones.
          <br /> División Sistemas de Información Geográfica
        </p>
        <p className="mt-1 font-medium">
          MBPC Geo H2
        </p>
      </footer>

      {/* ── MODAL: AMARRAR BARCAZA ──────────────────────────────────────────
          Renderizado vía portal al document.body (ver ModalAmarrarBarcaza).
          Se monta aquí en el layout para que sea accesible globalmente
          sin importar qué página hija esté activa.
      ─────────────────────────────────────────────────────────────────────── */}

      {/* ── ASISTENTE IA FLOTANTE ───────────────────────────────────────────
          Renderizado al final del árbol para garantizar z-index correcto
          sobre todos los elementos del layout. El componente gestiona su
          propio estado de visibilidad internamente.
      ─────────────────────────────────────────────────────────────────────── */}
      <ChatFloatingWindow 
        botName="Asistente IA MBPC"
        welcomeMessage="Bienvenido al sistema MBPC Geo H2. ¿En qué puedo ayudarte hoy?"
      />
      
    </div>
  );
}