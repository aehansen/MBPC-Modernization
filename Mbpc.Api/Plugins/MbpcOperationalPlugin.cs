// Archivo: Mbpc.Api/Plugins/MbpcOperationalPlugin.cs
using System;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Mbpc.Api.Services;
using Microsoft.SemanticKernel;

namespace Mbpc.Api.Plugins
{
    /// <summary>
    /// Plugin operativo del sistema MBPC para Semantic Kernel.
    /// Centraliza todas las herramientas de consulta que Gemini puede invocar
    /// sobre tráfico marítimo, telemetría AIS y convoyes.
    /// </summary>
    public sealed class MbpcOperationalPlugin
    {
        private readonly IViajeService          _viajeService;
        private readonly IConvoyManagerService  _convoyService;

        public MbpcOperationalPlugin(
            IViajeService         viajeService,
            IConvoyManagerService convoyService)
        {
            _viajeService  = viajeService;
            _convoyService = convoyService;
        }

        // ── HERRAMIENTA 1: Listado General ──────────────────────────────────

        [KernelFunction("ObtenerViajesActivos")]
        [Description(
            "Obtiene la lista completa de buques activos y viajes en curso registrados en MBPC. " +
            "Usá esta herramienta cuando el usuario pregunte por todos los buques, cuántos hay, " +
            "o quiera un resumen general de la flota activa.")]
        public async Task<string> ObtenerViajesActivosAsync()
        {
            var viajes = await _viajeService.GetViajesAsync();

            if (viajes == null || !viajes.Any()) return "No hay buques registrados o activos en este momento.";

            var lineas = viajes.Select(v =>
                $"- Buque: {v.VesselName ?? "Sin nombre"} | " +
                $"Estado: {v.NavegationStatusDesc ?? "Desconocido"} | " +
                $"MMSI: {v.Mmsi ?? "N/A"} | " +
                $"Última posición: {(v.MsgTime != default ? v.MsgTime.ToString("dd/MM/yyyy HH:mm") : "N/A")}");

            return $"Hay {viajes.Count()} buque(s):\n{string.Join("\n", lineas)}";
        }

        // ── HERRAMIENTA 2: Telemetría ───────────────────────────────────────

        [KernelFunction("ConsultarPosicionBuque")]
        [Description(
            "Busca la posición satelital (telemetría), rumbo y velocidad de un buque específico por su nombre o MMSI. " +
            "Debe usarse siempre como primer paso cuando el usuario pregunta por un buque en particular, para obtener " +
            "su estado actual y su ID interno de viaje, el cual es necesario para consultas más profundas.")]
        public async Task<string> ConsultarPosicionBuqueAsync(
            [Description("Nombre del buque (ej: 'PUMA', 'CAROLINA'). Opcional si se provee MMSI.")] string? nombreBuque = null,
            [Description("Número de identificación MMSI del buque. Opcional si se provee el nombre.")] string? mmsi = null)
        {
            if (string.IsNullOrWhiteSpace(nombreBuque) && string.IsNullOrWhiteSpace(mmsi))
                return "Debes proporcionar el nombre del buque o su MMSI para buscar su posición.";

            var viajes = await _viajeService.GetMapaViajesAsync(mmsi, nombreBuque);
            var viaje = viajes?.FirstOrDefault();

            if (viaje == null)
                return $"No se encontró telemetría ni viajes activos para el buque '{(nombreBuque ?? mmsi)}'.";

            return $"El buque '{viaje.NombreBuque}' (MMSI: {viaje.Mmsi ?? "N/A"}) fue localizado. " +
                   $"Su ID interno de viaje (ViajeId) es: '{viaje.Id}'. " + 
                   $"Posición: Lat {viaje.Latitud}, Lon {viaje.Longitud}. " +
                   $"Velocidad: {viaje.Velocidad} nudos, Rumbo: {viaje.Rumbo}°. " +
                   $"Última actualización: {viaje.UltimaActualizacion}. " +
                   $"Estado: {viaje.EstadoNav}.";
        }

        // ── HERRAMIENTA 3: Operaciones y Convoy ─────────────────────────────

