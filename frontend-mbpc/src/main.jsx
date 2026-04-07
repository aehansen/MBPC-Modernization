import React from 'react';
import ReactDOM from 'react-dom/client';
import { BrowserRouter, Routes, Route, Navigate } from 'react-router-dom';

// 1. IMPORTAMOS TANSTACK QUERY
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';

// Importamos nuestras dos pantallas principales
import MbpcDashboard from './MbpcDashboard';
import Login from "./pages/Login";

// Estilos globales
import './index.css';

// 2. CREAMOS LA INSTANCIA DEL CLIENTE
const queryClient = new QueryClient();

ReactDOM.createRoot(document.getElementById('root')).render(
  <React.StrictMode>
    {/* 3. ENVOLVEMOS LA APP CON EL PROVIDER */}
    <QueryClientProvider client={queryClient}>
      <BrowserRouter>
        <Routes>
          {/* Ruta para el Login */}
          <Route path="/login" element={<Login />} />
          
          {/* Ruta para el Sistema Principal */}
          <Route path="/dashboard" element={<MbpcDashboard />} />
          
          {/* Redirección por defecto */}
          <Route path="*" element={<Navigate to="/dashboard" replace />} />
        </Routes>
      </BrowserRouter>
    </QueryClientProvider>
  </React.StrictMode>
);