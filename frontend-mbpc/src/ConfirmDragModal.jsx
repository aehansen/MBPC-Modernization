import React from 'react';

// Modal component for confirming ship relocation via drag-and-drop
// Uses premium styling with dark mode and glassmorphism effect
export default function ConfirmDragModal({
  open,
  buqueNombre,
  newLat,
  newLng,
  onConfirm,
  onCancel,
}) {
  if (!open) return null;

  return (
    <div className="confirm-drag-modal-overlay">
      <div className="confirm-drag-modal">
        <h2>Confirmar reubicación</h2>
        <p>
          ¿Deseas reubicar el buque <strong>{buqueNombre}</strong> a las coordenadas
          {newLat.toFixed(5)}, {newLng.toFixed(5)}?
        </p>
        <div className="confirm-drag-buttons">
          <button className="btn-confirm" onClick={onConfirm}>
            Confirmar
          </button>
          <button className="btn-cancel" onClick={onCancel}>
            Cancelar
          </button>
        </div>
      </div>
      <style jsx>{`
        .confirm-drag-modal-overlay {
          position: fixed;
          inset: 0;
          background: rgba(0, 0, 0, 0.45);
          backdrop-filter: blur(4px);
          display: flex;
          align-items: center;
          justify-content: center;
          z-index: 1000;
        }
        .confirm-drag-modal {
          background: rgba(30, 30, 30, 0.85);
          border-radius: 12px;
          padding: 2rem;
          max-width: 420px;
          width: 90%;
          color: #f0f0f0;
          box-shadow: 0 8px 32px rgba(0, 0, 0, 0.6);
          text-align: center;
        }
        h2 {
          margin-top: 0;
          margin-bottom: 1rem;
          font-size: 1.5rem;
          color: #ffcc00;
        }
        p {
          margin: 1rem 0;
          line-height: 1.4;
        }
        .confirm-drag-buttons {
          display: flex;
          gap: 1rem;
          justify-content: center;
          margin-top: 1.5rem;
        }
        .btn-confirm,
        .btn-cancel {
          padding: 0.6rem 1.2rem;
          border: none;
          border-radius: 6px;
          font-weight: 600;
          cursor: pointer;
          transition: background 0.2s ease;
        }
        .btn-confirm {
          background: #007bff;
          color: #fff;
        }
        .btn-confirm:hover {
          background: #0056b3;
        }
        .btn-cancel {
          background: #6c757d;
          color: #fff;
        }
        .btn-cancel:hover {
          background: #5a6268;
        }
      `}</style>
    </div>
  );
}