        [KernelFunction("ObtenerDetalleOperativo")]
        [Description(
            "Obtiene el detalle operativo complejo de un buque, incluyendo su remolcador, etapas, y la " +
            "lista completa de barcazas y cargas que lleva (el convoy). " +
            "Requiere obligatoriamente el 'viajeId' (ID interno, ej: '65f...'), que se obtiene llamando previamente a 'ConsultarPosicionBuque'.")]
        public async Task<string> ObtenerDetalleOperativoAsync(
            [Description("El ID interno del viaje en MongoDB (string, ej: '65f1a...').")] string viajeId)
        {
            if (string.IsNullOrWhiteSpace(viajeId))
                return "Error: Se requiere un viajeId válido. Si solo tienes el nombre del buque, usa primero 'ConsultarPosicionBuque'.";

            var sb = new StringBuilder();

            // 1. Obtener detalle de MongoDB (Etapas) - ¡Ahora sí manejamos la Tupla correctamente!
            var (detalle, _) = await _viajeService.GetViajeDetalleByIdAsync(viajeId, default);

            // 2. Obtener detalle del Convoy cruzado (Mongo + Oracle) - ¡Recibe el objeto directo!
            var convoy = await _convoyService.ObtenerConvoyPorViajeIdAsync(viajeId, default);

            if (detalle == null && convoy == null)
                return $"No se encontraron detalles operativos ni convoy para el ViajeId: {viajeId}.";

            // ── Datos de Mongo (Etapas) ─────────────────────────────────────
            if (detalle != null)
            {
                sb.AppendLine("--- DETALLE DE ETAPAS ---");
                if (detalle.Etapas == null || detalle.Etapas.Count == 0)
                {
                    sb.AppendLine("No hay etapas registradas en la hoja de viaje.");
                }
                else
                {
                    foreach (var etapa in detalle.Etapas)
                    {
                        var fecha = etapa.FechaInicio.HasValue ? etapa.FechaInicio.Value.ToString("dd/MM/yyyy HH:mm") : "Sin fecha";
                        sb.AppendLine($"Etapa ID: {etapa.EtapaId} | Inicio: {fecha}");
                        sb.AppendLine($"Remolcador asignado en etapa: {etapa.Remolcador?.Nombre ?? "N/A"}");
                        
                        if (etapa.Barcazas != null && etapa.Barcazas.Count > 0)
                        {
                            sb.AppendLine("Barcazas en esta etapa:");
                            foreach (var b in etapa.Barcazas)
                            {
                                sb.AppendLine($"  • {b.Nombre} | Carga: {b.Cantidad:N2} {b.Unidad} de {b.Carga}" + 
                                              (b.MuelleActual != null ? $" | Muelle: {b.MuelleActual}" : ""));
                            }
                        }
                        else
                        {
                            sb.AppendLine("    Sin barcazas registradas en esta etapa.");
                        }

                        sb.AppendLine();
                    }
                }
            }

            // ── Datos del convoy ────────────────────────────────────────────
            if (convoy != null)
            {
                sb.AppendLine("--- CONVOY ASOCIADO ---");
                sb.AppendLine($"Convoy ID : {convoy.ViajeId}");
                sb.AppendLine($"Buque / Nombre  : {convoy.NombreBuque ?? "N/A"}");
                sb.AppendLine($"Remolcador      : {convoy.Remolcador?.Nombre ?? "N/A"}");
                sb.AppendLine($"Estado remolc.  : {convoy.Remolcador?.Estado ?? "N/A"}");

                var fechaSalida = convoy.Remolcador?.FechaSalida.HasValue == true
                    ? convoy.Remolcador.FechaSalida.Value.ToString("dd/MM/yyyy HH:mm")
                    : "N/A";
                sb.AppendLine($"Fecha de salida : {fechaSalida}");
                sb.AppendLine($"Barcazas activas: {convoy.BarcazasActivas}");
                sb.AppendLine($"Tonelaje total  : {convoy.TonelajeTotal:N2}");

                if (convoy.Barcazas != null && convoy.Barcazas.Count > 0)
                {
                    sb.AppendLine("Composición de barcazas:");
                    foreach (var b in convoy.Barcazas)
                    {
                        sb.AppendLine($"  • {b.Nombre} | {b.Tonelaje:N2} {b.Unidad} de {b.TipoCarga} | " +
                                      $"Estado: {b.Estado}" +
                                      (b.MuelleActual != null ? $" | Muelle: {b.MuelleActual}" : string.Empty));
                    }
                }
            }
            else
            {
                sb.AppendLine("Este buque no integra ningún convoy registrado en MBPC.");
            }

            return sb.ToString().TrimEnd();
        }
    }
}