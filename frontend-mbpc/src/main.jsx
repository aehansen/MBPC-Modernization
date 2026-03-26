import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import './index.css'
// Importamos nuestro nuevo Dashboard en lugar de App
import MbpcDashboard from './MbpcDashboard.jsx'

createRoot(document.getElementById('root')).render(
  <StrictMode>
    <MbpcDashboard />
  </StrictMode>,
)