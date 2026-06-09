import React from "react";
import { NavLink } from "react-router-dom";

export default function Sidebar() {
  const links = [
    {
      to: "/dashboard",
      label: "Viajes / Dashboard",
      icon: (
        <svg
          className="w-5 h-5 transition-transform group-hover:scale-110"
          fill="none"
          stroke="currentColor"
          strokeWidth="2"
          viewBox="0 0 24 24"
        >
          <path
            strokeLinecap="round"
            strokeLinejoin="round"
            d="M9 20l-5.447-2.724A1 1 0 013 16.382V5.618a1 1 0 011.447-.894L9 7m0 13l6-3m-6 3V7m6 10l4.553 2.276A1 1 0 0021 18.382V7.618a1 1 0 00-.553-.894L15 4m0 13V4m0 0L9 7"
          />
        </svg>
      ),
    },
    {
      to: "/catalogos",
      label: "Catálogos",
      icon: (
        <svg
          className="w-5 h-5 transition-transform group-hover:scale-110"
          fill="none"
          stroke="currentColor"
          strokeWidth="2"
          viewBox="0 0 24 24"
        >
          <path
            strokeLinecap="round"
            strokeLinejoin="round"
            d="M4 6h16M4 10h16M4 14h16M4 18h16"
          />
        </svg>
      ),
    },
  ];

  return (
    <aside className="w-64 bg-slate-900 text-white min-h-screen flex flex-col border-r border-slate-800 shadow-xl shrink-0">
      {/* Encabezado del Sidebar */}
      <div className="p-6 border-b border-slate-800 flex items-center justify-between">
        <div className="flex items-center gap-3">
          <div className="w-8 h-8 rounded-lg bg-blue-600 flex items-center justify-center font-bold text-sm text-white shadow-md border border-blue-500">
            M
          </div>
          <div>
            <h3 className="font-bold text-sm leading-none tracking-wide text-slate-100">
              Navegación
            </h3>
            <span className="text-[10px] text-slate-400 font-medium tracking-wider uppercase mt-1 inline-block">
              Sistema MBPC
            </span>
          </div>
        </div>
      </div>

      {/* Menú de Navegación */}
      <nav className="flex-grow p-4 space-y-1.5" aria-label="Menú principal">
        {links.map((link) => (
          <NavLink
            key={link.to}
            to={link.to}
            className={({ isActive }) =>
              `group flex items-center gap-3 px-4 py-3 rounded-xl text-sm font-semibold transition-all duration-200 ${
                isActive
                  ? "bg-blue-600 text-white shadow-lg shadow-blue-600/20 border border-blue-500/50"
                  : "text-slate-400 hover:bg-slate-800/60 hover:text-slate-100"
              }`
            }
          >
            {link.icon}
            <span>{link.label}</span>
          </NavLink>
        ))}
      </nav>

      {/* Footer del Sidebar */}
      <div className="p-4 border-t border-slate-800 bg-slate-950/40 text-[11px] text-slate-500">
        <p className="font-medium text-center">PNA · DICO · DSIG</p>
      </div>
    </aside>
  );
}
