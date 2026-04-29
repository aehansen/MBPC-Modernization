import React, { useState, useEffect, useRef } from 'react';
import { useBuscarBarcazas, AutocompleteBarcaza } from '@/hooks/useBuscarBarcazas';

interface BarcazaAutocompleteProps {
  etapaId: string;
  onSelect: (barcaza: AutocompleteBarcaza) => void;
  onClear: () => void;
  disabled?: boolean;
}

export default function BarcazaAutocomplete({ etapaId, onSelect, onClear, disabled = false }: BarcazaAutocompleteProps) {
  const [inputValue, setInputValue] = useState('');
  const [debouncedValue, setDebouncedValue] = useState('');
  const [showDropdown, setShowDropdown] = useState(false);
  const dropdownRef = useRef<HTMLDivElement>(null);

  // Lógica nativa de Debounce
  useEffect(() => {
    const handler = setTimeout(() => {
      setDebouncedValue(inputValue);
    }, 400);
    return () => clearTimeout(handler);
  }, [inputValue]);

  // Query a la API
  const { data: suggestions = [], isFetching, isError } = useBuscarBarcazas(etapaId, debouncedValue);

  // Manejador para cerrar el dropdown si se hace clic afuera
  useEffect(() => {
    if (!showDropdown) return;
    const handleClickOutside = (event: MouseEvent) => {
      if (dropdownRef.current && !dropdownRef.current.contains(event.target as Node)) {
        setShowDropdown(false);
      }
    };
    document.addEventListener('mousedown', handleClickOutside);
    return () => document.removeEventListener('mousedown', handleClickOutside);
  }, [showDropdown]);

  const handleInputChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const value = e.target.value;
    setInputValue(value);
    onClear(); // Limpiamos el valor en el padre si el usuario vuelve a escribir
    
    if (value.trim().length >= 2) {
      setShowDropdown(true);
    } else {
      setShowDropdown(false);
    }
  };

  const handleSelect = (barcaza: AutocompleteBarcaza) => {
    setInputValue(barcaza.nombre);
    setShowDropdown(false);
    onSelect(barcaza);
  };

  return (
    <div className="relative w-full" ref={dropdownRef}>
      <label className="block text-xs font-semibold text-gray-600 uppercase tracking-wider mb-1.5">
        Buscar Barcaza
      </label>
      <input
        type="text"
        value={inputValue}
        onChange={handleInputChange}
        onFocus={() => {
          if (inputValue.trim().length >= 2) setShowDropdown(true);
        }}
        placeholder="Buscar por nombre, matrícula..."
        disabled={disabled}
        autoComplete="off"
        className="w-full border border-gray-300 rounded-lg px-3 py-2.5 text-sm focus:outline-none focus:ring-2 focus:ring-[#104a8e]/40 focus:border-[#104a8e] disabled:bg-gray-50 transition-colors"
      />

      {showDropdown && (
        <div className="absolute z-50 w-full mt-1 bg-white border border-gray-200 rounded-lg shadow-xl max-h-60 overflow-y-auto">
          {isFetching ? (
            <div className="px-4 py-3 text-sm text-gray-500">Buscando...</div>
          ) : isError ? (
            <div className="px-4 py-3 text-sm text-red-500">Error al buscar barcazas.</div>
          ) : suggestions.length > 0 ? (
            <ul className="py-1">
              {suggestions.map((b) => (
                <li
                  key={b.idBuque}
                  className="px-4 py-2 hover:bg-[#104a8e] hover:text-white cursor-pointer transition-colors"
                  onClick={() => handleSelect(b)}
                >
                  <div className="text-sm font-semibold">{b.nombre}</div>
                  <div className="text-xs opacity-80 mt-0.5">
                    OMI: {b.omi || '-'} | Mat: {b.matricula || '-'} | {b.tipo}
                  </div>
                </li>
              ))}
            </ul>
          ) : debouncedValue.trim().length >= 2 ? (
            <div className="px-4 py-3 text-sm text-gray-500">Sin resultados.</div>
          ) : null}
        </div>
      )}
    </div>
  );
}