import { useState, useEffect } from 'react';
import axios from 'axios';

function App() {
  // Estado para los viajes y cargas
  const [viajes, setViajes] = useState([]);
  const [cargas, setCargas] = useState([]);
  const [viajeSeleccionado, setViajeSeleccionado] = useState(null);
  
  // Estado general de la UI
  const [loading, setLoading] = useState(true);
  const [mensaje, setMensaje] = useState("");

  // Estado para el formulario de NUEVO VIAJE
  const [nuevoBuque, setNuevoBuque] = useState("");
  const [nuevoOrigen, setNuevoOrigen] = useState("");
  const [nuevoDestino, setNuevoDestino] = useState("");

  const apiUrl = 'http://localhost:5009/api';

  useEffect(() => {
    axios.get(`${apiUrl}/viaje/activos`)
      .then(response => {
        setViajes(response.data);
        setLoading(false);
      })
      .catch(err => console.error(err));
  }, []);

  // --- NUEVA FUNCIÓN: POST PARA CREAR VIAJE ---
  const iniciarViaje = (e) => {
    e.preventDefault(); // Evita que la página se recargue al mandar el formulario

    const payload = {
      nombreBuque: nuevoBuque,
      origen: nuevoOrigen,
      destino: nuevoDestino
    };

    axios.post(`${apiUrl}/viaje`, payload)
      .then(response => {
        // Agregamos el viaje nuevo al principio de la lista actual en memoria
        setViajes([response.data, ...viajes]); 
        setMensaje(`🚢 ¡Viaje del buque ${response.data.buque} iniciado con éxito!`);
        
        // Limpiamos el formulario
        setNuevoBuque("");
        setNuevoOrigen("");
        setNuevoDestino("");
      })
      .catch(err => {
        console.error(err);
        setMensaje("❌ Error al iniciar el viaje. Verificá que todos los campos estén completos.");
      });
  };

  // --- FUNCIONES DE CARGAS (Ya existían) ---
  const verDetalleViaje = (viaje) => {
    setViajeSeleccionado(viaje);
    setMensaje(""); 
    axios.get(`${apiUrl}/carga/viaje/${viaje.id}`)
      .then(response => setCargas(response.data))
      .catch(err => console.error(err));
  };

  const amarrarBarcaza = (cargaId) => {
    const muelle = prompt("Ingrese el nombre del Muelle de destino:");
    if (!muelle) return;

    axios.put(`${apiUrl}/carga/${cargaId}/amarrar?nuevoMuelle=${encodeURIComponent(muelle)}`)
      .then(response => setMensaje(`✅ ${response.data.mensaje}`))
      .catch(err => setMensaje("❌ Error al amarrar."));
  };

  const fondearBarcaza = (cargaId) => {
    const zona = prompt("Ingrese la Zona de Fondeo:");
    if (!zona) return;

    axios.put(`${apiUrl}/carga/${cargaId}/fondear?zonaFondeo=${encodeURIComponent(zona)}`)
      .then(response => setMensaje(`⚓ ${response.data.mensaje}`))
      .catch(err => setMensaje("❌ Error al fondear."));
  };

  if (loading) return <h2>Cargando la flota activa...</h2>;

  return (
    <div style={{ padding: '40px', fontFamily: 'sans-serif' }}>
      <h1>Tablero de Viajes Activos - Sistema MBPC</h1>
      
      {mensaje && (
        <div style={{ padding: '15px', marginBottom: '20px', backgroundColor: '#e2e3e5', borderRadius: '4px', fontWeight: 'bold' }}>
          {mensaje}
        </div>
      )}

      {/* --- NUEVO FORMULARIO DE ZARPE --- */}
      <div style={{ padding: '20px', backgroundColor: '#f4f6f8', border: '1px solid #ccc', borderRadius: '8px', marginBottom: '30px' }}>
        <h3 style={{ marginTop: 0 }}>Despachar Nuevo Buque</h3>
        <form onSubmit={iniciarViaje} style={{ display: 'flex', gap: '15px', alignItems: 'flex-end' }}>
          
          <div style={{ display: 'flex', flexDirection: 'column' }}>
            <label style={{ fontWeight: 'bold', marginBottom: '5px' }}>Nombre del Buque</label>
            <input 
              type="text" 
              value={nuevoBuque} 
              onChange={(e) => setNuevoBuque(e.target.value)} 
              placeholder="Ej. GC-28 PREFECTO DERBES" 
              style={{ padding: '8px', width: '250px' }} 
              required
            />
          </div>

          <div style={{ display: 'flex', flexDirection: 'column' }}>
            <label style={{ fontWeight: 'bold', marginBottom: '5px' }}>Origen</label>
            <input 
              type="text" 
              value={nuevoOrigen} 
              onChange={(e) => setNuevoOrigen(e.target.value)} 
              placeholder="Ej. Mar del Plata" 
              style={{ padding: '8px', width: '200px' }} 
              required
            />
          </div>

          <div style={{ display: 'flex', flexDirection: 'column' }}>
            <label style={{ fontWeight: 'bold', marginBottom: '5px' }}>Destino</label>
            <input 
              type="text" 
              value={nuevoDestino} 
              onChange={(e) => setNuevoDestino(e.target.value)} 
              placeholder="Ej. Bahía Blanca" 
              style={{ padding: '8px', width: '200px' }} 
              required
            />
          </div>

          <button type="submit" style={{ padding: '10px 20px', backgroundColor: '#28a745', color: 'white', border: 'none', borderRadius: '4px', cursor: 'pointer', fontWeight: 'bold' }}>
            Iniciar Viaje
          </button>
        </form>
      </div>

      {/* GRILLA DE VIAJES */}
      <table style={{ width: '100%', borderCollapse: 'collapse', textAlign: 'left' }}>
        <thead>
          <tr style={{ backgroundColor: '#2c3e50', color: 'white' }}>
            <th style={{ padding: '12px' }}>Buque</th>
            <th style={{ padding: '12px' }}>Ruta</th>
            <th style={{ padding: '12px' }}>Estado</th>
            <th style={{ padding: '12px' }}>Acciones</th>
          </tr>
        </thead>
        <tbody>
          {viajes.map(viaje => (
            <tr key={viaje.id} style={{ borderBottom: '1px solid #ddd' }}>
              <td style={{ padding: '12px', fontWeight: 'bold' }}>{viaje.buque}</td>
              <td style={{ padding: '12px' }}>{viaje.ruta}</td>
              <td style={{ padding: '12px' }}>
                <span style={{ backgroundColor: viaje.estadoActual === 'En Curso' ? '#d4edda' : '#fff3cd', color: viaje.estadoActual === 'En Curso' ? '#155724' : '#856404', padding: '4px 8px', borderRadius: '4px' }}>
                  {viaje.estadoActual}
                </span>
              </td>
              <td style={{ padding: '12px' }}>
                <button 
                  onClick={() => verDetalleViaje(viaje)}
                  style={{ padding: '6px 12px', cursor: 'pointer', backgroundColor: '#007bff', color: 'white', border: 'none', borderRadius: '4px' }}
                >
                  Ver Cargas
                </button>
              </td>
            </tr>
          ))}
        </tbody>
      </table>

      {/* PANEL DE DETALLE DE CARGAS */}
      {viajeSeleccionado && (
        <div style={{ marginTop: '40px', padding: '20px', backgroundColor: '#f8f9fa', border: '1px solid #ddd', borderRadius: '8px' }}>
          <h2>Gestión Logística: {viajeSeleccionado.buque}</h2>
          {cargas.length === 0 ? (
            <p>No hay cargas o barcazas registradas para este viaje.</p>
          ) : (
            <table style={{ width: '100%', borderCollapse: 'collapse', textAlign: 'left', backgroundColor: 'white' }}>
              <thead>
                <tr style={{ backgroundColor: '#6c757d', color: 'white' }}>
                  <th style={{ padding: '10px' }}>Descripción</th>
                  <th style={{ padding: '10px' }}>Riesgo IMO</th>
                  <th style={{ padding: '10px' }}>Operaciones</th>
                </tr>
              </thead>
              <tbody>
                {cargas.map(carga => (
                  <tr key={carga.id} style={{ borderBottom: '1px solid #eee' }}>
                    <td style={{ padding: '10px' }}>{carga.descripcionLista}</td>
                    <td style={{ padding: '10px', color: carga.nivelRiesgo === 'Alto' ? 'red' : 'green', fontWeight: 'bold' }}>
                      {carga.nivelRiesgo}
                    </td>
                    <td style={{ padding: '10px', gap: '10px', display: 'flex' }}>
                      <button onClick={() => amarrarBarcaza(carga.id)} style={{ padding: '6px', cursor: 'pointer', backgroundColor: '#28a745', color: 'white', border: 'none', borderRadius: '4px' }}>Amarrar</button>
                      <button onClick={() => fondearBarcaza(carga.id)} style={{ padding: '6px', cursor: 'pointer', backgroundColor: '#ffc107', color: 'black', border: 'none', borderRadius: '4px' }}>Fondear</button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </div>
      )}
    </div>
  );
}

export default App;