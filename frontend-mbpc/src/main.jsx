import React from 'react';
import ReactDOM from 'react-dom/client';
import { BrowserRouter, Routes, Route, Navigate } from 'react-router-dom';

// Importamos nuestras dos pantallas principales
import MbpcDashboard from './MbpcDashboard';
import Login from './Login';

// Estilos globales
import './index.css';

ReactDOM.createRoot(document.getElementById('root')).render(
  <React.StrictMode>
    <BrowserRouter>
      <Routes>
        {/* Ruta para el Login */}
        <Route path="/login" element={<Login />} />
        
        {/* Ruta para el Sistema Principal */}
        <Route path="/dashboard" element={<MbpcDashboard />} />
        
        {/* Redirección por defecto: si alguien entra a "/" o a una ruta que no existe, va al dashboard (y si no tiene token, el dashboard lo pateará al login) */}
        <Route path="*" element={<Navigate to="/dashboard" replace />} />
      </Routes>
    </BrowserRouter>
  </React.StrictMode>
);